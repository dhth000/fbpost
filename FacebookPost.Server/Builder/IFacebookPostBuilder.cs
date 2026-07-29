using FacebookPost.Server.Model;

namespace FacebookPost.Server.Builder
{
    public interface IFacebookPostBuilder
    {
        string Build(ShopeeProduct product);
    }
}
