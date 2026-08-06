using ClosedXML.Excel;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace FacebookPost.Server.Model
{
    public class ProductDataService : IProductDataService
    {
        public async Task<string> AddProductToDataFileAsync(string sourceFile, string targetFileName)
        {
            var affiliateProducts = ReadProductUrlsFromExcel(sourceFile);

            await GetProductJsonAsync(affiliateProducts);

            var products = BuildShopeeProducts(affiliateProducts);

            var result = SaveProductToExcel(targetFileName, products);

            UpdateProcessAddTargetFile(sourceFile, affiliateProducts.Select(p => p.ItemId));

            return await Task.FromResult(result);
        }

        private static void UpdateProcessAddTargetFile(string sourceFile, IEnumerable<string> itemIds)
        {
            var processedItemIds = itemIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            using var workbook = new XLWorkbook(sourceFile);
            var worksheet = workbook.Worksheet(1);

            var headerMap = worksheet.Row(1)
                .CellsUsed()
                .ToDictionary(
                    c => c.GetString().Trim(),
                    c => c.Address.ColumnNumber,
                    StringComparer.OrdinalIgnoreCase);

            if (!headerMap.TryGetValue("item_id", out var itemIdCol))
                throw new InvalidOperationException("Column 'item_id' not found.");

            if (!headerMap.TryGetValue("process_add_target_file", out var processCol))
                throw new InvalidOperationException("Column 'process_add_target_file' not found.");

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                var itemId = row.Cell(itemIdCol).GetString().Trim();

                if (!processedItemIds.Contains(itemId))
                    continue;

                if (!row.Cell(processCol).GetString().Trim()
                    .Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    row.Cell(processCol).SetValue("yes");
                }
            }

            workbook.Save();
        }

        private List<AffiliateProduct> ReadProductUrlsFromExcel(string filePath)
        {
            const int maxProductsPerRun = 20;

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            var headerMap = worksheet.Row(1)
                .Cells()
                .Select((c, i) => new
                {
                    Name = c.GetString().Trim(),
                    Index = i + 1
                })
                .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

            var requiredColumns = new[]
            {
                "item_id",
                "lien_ket_chuyen_doi",
                "lien_ket_goc",
                "process_add_target_file"
            };

            foreach (var column in requiredColumns)
            {
                if (!headerMap.ContainsKey(column))
                    throw new Exception($"Không tìm thấy cột '{column}'.");
            }

            int itemIdColumn = headerMap["item_id"];
            int affiliateUrlColumn = headerMap["lien_ket_chuyen_doi"];
            int originalUrlColumn = headerMap["lien_ket_goc"];
            int processColumn = headerMap["process_add_target_file"];

            int lastRow = worksheet.LastRowUsed()!.RowNumber();

            var result = new List<AffiliateProduct>();

            for (int row = 2; row <= lastRow; row++)
            {
                if (result.Count >= maxProductsPerRun)
                    break;

                var process = worksheet.Cell(row, processColumn)
                    .GetString()
                    .Trim();

                if (process.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    continue;

                var affiliateUrl = worksheet.Cell(row, affiliateUrlColumn)
                    .GetString()
                    .Trim();

                var originalUrl = worksheet.Cell(row, originalUrlColumn)
                    .GetString()
                    .Trim();

                // Chỉ cần có 1 trong 2 URL
                if (string.IsNullOrWhiteSpace(affiliateUrl) &&
                    string.IsNullOrWhiteSpace(originalUrl))
                {
                    continue;
                }

                result.Add(new AffiliateProduct
                {
                    ItemId = worksheet.Cell(row, itemIdColumn).GetString().Trim(),
                    ProductUrl = affiliateUrl,
                    OriginalUrl = originalUrl
                });
            }

            return result;
        }

        public async Task GetProductJsonAsync(List<AffiliateProduct> products)
        {
            var result = new List<string>();

            using var playwright = await Playwright.CreateAsync();

            var browser = await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");

            var context = browser.Contexts.First();

            foreach (var product in products)
            {
                var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

                string? json = null;

                page.Response += async (_, response) =>
                {
                    if (!response.Url.Contains("/api/v4/pdp/get_pc"))
                        return;

                    json = await response.TextAsync();
                };

                try
                {
                    await page.GotoAsync(product.OriginalUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 60000
                    });

                    await page.WaitForTimeoutAsync(2000);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        //result.Add(json);
                        product.ShopeeProductJson = json;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                await page.GotoAsync("about:blank");
            }
        }

        private List<ShopeeProduct> BuildShopeeProducts(List<AffiliateProduct> affiliateProducts)
        {
            var result = new List<ShopeeProduct>();

            foreach (var affiliate in affiliateProducts)
            {
                if (string.IsNullOrWhiteSpace(affiliate.ShopeeProductJson))
                    continue;

                result.Add(BuildShopeeProduct(affiliate));
            }

            return result;
        }

        private ShopeeProduct BuildShopeeProduct(AffiliateProduct affiliate)
        {
            JObject json = JObject.Parse(affiliate.ShopeeProductJson);

            long itemId = json["data"]?["item"]?["item_id"]?.Value<long>() ?? 0;

            return new ShopeeProduct
            {
                ShopName = json["data"]?["shop_detailed"]?["name"]?.ToString() ?? "",
                ShopMall = json["data"]?["shop_detailed"]?["is_official_shop"]?.ToString().Trim().ToLower() == "true" ? "yes" : "no",
                ItemId = itemId,
                Title = json["data"]?["item"]?["title"]?.ToString() ?? "",

                ProductImages = string.Join(",",
                    json["data"]?["product_images"]?["images"]?
                    .Select(x => x.ToString()) ?? Enumerable.Empty<string>()),

                Rating = Math.Round(json["data"]?["item"]?["item_rating"]?["rating_star"]?.Value<double>() ?? 0, 1).ToString("0.0"),

                Sales = json["data"]?["product_review"]?["sold_count_display"]?.ToString() ?? "",

                Hashtags = GetHashtags(json),

                LinkAffiliate = affiliate.ProductUrl
            };
        }

        private string GetAffiliateLink(string excelPath, long itemId)
        {
            using var wb = new XLWorkbook(excelPath);
            var ws = wb.Worksheet(1);

            var header = ws.FirstRowUsed();

            int itemIdCol = header.Cells()
                .First(x => x.GetString() == "item_id")
                .Address.ColumnNumber;

            int linkCol = header.Cells()
                .First(x => x.GetString() == "lien_ket_chuyen_doi")
                .Address.ColumnNumber;

            string itemIdString = itemId.ToString();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                if (row.Cell(itemIdCol).GetString().Trim() == itemIdString)
                {
                    return row.Cell(linkCol).GetString();
                }
            }

            return string.Empty;
        }

        private string SaveProductToExcel(string targetFileName, List<ShopeeProduct> products)
        {
            using var wb = new XLWorkbook(targetFileName);
            var ws = wb.Worksheet(1);

            var header = ws.FirstRowUsed();

            var columns = header.Cells()
                .ToDictionary(
                    c => c.GetString().Trim().ToLower(),
                    c => c.Address.ColumnNumber);

            //====================================================
            // Cache ItemId -> RowNumber để tìm kiếm nhanh
            //====================================================
            var rowMap = new Dictionary<string, int>();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var itemId = row.Cell(columns["item_id"]).GetString().Trim();

                if (!string.IsNullOrWhiteSpace(itemId) &&
                    !rowMap.ContainsKey(itemId))
                {
                    rowMap[itemId] = row.RowNumber();
                }
            }

            int added = 0;
            int updated = 0;

            foreach (var product in products)
            {
                bool isNewRow = !rowMap.TryGetValue(
                    product.ItemId.ToString(),
                    out int rowNumber);

                if (isNewRow)
                {
                    rowNumber = ws.LastRowUsed() == null
                        ? 2
                        : ws.LastRowUsed().RowNumber() + 1;

                    int index = rowNumber - 1;

                    if (columns.ContainsKey("index"))
                        ws.Cell(rowNumber, columns["index"]).Value = index;

                    // Cập nhật cache để tránh duplicate trong cùng lần chạy
                    rowMap[product.ItemId.ToString()] = rowNumber;

                    added++;
                }
                else
                {
                    updated++;
                }

                //==========================
                // Update dữ liệu
                //==========================
                SetCell(ws, columns, rowNumber, "shop_name", product.ShopName);
                SetCell(ws, columns, rowNumber, "shop_mall", product.ShopMall);
                SetCell(ws, columns, rowNumber, "item_id", product.ItemId.ToString());
                SetCell(ws, columns, rowNumber, "title", product.Title);
                SetCell(ws, columns, rowNumber, "product_images", product.ProductImages);
                SetCell(ws, columns, rowNumber, "product_videos", "");
                SetCell(ws, columns, rowNumber, "product_rating", product.Rating);
                SetCell(ws, columns, rowNumber, "product_sales", product.Sales);
                SetCell(ws, columns, rowNumber, "link_affiliate", product.LinkAffiliate);
                SetCell(ws, columns, rowNumber, "hash_tags", product.Hashtags);
                SetCell(ws, columns, rowNumber, "is_active", true.ToString());
            }

            wb.Save();

            return $"Hoàn thành. Thêm mới: {added}, Cập nhật: {updated}.";
        }

        private void SetCell(IXLWorksheet ws, Dictionary<string, int> columns, int rowNumber, string columnName, string value)
        {
            if (columns.TryGetValue(columnName.ToLower(), out int col))
            {
                ws.Cell(rowNumber, col).Value = value;
            }
        }

        private static string GetHashtags(JObject json)
        {
            Regex hashtagRegex = new(@"#\w+", RegexOptions.Compiled);

            var paragraphList = json["data"]?["product_description"]?["paragraph_list"] as JArray;

            if (paragraphList == null || paragraphList.Count == 0)
                return string.Empty;

            var text = paragraphList.Last?["text"]?.ToString() ?? string.Empty;

            return hashtagRegex.Matches(text).Count > 3
                ? text
                : string.Empty;
        }
    }
}
