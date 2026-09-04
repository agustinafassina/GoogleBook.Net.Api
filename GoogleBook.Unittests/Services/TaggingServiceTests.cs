using Microsoft.Extensions.Logging.Abstractions;
using GoogleBook.Models.Dto;
using GoogleBook.Services.Implementations;

namespace GoogleBook.UnitTests.Services;

public class TaggingServiceTests
{
    private static TaggingService CreateSut() => new(NullLogger<TaggingService>.Instance);

    [Fact]
    public void InferTags_uses_google_books_categories()
    {
        var book = new BookDto
        {
            Title = "A history of science",
            Categories = new List<string> { "History", "Science" }
        };

        IReadOnlyList<string> tags = CreateSut().InferTags(book);

        Assert.Equal(2, tags.Count);
        Assert.Contains("History", tags);
        Assert.Contains("Science", tags);
    }

    [Fact]
    public void InferTags_trims_and_deduplicates_categories()
    {
        var book = new BookDto
        {
            Title = "Anthology",
            Categories = new List<string> { " Fiction ", "fiction", "Poetry", "" }
        };

        IReadOnlyList<string> tags = CreateSut().InferTags(book);

        Assert.Equal(2, tags.Count);
        Assert.Contains("Fiction", tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Poetry", tags);
    }

    [Fact]
    public void InferTags_returns_empty_when_there_are_no_categories()
    {
        var book = new BookDto { Title = "A book about gardening", Description = "How to grow tomatoes." };

        IReadOnlyList<string> tags = CreateSut().InferTags(book);

        Assert.Empty(tags);
    }

    [Fact]
    public void InferTags_throws_when_book_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().InferTags(null!));
    }
}
