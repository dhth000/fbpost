namespace FacebookPost.Server.Model
{
    public interface IProductDataService
    {
        Task<string> AddProductToDataFileAsync(string sourceFile, string targetFileName, bool isShopMall);
    }
}
