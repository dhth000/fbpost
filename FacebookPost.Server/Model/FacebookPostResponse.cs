using System.Text.Json.Serialization;

namespace FacebookPost.Server.Model
{
    public class FacebookPostResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }
}
