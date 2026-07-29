using FacebookPost.Server.Model;
using FacebookPost.Server.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FacebookPost.Server.Controllers
{
    [ApiController]
    [Route("api/facebook")]
    public class FacebookController : Controller
    {
        private readonly IFacebookService _facebookService;
        private readonly IProductDataService _productDataService;

        public FacebookController(IFacebookService facebookService, IProductDataService productDataService)
        {
            _facebookService = facebookService;
            _productDataService = productDataService;
        }

        [HttpGet("AddProductToDataFileAsync")]
        public async Task<string> AddProductToDataFileAsync(string fileName, bool isShopMall)
        {
            return await _productDataService.AddProductToDataFileAsync(fileName, isShopMall);
        }

        [HttpGet("CreateFacebookPostAsync")]
        public async Task<string> CreateFacebookPostAsync()
        {
            var giaDungFileUrl = "https://drive.usercontent.google.com/download?id=1b6RMVPkNDOCCSbu3A4wImG8DL_LpWWyO";
            return await _facebookService.ProcessCreatePostToFacebook(giaDungFileUrl);
        }
    }
}
