using System.Text.Json.Serialization;

namespace GoogleBook.Models.Wikidata
{
    public sealed class SparqlResults
    {
        [JsonPropertyName("bindings")]
        public List<Dictionary<string, SparqlValue>>? Bindings { get; set; }
    }
}