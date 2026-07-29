using FacebookPost.Server.Model;
using FacebookPost.Server.Service;

namespace FacebookPost.Server.Builder
{
    public class FacebookPostBuilder : IFacebookPostBuilder
    {
        private readonly ITemplateService _templateService;

        public FacebookPostBuilder(ITemplateService templateService)
        {
            _templateService = templateService;
        }

        public string Build(ShopeeProduct product)
        {
            var template = _templateService.GetRandomTemplate();

            var body = template.Body
                .Replace("{title}", product.ShopMall == "yes" ? $"Shopee Mall | {product.Title}" : product.Title)
                .Replace("{shop_name}", product.ShopMall == "yes" ? $"Shop Mall: {product.ShopName}" : $"Branch: {product.ShopName}")
                .Replace("{rating}", product.Rating)
                .Replace("{sales}", product.Sales);

            return $"""
            {template.Hook}
            {template.Cta.Replace("{link_affiliate}", product.LinkAffiliate)}

            {body}

            {string.Join(" ", template.Hashtags)}
            """;
        }
    }
}
