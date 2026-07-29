using ClosedXML.Excel;
using System.Net;
using System.Text.RegularExpressions;

namespace FacebookPost.Server.Service
{
    public class ShopeeLinkService : IShopeeLinkService
    {
        private readonly HttpClient _httpClient;

        public ShopeeLinkService()
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };

            _httpClient = new HttpClient(handler);

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0");
        }

        public async Task UpdateShopeeLinkAsync(string sourceFile)
        {
            using var workbook = new XLWorkbook(sourceFile);

            var ws = workbook.Worksheet(1);

            var header = ws.FirstRowUsed();

            var map = header.Cells()
                .ToDictionary(
                    x => x.GetString().Trim(),
                    x => x.Address.ColumnNumber);

            var colShort = map["lien_ket_chuyen_doi"];
            var colOrigin = map["lien_ket_goc"];
            var colItemId = map["item_id"];
            var colReason = map["ly_do_that_bai"];

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var shortUrl = row.Cell(colShort).GetString().Trim();

                if (string.IsNullOrWhiteSpace(shortUrl))
                    continue;

                try
                {
                    var finalUrl = await GetFinalUrl(shortUrl);

                    var normalizedUrl = NormalizeShopeeUrl(finalUrl);

                    row.Cell(colOrigin).Value = normalizedUrl;

                    row.Cell(colItemId).Value = GetItemId(normalizedUrl);

                    row.Cell(colReason).Value = "";
                }
                catch (Exception ex)
                {
                    row.Cell(colReason).Value = ex.Message;
                }
            }

            workbook.Save();
        }

        private async Task<string> GetFinalUrl(string url)
        {
            string current = url;

            for (int i = 0; i < 10; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead);

                if (IsRedirect(response.StatusCode))
                {
                    var location = response.Headers.Location;

                    if (location == null)
                        throw new Exception("Redirect không có Location");

                    if (!location.IsAbsoluteUri)
                        location = new Uri(new Uri(current), location);

                    current = location.ToString();

                    continue;
                }

                return current;
            }

            throw new Exception("Redirect quá nhiều.");
        }

        private static bool IsRedirect(HttpStatusCode code)
        {
            return code == HttpStatusCode.Moved
                || code == HttpStatusCode.Redirect
                || code == HttpStatusCode.RedirectMethod
                || code == HttpStatusCode.TemporaryRedirect
                || (int)code == 308;
        }

        private static string GetItemId(string url)
        {
            var match = Regex.Match(
                url,
                @"/(?:product|opaanlp)/\d+/(\d+)"
            );

            return match.Success
                ? match.Groups[1].Value
                : "";
        }

        private static string NormalizeShopeeUrl(string url)
        {
            var match = Regex.Match(
                url,
                @"/(?:product|opaanlp)/(\d+)/(\d+)"
            );

            if (!match.Success)
                return url;

            var shopId = match.Groups[1].Value;
            var itemId = match.Groups[2].Value;

            return $"https://shopee.vn/product/{shopId}/{itemId}";
        }
    }
}
