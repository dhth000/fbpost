using ClosedXML.Excel;
using FacebookPost.Server.Builder;
using FacebookPost.Server.Model;
using Microsoft.Playwright;

namespace FacebookPost.Server.Service
{
    public class ShopeeService : IShopeeService
    {
        private readonly HttpClient _httpClient;
        private readonly IFacebookPostBuilder _facebookPostBuilder;

        public ShopeeService(HttpClient httpClient, IFacebookPostBuilder facebookPostBuilder)
        {
            _httpClient = httpClient;
            _facebookPostBuilder = facebookPostBuilder;
        }
    }
}
