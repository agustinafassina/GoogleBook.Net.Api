using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GoogleBook.Models.Dto;
using GoogleBook.Repository.Implementations;
using GoogleBook.UnitTests.TestHelpers;

namespace GoogleBook.UnitTests.Repository;

public class GoogleBooksClientTests
{
    private static GoogleBooksClient CreateSut(HttpMessageHandler handler, string? apiKey = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ExternalApis:GoogleBooks:ApiKey"]).Returns(apiKey);

        return new GoogleBooksClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://www.googleapis.com/") },
            config.Object,
            NullLogger<GoogleBooksClient>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_returns_empty_for_blank_query(string? query)
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called"));
        GoogleBooksClient? sut = CreateSut(handler);

        IReadOnlyList<BookDto> result = await sut.SearchAsync(query!, 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_maps_volume_fields_from_google_response()
    {
        const string json = """
            {
              "items": [
                {
                  "id": "vol1",
                  "volumeInfo": {
                    "title": "Test Book",
                    "authors": ["Author One"],
                    "description": "A description",
                    "publisher": "Publisher",
                    "publishedDate": "2020",
                    "categories": ["Fiction"],
                    "language": "en",
                    "industryIdentifiers": [{ "type": "ISBN_13", "identifier": "123" }],
                    "imageLinks": { "thumbnail": "http://thumb" }
                  }
                }
              ]
            }
            """;

        FakeHttpMessageHandler? handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(json));
        GoogleBooksClient? sut = CreateSut(handler);

        IReadOnlyList<BookDto> result = await sut.SearchAsync("feminism", 10);

        Assert.Single(result);
        Assert.Equal("Test Book", result[0].Title);
        Assert.Equal("Author One", result[0].Authors[0]);
        Assert.Equal("GoogleBooks", result[0].Source);
        Assert.Equal("vol1", result[0].ExternalId);
        Assert.Equal("123", result[0].Isbns[0]);
        Assert.Equal("http://thumb", result[0].ThumbnailUrl);
    }

    [Fact]
    public async Task SearchAsync_skips_items_without_title()
    {
        const string json = """
            {
              "items": [
                { "id": "no-title", "volumeInfo": { "authors": ["Someone"] } },
                { "id": "ok", "volumeInfo": { "title": "Valid" } }
              ]
            }
            """;

        FakeHttpMessageHandler? handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(json));
        GoogleBooksClient? sut = CreateSut(handler);

        IReadOnlyList<BookDto> result = await sut.SearchAsync("q", 10);

        Assert.Single(result);
        Assert.Equal("Valid", result[0].Title);
    }

    [Fact]
    public async Task SearchAsync_returns_empty_when_google_returns_no_items()
    {
        FakeHttpMessageHandler? handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse("""{ "items": [] }"""));
        GoogleBooksClient? sut = CreateSut(handler);

        IReadOnlyList<BookDto> result = await sut.SearchAsync("q", 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_throws_when_google_returns_quota_error()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{ "error": { "code": 429, "message": "Quota exceeded" } }""")
            });
        GoogleBooksClient? sut = CreateSut(handler);

        InvalidOperationException? ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SearchAsync("science", 10));

        Assert.Contains("quota exceeded", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_appends_api_key_when_configured()
    {
        string? capturedUrl = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return FakeHttpMessageHandler.JsonResponse("""{ "items": [] }""");
        });
        GoogleBooksClient? sut = CreateSut(handler, apiKey: "secret-key");

        await sut.SearchAsync("science", 10);

        Assert.NotNull(capturedUrl);
        Assert.Contains("key=secret-key", capturedUrl);
    }

    [Fact]
    public async Task SearchAsync_clamps_max_results_to_google_limit()
    {
        string? capturedUrl = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return FakeHttpMessageHandler.JsonResponse("""{ "items": [] }""");
        });
        GoogleBooksClient? sut = CreateSut(handler);

        await sut.SearchAsync("q", 100);

        Assert.Contains("maxResults=40", capturedUrl);
    }
}
