using System.Text.Json.Serialization;

namespace GoogleBook.Models.GoogleBooks
{
    public sealed class GoogleBooksResponse
    {
        [JsonPropertyName("items")]
        public List<GoogleBooksItem>? Items { get; set; }
    }
}