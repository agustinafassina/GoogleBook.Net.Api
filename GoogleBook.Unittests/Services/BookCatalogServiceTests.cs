using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GoogleBook.Models.Dto;
using GoogleBook.Repository.Interfaces;
using GoogleBook.Services.Implementations;
using GoogleBook.Services.Interfaces;

namespace GoogleBook.UnitTests.Services;

public class BookCatalogServiceTests
{
    private readonly Mock<IBookRepository> _repo = new();
    private readonly Mock<IGoogleBooksClient> _google = new();
    private readonly Mock<IWikidataClient> _wikidata = new();
    private readonly Mock<ITaggingService> _tagging = new();

    private BookCatalogService CreateSut() => new(
        _repo.Object, _google.Object, _wikidata.Object, _tagging.Object, NullLogger<BookCatalogService>.Instance);

    [Fact]
    public async Task SearchGoogleBooksAsync_returns_google_results_without_persisting()
    {
        var candidate = new BookDto { Title = "Science book", Source = "GoogleBooks", ExternalId = "g1" };
        _google.Setup(c => c.SearchAsync("science", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });
        _tagging.Setup(t => t.InferTags(candidate)).Returns(new[] { "Science", "History" });

        IReadOnlyList<BookDto>? result = await CreateSut().SearchGoogleBooksAsync("science", 10);

        Assert.Single(result);
        Assert.Contains("Science", result[0].Tags);
        Assert.Contains("History", result[0].Tags);
        _repo.Verify(r => r.Add(It.IsAny<BookDto>()), Times.Never);
    }

    [Fact]
    public async Task SearchGoogleBooksAsync_skips_auto_tagging_when_disabled()
    {
        var candidate = new BookDto { Title = "Plain result", Source = "GoogleBooks", ExternalId = "g1" };
        _google.Setup(c => c.SearchAsync("q", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });

        IReadOnlyList<BookDto>? result = await CreateSut().SearchGoogleBooksAsync("q", 5, autoTag: false);

        Assert.Single(result);
        Assert.Empty(result[0].Tags);
        _tagging.Verify(t => t.InferTags(It.IsAny<BookDto>()), Times.Never);
    }

    [Fact]
    public async Task SearchGoogleBooksByTagAsync_uses_tag_query_and_marks_results_with_requested_tag()
    {
        var candidate = new BookDto { Title = "History result", Source = "GoogleBooks", ExternalId = "g1" };
        _google.Setup(c => c.SearchAsync("subject:history", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });
        _tagging.Setup(t => t.InferTags(candidate)).Returns(Array.Empty<string>());

        IReadOnlyList<BookDto>? result = await CreateSut().SearchGoogleBooksByTagAsync("history", 20);

        Assert.Single(result);
        Assert.Contains("history", result[0].Tags);
        _repo.Verify(r => r.Add(It.IsAny<BookDto>()), Times.Never);
    }

    [Fact]
    public async Task SearchGoogleBooksByTagAsync_tries_fallback_query_when_first_returns_empty()
    {
        var candidate = new BookDto { Title = "Fallback hit", Source = "GoogleBooks", ExternalId = "g2" };
        _google.Setup(c => c.SearchAsync("subject:history", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BookDto>());
        _google.Setup(c => c.SearchAsync("history books", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });
        _tagging.Setup(t => t.InferTags(candidate)).Returns(Array.Empty<string>());

        IReadOnlyList<BookDto>? result = await CreateSut().SearchGoogleBooksByTagAsync("history", 20);

        Assert.Single(result);
        Assert.Equal("Fallback hit", result[0].Title);
        _google.Verify(c => c.SearchAsync("subject:history", 20, It.IsAny<CancellationToken>()), Times.Once);
        _google.Verify(c => c.SearchAsync("history books", 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchGoogleBooksByTagAsync_returns_empty_when_tag_is_blank()
    {
        IReadOnlyList<BookDto> result = await CreateSut().SearchGoogleBooksByTagAsync("   ", 20);

        Assert.Empty(result);
        _google.Verify(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestFromGoogleBooksAsync_persists_new_books_with_manual_and_auto_tags()
    {
        var candidate = new BookDto { Title = "History book", Source = "GoogleBooks", ExternalId = "g1" };
        _google.Setup(c => c.SearchAsync("q", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });
        _repo.Setup(r => r.GetByExternalId("GoogleBooks", "g1")).Returns((BookDto?)null);
        _tagging.Setup(t => t.InferTags(It.IsAny<BookDto>())).Returns(new[] { "History" });
        _repo.Setup(r => r.Add(It.IsAny<BookDto>())).Returns((BookDto b) => { b.Id = 1; return b; });

        var request = new GoogleBooksIngestRequest
        {
            Query = "q",
            MaxResults = 5,
            Tags = new List<string> { "Biography" },
            AutoTag = true
        };

        IReadOnlyList<BookDto>? result = await CreateSut().IngestFromGoogleBooksAsync(request);

        Assert.Single(result);
        Assert.Contains("Biography", result[0].Tags);
        Assert.Contains("History", result[0].Tags);
        _repo.Verify(r => r.Add(It.IsAny<BookDto>()), Times.Once);
    }

    [Fact]
    public async Task IngestFromGoogleBooksAsync_skips_already_ingested_volumes()
    {
        var candidate = new BookDto { Title = "Dup", Source = "GoogleBooks", ExternalId = "g1" };
        _google.Setup(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });
        _repo.Setup(r => r.GetByExternalId("GoogleBooks", "g1"))
            .Returns(new BookDto { Id = 99, Title = "Dup", ExternalId = "g1" });

        IReadOnlyList<BookDto>? result = await CreateSut().IngestFromGoogleBooksAsync(new GoogleBooksIngestRequest { Query = "q" });

        Assert.Empty(result);
        _repo.Verify(r => r.Add(It.IsAny<BookDto>()), Times.Never);
    }

    [Fact]
    public async Task IngestFromGoogleBooksAsync_enriches_authors_when_requested()
    {
        var candidate = new BookDto
        {
            Title = "Book",
            Source = "GoogleBooks",
            ExternalId = "g1",
            Authors = new List<string> { "Virginia Woolf" }
        };
        _google.Setup(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookDto> { candidate });
        _repo.Setup(r => r.GetByExternalId(It.IsAny<string>(), It.IsAny<string>())).Returns((BookDto?)null);
        _wikidata.Setup(w => w.GetAuthorProfileAsync("Virginia Woolf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorDto { Name = "Virginia Woolf" });
        _tagging.Setup(t => t.InferTags(It.IsAny<BookDto>())).Returns(Array.Empty<string>());
        _repo.Setup(r => r.Add(It.IsAny<BookDto>())).Returns((BookDto b) => { b.Id = 1; return b; });

        var request = new GoogleBooksIngestRequest { Query = "q", EnrichAuthors = true, AutoTag = false };

        IReadOnlyList<BookDto>? result = await CreateSut().IngestFromGoogleBooksAsync(request);

        Assert.Single(result[0].AuthorProfiles);
        Assert.Equal("Virginia Woolf", result[0].AuthorProfiles[0].Name);
        _wikidata.Verify(w => w.GetAuthorProfileAsync("Virginia Woolf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrichAuthorsAsync_returns_null_when_book_missing()
    {
        _repo.Setup(r => r.GetById(123)).Returns((BookDto?)null);

        BookDto? result = await CreateSut().EnrichAuthorsAsync(123);

        Assert.Null(result);
        _wikidata.Verify(w => w.GetAuthorProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AddTags_merges_without_duplicates()
    {
        var book = new BookDto { Id = 1, Title = "B", Tags = { "History" } };
        _repo.Setup(r => r.GetById(1)).Returns(book);
        _repo.Setup(r => r.Update(It.IsAny<BookDto>())).Returns((BookDto b) => b);

        BookDto? result = CreateSut().AddTags(1, new[] { "history", "Fiction" });

        Assert.NotNull(result);
        Assert.Equal(2, result!.Tags.Count);
        Assert.Contains("Fiction", result.Tags);
    }

    [Fact]
    public void GetAvailableTags_returns_distinct_tags_from_catalog()
    {
        _repo.Setup(r => r.GetAll()).Returns(new[]
        {
            new BookDto { Title = "A", Tags = { "History", "Fiction" } },
            new BookDto { Title = "B", Tags = { "history", "Science" } }
        });

        IReadOnlyList<string> tags = CreateSut().GetAvailableTags();

        Assert.Equal(3, tags.Count);
        Assert.Contains("Fiction", tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("History", tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Science", tags, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetByAuthor_returns_empty_for_blank_author(string? author)
    {
        IReadOnlyList<BookDto> result = CreateSut().GetByAuthor(author!);

        Assert.Empty(result);
        _repo.Verify(r => r.GetByAuthor(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetByAuthor_trims_author_before_querying_repository()
    {
        _repo.Setup(r => r.GetByAuthor("Virginia Woolf")).Returns(Array.Empty<BookDto>());

        CreateSut().GetByAuthor("  Virginia Woolf  ");

        _repo.Verify(r => r.GetByAuthor("Virginia Woolf"), Times.Once);
    }

    [Fact]
    public void GetByTag_delegates_to_repository()
    {
        var books = new List<BookDto> { new() { Id = 1, Title = "Tagged", Tags = { "Science" } } };
        _repo.Setup(r => r.GetByTag("Science")).Returns(books);

        IReadOnlyList<BookDto> result = CreateSut().GetByTag("Science");

        Assert.Single(result);
        _repo.Verify(r => r.GetByTag("Science"), Times.Once);
    }

    [Fact]
    public void AddTags_returns_null_when_book_missing()
    {
        _repo.Setup(r => r.GetById(42)).Returns((BookDto?)null);

        BookDto? result = CreateSut().AddTags(42, new[] { "Fiction" });

        Assert.Null(result);
        _repo.Verify(r => r.Update(It.IsAny<BookDto>()), Times.Never);
    }

    [Fact]
    public async Task EnrichAuthorsAsync_enriches_existing_book_and_reapplies_auto_tags()
    {
        var book = new BookDto
        {
            Id = 7,
            Title = "Essays",
            Authors = new List<string> { "Virginia Woolf" }
        };
        _repo.Setup(r => r.GetById(7)).Returns(book);
        _wikidata.Setup(w => w.GetAuthorProfileAsync("Virginia Woolf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorDto { Name = "Virginia Woolf" });
        _tagging.Setup(t => t.InferTags(book)).Returns(new[] { "Essays" });
        _repo.Setup(r => r.Update(book)).Returns(book);

        BookDto? result = await CreateSut().EnrichAuthorsAsync(7);

        Assert.NotNull(result);
        Assert.Single(result!.AuthorProfiles);
        Assert.Contains("Essays", result.Tags);
        _repo.Verify(r => r.Update(book), Times.Once);
    }

    [Fact]
    public async Task IngestFromGoogleBooksAsync_throws_when_request_is_null()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().IngestFromGoogleBooksAsync(null!));
    }
}
