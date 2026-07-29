namespace FacebookPost.Server.Model
{
    public interface IProductDataService
    {
        Task<string> AddProductToDataFileAsync(string targetFileName, bool isShopMall);
    }
}
