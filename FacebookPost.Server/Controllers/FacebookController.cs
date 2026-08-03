using FacebookPost.Server.Model;
using FacebookPost.Server.Service;
using Microsoft.AspNetCore.Mvc;

namespace FacebookPost.Server.Controllers
{
    [ApiController]
    [Route("api/facebook")]
    public class FacebookController : Controller
    {
        private readonly IFacebookService _facebookService;
        private readonly IProductDataService _productDataService;
        private readonly IShopeeLinkService _shopeeLinkService;

        public FacebookController(IFacebookService facebookService, 
            IProductDataService productDataService, 
            IShopeeLinkService shopeeLinkService)
        {
            _facebookService = facebookService;
            _productDataService = productDataService;
            _shopeeLinkService = shopeeLinkService;
        }

        [HttpPost("publish-all")]
        public async Task<IActionResult> PublishAll()
        {
            var result = await _facebookService.ProcessCreatePostToAllFacebookPages();

            return Ok(result);
        }

        [HttpPost("pages/{pageId}/publish")]
        public async Task<IActionResult> Publish(string pageId)
        {
            var result = await _facebookService.ProcessCreatePostToFacebook(pageId);
            return Ok(result);
        }

        [HttpGet("AddProductToDataFileAsync")]
        public async Task<string> AddProductToDataFileAsync(string sourceFile, string targetFile)
        {
            return await _productDataService.AddProductToDataFileAsync(sourceFile, targetFile);
        }

        [HttpPost("update-item-ids")]
        public async Task<IActionResult> UpdateItemId(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile))
            {
                return BadRequest("sourceFile is required.");
            }

            if (!System.IO.File.Exists(sourceFile))
            {
                return NotFound($"File not found: {sourceFile}");
            }

            await _shopeeLinkService.UpdateShopeeLinkAsync(sourceFile);

            return Ok(new
            {
                Success = true,
                Message = "Update completed."
            });
        }
    }
}