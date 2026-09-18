using System.Text.Json.Serialization;

namespace GoogleBook.Models.GoogleBooks
{
    public sealed class GoogleVolumeInfo
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("authors")]
        public List<string>? Authors { get; set; }

        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }

        [JsonPropertyName("publishedDate")]
        public string? PublishedDate { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("categories")]
        public List<string>? Categories { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("industryIdentifiers")]
        public List<GoogleIndustryIdentifier>? IndustryIdentifiers { get; set; }

        [JsonPropertyName("imageLinks")]
        public GoogleImageLinks? ImageLinks { get; set; }
    }
}