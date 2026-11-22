using ClosedXML.Excel;
using FacebookPost.Server.Builder;
using FacebookPost.Server.Model;
using FacebookPost.Server.Options;
using FacebookPost.Server.Providers;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace FacebookPost.Server.Service
{
    public class FacebookService : IFacebookService
    {
        private readonly HttpClient _httpClient;
        private readonly FacebookOptions _facebookOptions;
        private readonly IFacebookPostBuilder _facebookPostBuilder;
        private readonly IDateTimeProvider _dateTimeProvider;

        private static readonly Random _rng = new Random();

        public FacebookService(HttpClient httpClient, IConfiguration configuration, IFacebookPostBuilder facebookPostBuilder, IDateTimeProvider dateTimeProvider)
        {
            _httpClient = httpClient;
            _facebookPostBuilder = facebookPostBuilder;
            _dateTimeProvider = dateTimeProvider;
            _facebookOptions = configuration.GetSection("Facebook").Get<FacebookOptions>()!;
        }

        public async Task<string> ProcessCreatePostToFacebook(string pageId)
        {
            var page = GetFacebookPage(pageId);
            if (page == null) return $"Facebook page with ID {pageId} not found.";

            var shopeeData = await ReadDataFileFromDriveAsync(page.ProductsUrl);
            if (shopeeData == null || shopeeData.Count == 0) return "No data available to process.";

            var productsPost = GetProductsPost(shopeeData);
            if (productsPost == null || productsPost.Count == 0) return "No valid products found.";

            var now = _dateTimeProvider.VietnamNow;
            var result = string.Empty;

            foreach (var (index, product) in productsPost.Select((x, i) => (i, x)))
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

                var publishTime = GetRandomPublishTime(now, index);

                if (publishTime <= now)
                {
                    publishTime = publishTime.AddDays(1);
                }

                // ----- 1. Tạo Post -----
                try
                {
                    await PublishFacebookPostAsync(
                        page,
                        imageFiles,
                        postContent,
                        publishTime);

                    result += $"\n[{pageId}] Processed product: {product.Title} -> Post created successfully.\n";
                }
                catch (Exception ex)
                {
                    result += $"\n[{pageId}] Failed to create Post for product: {product.Title} -> {ex.Message}\n";
                }

                // -----2.Tạo Reel(dùng chung ảnh vừa tải, lệch giờ đăng để không trùng Post)---- -
                try
                {
                    var reelPublishTime = publishTime.AddMinutes(Random.Shared.Next(10, 20)); // tránh trùng giờ với Post, tùy chỉnh nếu cần

                    var reelResult = await PublishFacebookReelAsync(
                        page,
                        imageFiles,
                        postContent,
                        reelPublishTime);

                    result += $"[{pageId}] Processed product: {product.Title} -> {reelResult}\n";
                }
                catch (Exception ex)
                {
                    result += $"[{pageId}] Failed to create Reel for product: {product.Title} -> {ex.Message}\n";
                    Console.WriteLine($"[{pageId}] Failed to create Reel for product: {product.Title} -> {ex.Message}");
                }

                foreach (var file in imageFiles)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
            }

            return result;
        }

        public async Task<string> ProcessCreatePostToAllFacebookPages()
        {
            if (_facebookOptions.Pages == null || !_facebookOptions.Pages.Any())
            {
                return "No Facebook pages configured.";
            }

            var startTimes = GenerateStartTimes(_dateTimeProvider.VietnamNow, _facebookOptions.Pages.Count);

            var tasks = _facebookOptions.Pages
                .Select((page, index) =>
                    ProcessFacebookPageAsync(page, startTimes[index]));

            var results = await Task.WhenAll(tasks);

            return string.Join(Environment.NewLine, results);
        }

        // 3. Publish Reel lên Facebook (start -> upload -> finish)
        public async Task<string> PublishFacebookReelAsync(
            FacebookPageOption page,
            List<string> imageFiles,
            string postContent,
            DateTime publishTime)
        {
            string reelVideoPath = null;

            try
            {
                var musicFile = GetRandomMusicFile();
                reelVideoPath = await CreateReelVideoAsync(imageFiles, musicFile);

                using var httpClient = new HttpClient();
                var apiVersion = page.GraphVersion ?? "v19.0";
                var baseUrl = $"https://graph.facebook.com/{apiVersion}/{page.PageId}/video_reels";

                // --- Phase 1: START ---
                var startResp = await httpClient.PostAsync(
                    $"{baseUrl}?upload_phase=start&access_token={page.AccessToken}",
                    null);
                var startJson = JObject.Parse(await startResp.Content.ReadAsStringAsync());

                if (!startResp.IsSuccessStatusCode)
                    throw new Exception($"Start phase failed: {startJson}");

                var videoId = startJson["video_id"]!.ToString();
                var uploadUrl = startJson["upload_url"]!.ToString();

                // --- Phase 2: UPLOAD binary video lên uploadUrl ---
                var fileBytes = await File.ReadAllBytesAsync(reelVideoPath);
                using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                uploadRequest.Headers.Add("Authorization", $"OAuth {page.AccessToken}");
                uploadRequest.Headers.Add("offset", "0");
                uploadRequest.Headers.Add("file_size", fileBytes.Length.ToString());
                uploadRequest.Content = new ByteArrayContent(fileBytes);
                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var uploadResp = await httpClient.SendAsync(uploadRequest);
                if (!uploadResp.IsSuccessStatusCode)
                    throw new Exception($"Upload phase failed: {await uploadResp.Content.ReadAsStringAsync()}");

                await Task.Delay(10000); // Delay 10s để chắc chắn video đã được xử lý xong trước khi gọi finish

                // --- Phase 3: FINISH (publish hoặc schedule) ---
                var scheduledTimestamp = _dateTimeProvider.ToUnixTimeSeconds(publishTime);

                var finishParams = new Dictionary<string, string>
                {
                    ["upload_phase"] = "finish",
                    ["video_id"] = videoId,
                    ["description"] = postContent,
                    ["video_state"] = "SCHEDULED",
                    ["scheduled_publish_time"] = scheduledTimestamp.ToString(),
                    ["access_token"] = page.AccessToken
                };

                var finishResp = await httpClient.PostAsync(baseUrl, new FormUrlEncodedContent(finishParams));
                var finishJson = await finishResp.Content.ReadAsStringAsync();

                if (!finishResp.IsSuccessStatusCode)
                    throw new Exception($"Finish phase failed: {finishJson}");

                return $"[{page.PageId}] Reel published successfully. VideoId: {videoId}, ScheduledAt: {publishTime}";
            }
            finally
            {
                // Dọn file tạm
                if (reelVideoPath != null && File.Exists(reelVideoPath))
                    File.Delete(reelVideoPath);
            }
        }

        // 1. Lấy random 1 file nhạc trong khoảng 1.mp4 -> 49.mp4, trong thư mục Musics của project
        private string GetRandomMusicFile()
        {
            // AppContext.BaseDirectory = thư mục chứa file .dll/.exe khi chạy (thường là bin/Debug/net.../)
            var musicFolder = Path.Combine(AppContext.BaseDirectory, "Musics");

            if (!Directory.Exists(musicFolder))
                throw new DirectoryNotFoundException($"Music folder not found: {musicFolder}");

            var index = _rng.Next(1, 50); // 1 -> 49
            var musicPath = Path.Combine(musicFolder, $"{index}.mp4");

            if (!File.Exists(musicPath))
                throw new FileNotFoundException($"Music file not found: {musicPath}");

            return musicPath;
        }

        // 1b. Lấy đường dẫn ffmpeg.exe đóng gói sẵn trong project (không cần cài trên máy)
        private string GetFfmpegExecutablePath()
        {
            string relativePath;

            if (OperatingSystem.IsWindows())
            {
                relativePath = Path.Combine("ffmpeg", "win-x64", "ffmpeg.exe");
            }
            else if (OperatingSystem.IsLinux())
            {
                relativePath = Path.Combine("ffmpeg", "linux-x64", "ffmpeg");
            }
            else
            {
                throw new PlatformNotSupportedException(
                    "Chỉ hỗ trợ chạy ffmpeg trên Windows hoặc Linux.");
            }

            var path = Path.Combine(AppContext.BaseDirectory, relativePath);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"FFmpeg not found. Expected at: {path}. " +
                    $"Hãy copy file ffmpeg vào thư mục '{relativePath}' trong project.");

            return path;
        }

        // 2. Ghép ảnh sản phẩm + nhạc thành video reel bằng FFmpeg
        private async Task<string> CreateReelVideoAsync(List<string> imageFiles, string musicFile)
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"reel_{Guid.NewGuid()}.mp4");

            // Tạo file danh sách ảnh cho ffmpeg concat (mỗi ảnh hiển thị vài giây)
            var listFilePath = Path.Combine(Path.GetTempPath(), $"list_{Guid.NewGuid()}.txt");
            var sb = new StringBuilder();
            const double durationPerImage = 5.0; // giây / ảnh

            foreach (var img in imageFiles)
            {
                sb.AppendLine($"file '{img.Replace("\\", "/")}'");
                sb.AppendLine($"duration {durationPerImage}");
            }
            // ffmpeg yêu cầu lặp lại file cuối để duration cuối có hiệu lực
            sb.AppendLine($"file '{imageFiles.Last().Replace("\\", "/")}'");
            await File.WriteAllTextAsync(listFilePath, sb.ToString());

            // ffmpeg: ghép ảnh thành video (silent), scale cho đúng tỉ lệ 9:16, sau đó mux với audio
            var args = $"-y -f concat -safe 0 -i \"{listFilePath}\" " +
                       $"-i \"{musicFile}\" " +
                       $"-vf \"scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2,setsar=1\" " +
                       $"-c:v libx264 -pix_fmt yuv420p -r 30 " +
                       $"-c:a aac -shortest -map 0:v:0 -map 1:a:0 " +
                       $"\"{outputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = GetFfmpegExecutablePath(), // thay vì "ffmpeg" -> trỏ đúng file đóng gói sẵn
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Không thể khởi động ffmpeg process.");

            var stdErrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            File.Delete(listFilePath);

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                var stderr = await stdErrTask;
                throw new Exception($"FFmpeg failed (ExitCode={process.ExitCode}). Chi tiết: {stderr}");
            }

            return outputPath;
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

        private async Task<string> ProcessFacebookPageAsync(FacebookPageOption page, DateTime startTime)
        {
            try
            {
                var now = _dateTimeProvider.VietnamNow;

                var delay = startTime - now;

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }

                return await ProcessCreatePostToFacebook(page.PageId);
            }
            catch (Exception ex)
            {
                return $"[{page.Name}] ERROR: {ex.Message}";
            }
        }

        private List<DateTime> GenerateStartTimes(DateTime now, int pageCount)
        {
            var start = now.Date.AddHours(22).AddMinutes(05);
            var end = now.Date.AddHours(22).AddMinutes(55);

            // Sau 23:15 thì chạy ngay toàn bộ
            if (now >= end)
            {
                return Enumerable.Repeat(now, pageCount).ToList();
            }

            // Nếu đã qua 22:05 thì random từ thời điểm hiện tại
            if (now > start)
            {
                start = now;
            }

            var totalSeconds = (int)(end - start).TotalSeconds;

            if (totalSeconds <= 0)
            {
                return Enumerable.Repeat(now, pageCount).ToList();
            }

            // Không để vượt quá số giây có thể random
            pageCount = Math.Min(pageCount, totalSeconds + 1);

            var randomSeconds = Enumerable.Range(0, totalSeconds + 1)
                .OrderBy(_ => Random.Shared.Next())
                .Take(pageCount)
                .OrderBy(x => x);

            return randomSeconds
                .Select(x => start.AddSeconds(x))
                .ToList();
        }

        private async Task<string> UploadPhotoAsync(FacebookPageOption page, string imagePath)
        {
            var url = $"https://graph.facebook.com/{page.GraphVersion}/{page.PageId}/photos";

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

        private async Task CreateScheduledPostAsync(FacebookPageOption page, string message, List<string> mediaIds, DateTime publishTime)
        {
            var url = $"https://graph.facebook.com/{page.GraphVersion}/{page.PageId}/feed";

            using var form = new MultipartFormDataContent();

            form.Add(new StringContent(page.AccessToken), "access_token");

            form.Add(new StringContent(message), "message");

            form.Add(new StringContent("false"), "published");

            var unixTime = _dateTimeProvider.ToUnixTimeSeconds(publishTime);

            form.Add(new StringContent(unixTime.ToString()),"scheduled_publish_time");

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

            using var response = await _httpClient.PostAsync(url, form);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(json);
            }

            var result = JsonSerializer.Deserialize<FacebookPostResponse>(json)!;

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
                        Hashtags = GetString(row, headerMap, "hash_tags"),
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

        private DateTime GetRandomPublishTime(DateTime today, int index)
        {
            var random = Random.Shared;

            int[] TimeSlots =
            {
                0, // 07:00
                1, // 11:30
                2, // 19:00
                3, // 20:30
                2, // 19:00 (ưu tiên)
                3  // 20:30 (ưu tiên)
            };

            var slot = TimeSlots[index % TimeSlots.Length];

            return slot switch
            {
                0 => today.Date
                    .AddHours(7)
                    .AddMinutes(random.Next(0, 91)),          // 07:00 - 08:30

                1 => today.Date
                    .AddHours(11)
                    .AddMinutes(30 + random.Next(0, 91)),     // 11:30 - 13:00

                2 => today.Date
                    .AddHours(19)
                    .AddMinutes(random.Next(0, 91)),          // 19:00 - 20:30

                3 => today.Date
                    .AddHours(20)
                    .AddMinutes(30 + random.Next(0, 91)),     // 20:30 - 22:00

                _ => throw new InvalidOperationException($"Invalid time slot: {slot}")
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

        private List<ShopeeProduct> GetProductsPost(List<ShopeeProduct> shopeeProducts)
        {
            var numbersOfPost = _facebookOptions.NumbersOfPosts;
            var baseDate = new DateTime(2026, 1, 1);
            int days = (DateTime.Today - baseDate).Days;
            int block = days % (int)Math.Ceiling(shopeeProducts.Count / (double)numbersOfPost);
            int skip = block * numbersOfPost;

            var ordered = shopeeProducts
                .Where(x => x.IsActive.Trim() != "false")
                .OrderBy(x => x.Index)
                .ToList();

            return Enumerable.Range(0, numbersOfPost)
                .Select(i => ordered[(skip + i) % ordered.Count])
                .ToList();
        }

        private FacebookPageOption GetFacebookPage(string pageId)
        {
            return _facebookOptions.Pages.First(x => x.PageId == pageId);
        }
    }
}
