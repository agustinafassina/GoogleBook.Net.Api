using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using GoogleBook.Models.Dto;
using GoogleBook.Repository.Implementations;
using GoogleBook.UnitTests.TestHelpers;

namespace GoogleBook.UnitTests.Repository;

public class WikidataClientTests
{
    private static WikidataClient CreateSut(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://query.wikidata.org/") }, NullLogger<WikidataClient>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAuthorProfileAsync_returns_null_for_blank_name(string? authorName)
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called"));
        WikidataClient? sut = CreateSut(handler);

        AuthorDto? result = await sut.GetAuthorProfileAsync(authorName!);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorProfileAsync_maps_sparql_bindings_to_author_dto()
    {
        const string json = """
            {
              "results": {
                "bindings": [
                  {
                    "person": { "value": "http://www.wikidata.org/entity/Q123" },
                    "personLabel": { "value": "Audre Lorde" },
                    "genderLabel": { "value": "female" },
                    "orientationLabel": { "value": "lesbian" },
                    "countryLabel": { "value": "United States" },
                    "description": { "value": "American writer" },
                    "occupations": { "value": "writer; poet" }
                  }
                ]
              }
            }
            """;

        FakeHttpMessageHandler? handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(json));
        WikidataClient? sut = CreateSut(handler);

        AuthorDto? result = await sut.GetAuthorProfileAsync("Audre Lorde");

        Assert.NotNull(result);
        Assert.Equal("Audre Lorde", result!.Name);
        Assert.Equal("Q123", result.WikidataId);
        Assert.Equal("female", result.Gender);
        Assert.Equal("lesbian", result.SexualOrientation);
        Assert.Equal("United States", result.CountryOfCitizenship);
        Assert.Equal("American writer", result.Description);
        Assert.Equal(new[] { "writer", "poet" }, result.Occupations);
    }

    [Fact]
    public async Task GetAuthorProfileAsync_returns_null_when_no_bindings()
    {
        FakeHttpMessageHandler? handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse("""{ "results": { "bindings": [] } }"""));
        WikidataClient? sut = CreateSut(handler);

        AuthorDto? result = await sut.GetAuthorProfileAsync("Unknown Author");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorProfileAsync_returns_null_when_http_request_fails()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        WikidataClient? sut = CreateSut(handler);

        AuthorDto? result = await sut.GetAuthorProfileAsync("Audre Lorde");

        Assert.Null(result);
    }
}
