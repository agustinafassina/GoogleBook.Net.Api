using System.Text.Json.Serialization;

namespace GoogleBook.Models.Wikidata
{
    public sealed class SparqlValue
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}