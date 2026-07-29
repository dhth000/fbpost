using ClosedXML.Excel;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FacebookPost.Server.Model
{
    public class ProductDataService : IProductDataService
    {
        public async Task<string> AddProductToDataFileAsync(string sourceFile, string targetFileName)
        {
            string jsonFile = @"D:\AffiliateData\add-product.json";
            string affiliateFile = sourceFile;
            string targetFile = targetFileName;

            //B1 + B2 + B3
            ShopeeProduct product = BuildShopeeProduct(jsonFile, affiliateFile);

            //B4
            var result = SaveProductToExcel(targetFile, product);

            return await Task.FromResult(result);
        }

        private ShopeeProduct BuildShopeeProduct(string jsonPath, string affiliateExcelPath)
        {
            JObject json = JObject.Parse(File.ReadAllText(jsonPath));

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

                LinkAffiliate = GetAffiliateLink(affiliateExcelPath, itemId)
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

        private string SaveProductToExcel(string filePath, ShopeeProduct product)
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheet(1);

            var header = ws.FirstRowUsed();

            var columns = header.Cells()
                .ToDictionary(
                    c => c.GetString().Trim().ToLower(),
                    c => c.Address.ColumnNumber);

            //==========================
            // Tìm row theo ItemId
            //==========================
            int rowNumber = 0;

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                string itemId = row.Cell(columns["item_id"]).GetString().Trim();

                if (itemId == product.ItemId.ToString())
                {
                    rowNumber = row.RowNumber();
                    break;
                }
            }

            bool isNewRow = rowNumber == 0;

            if (isNewRow)
            {
                rowNumber = ws.LastRowUsed() == null
                    ? 2
                    : ws.LastRowUsed().RowNumber() + 1;

                int index = rowNumber - 1;

                if (columns.ContainsKey("index"))
                    ws.Cell(rowNumber, columns["index"]).Value = index;
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

            wb.Save();

            return isNewRow == true ? $"Đã thêm sản phẩm: {product.Title}" : $"Đã update sản phẩm: {product.Title}";
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
