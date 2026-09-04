using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using GoogleBook.Models.Dto;
using GoogleBook.Models.Wikidata;
using GoogleBook.Repository.Interfaces;

namespace GoogleBook.Repository.Implementations
{
    public class WikidataClient : IWikidataClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WikidataClient> _logger;

        public WikidataClient(HttpClient httpClient, ILogger<WikidataClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<AuthorDto?> GetAuthorProfileAsync(string authorName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(authorName))
                return null;

            string? sparql = BuildQuery(authorName);
            string? url = $"sparql?format=json&query={Uri.EscapeDataString(sparql)}";

            _logger.LogInformation("Querying Wikidata for author '{Author}'", authorName);

            SparqlResponse? response;
            try
            {
                response = await _httpClient.GetFromJsonAsync<SparqlResponse>(url, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Wikidata request failed for author '{Author}'", authorName);
                return null;
            }

            List<Dictionary<string, SparqlValue>>? bindings = response?.Results?.Bindings;
            if (bindings is null || bindings.Count == 0)
            {
                _logger.LogInformation("Wikidata returned no match for author '{Author}'", authorName);
                return null;
            }

            Dictionary<string, SparqlValue>? row = bindings[0];
            List<string>? occupations = (ReadValue(row, "occupations") ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();

            return new AuthorDto
            {
                Name = ReadValue(row, "personLabel") ?? authorName,
                WikidataId = ExtractEntityId(ReadValue(row, "person")),
                Gender = ReadValue(row, "genderLabel"),
                SexualOrientation = ReadValue(row, "orientationLabel"),
                CountryOfCitizenship = ReadValue(row, "countryLabel"),
                Description = ReadValue(row, "description"),
                Occupations = occupations
            };
        }

        private static string? ReadValue(Dictionary<string, SparqlValue> binding, string key)
            => binding.TryGetValue(key, out var value) ? value?.Value : null;

        private static string BuildQuery(string authorName)
        {
            string? safeName = authorName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $$"""
                SELECT ?person ?personLabel ?genderLabel ?orientationLabel ?countryLabel ?description
                       (GROUP_CONCAT(DISTINCT ?occupationLabel; SEPARATOR="; ") AS ?occupations)
                WHERE {
                  ?person rdfs:label "{{safeName}}"@en.
                  ?person wdt:P31 wd:Q5.
                  OPTIONAL { ?person wdt:P21 ?gender. ?gender rdfs:label ?genderLabel. FILTER(LANG(?genderLabel) = "en") }
                  OPTIONAL { ?person wdt:P91 ?orientation. ?orientation rdfs:label ?orientationLabel. FILTER(LANG(?orientationLabel) = "en") }
                  OPTIONAL { ?person wdt:P27 ?country. ?country rdfs:label ?countryLabel. FILTER(LANG(?countryLabel) = "en") }
                  OPTIONAL { ?person wdt:P106 ?occupation. ?occupation rdfs:label ?occupationLabel. FILTER(LANG(?occupationLabel) = "en") }
                  OPTIONAL { ?person schema:description ?description. FILTER(LANG(?description) = "en") }
                  SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
                }
                GROUP BY ?person ?personLabel ?genderLabel ?orientationLabel ?countryLabel ?description
                LIMIT 1
                """;
        }

        private static string? ExtractEntityId(string? entityUri)
        {
            if (string.IsNullOrWhiteSpace(entityUri))
                return null;
            int index = entityUri.LastIndexOf('/');
            return index >= 0 ? entityUri[(index + 1)..] : entityUri;
        }
    }
}