using ClosedXML.Excel;
using FacebookPost.Server.Builder;
using FacebookPost.Server.Model;
using FacebookPost.Server.Options;
using System.Text.Json;

namespace FacebookPost.Server.Service
{
    public class FacebookService : IFacebookService
    {
        private readonly HttpClient _httpClient;
        private readonly FacebookOptions _facebookOptions;
        private readonly IFacebookPostBuilder _facebookPostBuilder;

        public FacebookService(HttpClient httpClient, IConfiguration configuration, IFacebookPostBuilder facebookPostBuilder)
        {
            _httpClient = httpClient;
            _facebookPostBuilder = facebookPostBuilder;
            _facebookOptions = configuration.GetSection("Facebook").Get<FacebookOptions>()!;
        }

        public async Task<string> ProcessCreatePostToFacebook(string pageId)
        {
            var page = GetFacebookPage(pageId);
            if (page == null) return $"Facebook page with ID {pageId} not found.";

            var shopeeData = await ReadDataFileFromDriveAsync(page.ProductsUrl);
            if (shopeeData == null || shopeeData.Count == 0) return "No data available to process.";

            var fiveProducts = GetFiveProductPost(shopeeData);
            if (fiveProducts == null || fiveProducts.Count == 0) return "No valid products found.";

            var result = string.Empty;

            foreach (var (index, product) in fiveProducts.Select((x, i) => (i, x)))
            {
                var postContent = _facebookPostBuilder.Build(product);

                var imageUrls = product.ProductImages
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => $"https://down-vn.img.susercontent.com/file/{x.Trim()}")
                        .ToList();

                var imageFiles = new List<string>();

                foreach (var imageUrl in imageUrls)
                {
                    imageFiles.Add(await DownloadImageAsync(imageUrl));
                }

                var publishTime = GetRandomPublishTime(index);

                await PublishFacebookPostAsync(
                    page,
                    imageFiles,
                    postContent,
                    publishTime <= DateTime.Now
                        ? publishTime.AddDays(1)
                        : publishTime);

                foreach (var file in imageFiles)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }

                result += $"\nProcessed product {product.Title} successfully.\n";
            }

            return result;
        }

        private async Task PublishFacebookPostAsync(FacebookPageOption page, List<string> imageFiles, string message, DateTime publishTime)
        {
            //----------------------------------------
            // Upload tất cả ảnh
            //----------------------------------------

            var mediaIds = new List<string>();

            foreach (var image in imageFiles)
            {
                var mediaId = await UploadPhotoAsync(page, image);

                mediaIds.Add(mediaId);
            }

            //----------------------------------------
            // Tạo bài viết
            //----------------------------------------

            await CreateScheduledPostAsync(
                page,
                message,
                mediaIds,
                publishTime);
        }

        private async Task<string> UploadPhotoAsync(FacebookPageOption page, string imagePath)
        {
            var url =
                $"https://graph.facebook.com/{page.GraphVersion}/{page.PageId}/photos";

            using var form = new MultipartFormDataContent();

            form.Add(
                new StringContent(page.AccessToken),
                "access_token");

            form.Add(
                new StringContent("false"),
                "published");

            await using var stream = File.OpenRead(imagePath);

            form.Add(
                new StreamContent(stream),
                "source",
                Path.GetFileName(imagePath));

            using var response =
                await _httpClient.PostAsync(url, form);

            var json =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(json);
            }

            var result =
                JsonSerializer.Deserialize<FacebookPhotoResponse>(json)!;

            return result.Id;
        }

        private async Task CreateScheduledPostAsync(FacebookPageOption page,string message, List<string> mediaIds, DateTime publishTime)
        {
            var url = $"https://graph.facebook.com/{page.GraphVersion}/{page.PageId}/feed";

            using var form = new MultipartFormDataContent();

            form.Add(
                new StringContent(page.AccessToken),
                "access_token");

            form.Add(
                new StringContent(message),
                "message");

            form.Add(
                new StringContent("false"),
                "published");

            var unixTime =
                new DateTimeOffset(publishTime)
                    .ToUnixTimeSeconds();

            form.Add(
                new StringContent(unixTime.ToString()),
                "scheduled_publish_time");

            for (int i = 0; i < mediaIds.Count; i++)
            {
                var attachedMedia =
                    JsonSerializer.Serialize(new
                    {
                        media_fbid = mediaIds[i]
                    });

                form.Add(
                    new StringContent(attachedMedia),
                    $"attached_media[{i}]");
            }

            using var response =
                await _httpClient.PostAsync(url, form);

            var json =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(json);
            }

            var result =
                JsonSerializer.Deserialize<FacebookPostResponse>(json)!;

            Console.WriteLine($"Facebook PostId : {result.Id}");
        }

        public async Task<List<ShopeeProduct>> ReadDataFileFromDriveAsync(string downloadUrl)
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(downloadUrl);

                using var stream = new MemoryStream(bytes);

                using var workbook = new XLWorkbook(stream);

                var ws = workbook.Worksheet(1);

                var headerMap = ws.Row(1)
                    .CellsUsed()
                    .ToDictionary(
                        c => c.GetString().Trim(),
                        c => c.Address.ColumnNumber,
                        StringComparer.OrdinalIgnoreCase);

                var result = new List<ShopeeProduct>();

                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var addProduct = new ShopeeProduct
                    {
                        Index = GetString(row, headerMap, "index"),
                        ShopName = GetString(row, headerMap, "shop_name"),
                        ShopMall = GetString(row, headerMap, "shop_mall"),
                        ItemId = long.TryParse(GetString(row, headerMap, "item_id"), out var id) ? id : 0,
                        Title = GetString(row, headerMap, "title"),
                        ProductImages = GetString(row, headerMap, "product_images"),
                        ProductVideos = GetString(row, headerMap, "product_videos"),
                        Rating = GetString(row, headerMap, "product_rating"),
                        Sales = GetString(row, headerMap, "product_sales"),
                        LinkAffiliate = GetString(row, headerMap, "link_affiliate"),
                        IsActive = GetString(row, headerMap, "is_active")
                    };

                    if (addProduct.IsActive.Trim().ToLower() != "false")
                    {
                        result.Add(addProduct);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async Task<string> DownloadImageAsync(string imageUrl)
        {
            var folder =
                Path.Combine(
                    Path.GetTempPath(),
                    "FacebookImages");

            Directory.CreateDirectory(folder);

            var fileName =
                $"{Guid.NewGuid()}.jpg";

            var fullPath =
                Path.Combine(folder, fileName);

            using var response =
                await _httpClient.GetAsync(imageUrl);

            response.EnsureSuccessStatusCode();

            await using var fs =
                new FileStream(
                    fullPath,
                    FileMode.Create);

            await response.Content.CopyToAsync(fs);

            return fullPath;
        }

        private static DateTime GetRandomPublishTime(int index)
        {
            var random = Random.Shared;

            var today = DateTime.Today;

            return index switch
            {
                0 => today
                        .AddHours(7)
                        .AddMinutes(random.Next(0, 91)),

                1 => today
                        .AddHours(11)
                        .AddMinutes(30 + random.Next(0, 91)),

                2 => today
                        .AddHours(19)
                        .AddMinutes(random.Next(0, 91)),

                3 => today
                        .AddHours(20)
                        .AddMinutes(30 + random.Next(0, 91)),

                _ => today
                        .AddHours(20)
                        .AddMinutes(30 + random.Next(0, 91))
            };
        }

        private static string GetString(IXLRow row,
            Dictionary<string, int> headerMap,
            string header)
        {
            return headerMap.TryGetValue(header, out var col)
                ? row.Cell(col).GetString()
                : "";
        }

        private static List<ShopeeProduct> GetFiveProductPost(List<ShopeeProduct> shopeeProducts)
        {
            var baseDate = new DateTime(2026, 1, 1);
            int days = (DateTime.Today - baseDate).Days;
            int block = days % (int)Math.Ceiling(shopeeProducts.Count / 5.0);
            int skip = block * 5;
            return shopeeProducts
                .Where(x => x.IsActive.Trim().ToLower() != "false")
                .OrderBy(x => x.Index)
                .Skip(skip)
                .Take(5)
                .ToList();
        }

        private FacebookPageOption GetFacebookPage(string pageId)
        {
            return _facebookOptions.Pages.First(x => x.PageId == pageId);
        }
    }
}
