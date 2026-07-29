namespace FacebookPost.Server.Options
{
    public class FacebookOptions
    {
        public List<FacebookPageOption> Pages { get; set; } = [];
    }

    public class FacebookPageOption
    {
        public required string PageId { get; init; }

        public required string Name { get; init; }

        public required string ProductsUrl { get; init; }

        public required string AccessToken { get; init; }

        public required string GraphVersion { get; init; }
    }
}
