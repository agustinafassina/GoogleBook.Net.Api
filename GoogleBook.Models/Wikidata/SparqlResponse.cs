using System.Text.Json.Serialization;

namespace GoogleBook.Models.Wikidata
{
    public sealed class SparqlResponse
    {
        [JsonPropertyName("results")]
        public SparqlResults? Results { get; set; }
    }
}
