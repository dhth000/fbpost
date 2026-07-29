using System.Text.Json.Serialization;

namespace FacebookPost.Server.Model
{
    public class FacebookPhotoResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }
}
