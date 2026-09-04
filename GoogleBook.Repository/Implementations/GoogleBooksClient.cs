using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GoogleBook.Models.Dto;
using GoogleBook.Models.GoogleBooks;
using GoogleBook.Repository.Interfaces;

namespace GoogleBook.Repository.Implementations
{
    public class GoogleBooksClient : IGoogleBooksClient
    {
        private const int GoogleBooksMaxPerCall = 40;
        private const string SourceName = "GoogleBooks";
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleBooksClient> _logger;
        private readonly string? _apiKey;

        public GoogleBooksClient(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleBooksClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["ExternalApis:GoogleBooks:ApiKey"];
        }

        public async Task<IReadOnlyList<BookDto>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<BookDto>();

            int limit = Math.Clamp(maxResults, 1, GoogleBooksMaxPerCall);
            string? url = $"books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults={limit}";
            if (!string.IsNullOrWhiteSpace(_apiKey))
                url += $"&key={Uri.EscapeDataString(_apiKey)}";

            _logger.LogInformation("Querying Google Books with query '{Query}' (maxResults={MaxResults})", query, limit);

            using HttpResponseMessage httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Google Books API error {StatusCode} for query '{Query}': {Body}",
                    (int)httpResponse.StatusCode,
                    query,
                    body);

                throw httpResponse.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => new InvalidOperationException(
                        "Google Books API quota exceeded. Check your API key, enable the Books API in Google Cloud, or wait for the daily quota reset."),
                    HttpStatusCode.Forbidden => new InvalidOperationException(
                        "Google Books API access denied. Verify the API key and that the Books API is enabled in Google Cloud."),
                    _ => new HttpRequestException($"Google Books API returned {(int)httpResponse.StatusCode}.")
                };
            }

            GoogleBooksResponse? response = await httpResponse.Content.ReadFromJsonAsync<GoogleBooksResponse>(cancellationToken);
            if (response?.Items is null || response.Items.Count == 0)
            {
                _logger.LogInformation("Google Books returned no items for query '{Query}'", query);
                return Array.Empty<BookDto>();
            }

            List<BookDto>? books = response.Items
                .Where(i => i.VolumeInfo is not null && !string.IsNullOrWhiteSpace(i.VolumeInfo!.Title))
                .Select(MapToBook)
                .ToList();

            _logger.LogInformation("Google Books returned {Count} usable volumes for query '{Query}'", books.Count, query);
            return books;
        }

        private static BookDto MapToBook(GoogleBooksItem item)
        {
            GoogleVolumeInfo? info = item.VolumeInfo!;
            return new BookDto
            {
                Title = info.Title!,
                Authors = info.Authors ?? new List<string>(),
                Description = info.Description,
                Publisher = info.Publisher,
                PublishedDate = info.PublishedDate,
                Language = info.Language,
                ThumbnailUrl = info.ImageLinks?.Thumbnail,
                Categories = info.Categories ?? new List<string>(),
                Isbns = info.IndustryIdentifiers?
                    .Where(id => !string.IsNullOrWhiteSpace(id.Identifier))
                    .Select(id => id.Identifier!)
                    .ToList() ?? new List<string>(),
                Source = SourceName,
                ExternalId = item.Id
            };
        }
    }
}