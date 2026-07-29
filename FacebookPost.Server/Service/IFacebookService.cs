namespace FacebookPost.Server.Service
{
    public interface IFacebookService
    {
        Task<string> ProcessCreatePostToFacebook(string downloadUrl);
    }
}
