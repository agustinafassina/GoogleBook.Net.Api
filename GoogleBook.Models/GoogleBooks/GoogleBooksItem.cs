using System.Text.Json.Serialization;

namespace GoogleBook.Models.GoogleBooks
{
    public sealed class GoogleBooksItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("volumeInfo")]
        public GoogleVolumeInfo? VolumeInfo { get; set; }
    }
}