using System.Text.Json.Serialization;

namespace FacebookPost.Server.Model
{
    public class FacebookTemplate
    {
        [JsonPropertyName("id")]
        public required int Id { get; init; }

        [JsonPropertyName("hook")]
        public required string Hook { get; init; }

        [JsonPropertyName("body")]
        public required string Body { get; init; }

        [JsonPropertyName("cta")]
        public required string Cta { get; init; }

        [JsonPropertyName("hashtags")]
        public required List<string> Hashtags { get; init; }

        [JsonPropertyName("priority")]
        public required int Priority { get; init; }

        [JsonPropertyName("isActive")]
        public required bool IsActive { get; init; }
    }
}
