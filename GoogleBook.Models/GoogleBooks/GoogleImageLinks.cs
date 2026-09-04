using System.Text.Json.Serialization;

namespace GoogleBook.Models.GoogleBooks
{
    public sealed class GoogleImageLinks
    {
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }
    }
}