using System.Text.Json.Serialization;

namespace GoogleBook.Models.GoogleBooks
{
    public sealed class GoogleIndustryIdentifier
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("identifier")]
        public string? Identifier { get; set; }
    }
}