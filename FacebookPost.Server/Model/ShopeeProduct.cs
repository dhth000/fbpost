namespace FacebookPost.Server.Model
{
    public class ShopeeProduct
    {
        public string Index { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string ShopMall { get; set; } = "";
        public long ItemId { get; set; }
        public string Title { get; set; } = "";
        public string ProductImages { get; set; } = "";
        public string ProductVideos { get; set; } = "";
        public string Rating { get; set; } = "";
        public string Sales { get; set; } = "";
        public string LinkAffiliate { get; set; } = "";
        public string IsActive { get; set; } = "";
    }
}
