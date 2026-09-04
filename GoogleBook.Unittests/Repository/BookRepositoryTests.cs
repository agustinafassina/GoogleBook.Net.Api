using GoogleBook.Models.Dto;
using GoogleBook.Repository.Implementations;

namespace GoogleBook.UnitTests.Repository;

public class BookRepositoryTests
{
    [Fact]
    public void Add_assigns_incremental_ids_starting_at_one()
    {
        var sut = new BookRepository();

        BookDto first = sut.Add(new BookDto { Title = "First" });
        BookDto second = sut.Add(new BookDto { Title = "Second" });

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
    }

    [Fact]
    public void GetByTag_returns_only_books_with_matching_tag()
    {
        var sut = new BookRepository();
        sut.Add(new BookDto { Title = "History", Tags = { "History" } });
        sut.Add(new BookDto { Title = "Fiction", Tags = { "Fiction" } });
        sut.Add(new BookDto { Title = "Both", Tags = { "History", "Fiction" } });

        List<BookDto> result = sut.GetByTag("history").ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Contains("History", b.Tags, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetByAuthor_matches_partial_name_case_insensitively()
    {
        var sut = new BookRepository();
        sut.Add(new BookDto { Title = "A", Authors = new List<string> { "Audre Lorde" } });
        sut.Add(new BookDto { Title = "B", Authors = new List<string> { "James Baldwin" } });

        List<BookDto> result = sut.GetByAuthor("audre").ToList();

        Assert.Single(result);
        Assert.Equal("A", result[0].Title);
    }

    [Fact]
    public void GetByExternalId_matches_source_and_id_case_insensitively()
    {
        var sut = new BookRepository();
        sut.Add(new BookDto { Title = "Stored", Source = "GoogleBooks", ExternalId = "ABC123" });

        BookDto? found = sut.GetByExternalId("googlebooks", "abc123");

        Assert.NotNull(found);
        Assert.Equal("Stored", found!.Title);
    }

    [Fact]
    public void Update_replaces_existing_book()
    {
        var sut = new BookRepository();
        BookDto book = sut.Add(new BookDto { Title = "Original", Tags = { "Fiction" } });
        book.Title = "Updated";

        BookDto updated = sut.Update(book);

        Assert.Equal("Updated", updated.Title);
        Assert.Equal("Updated", sut.GetById(book.Id)!.Title);
    }

    [Fact]
    public void Update_throws_when_book_not_found()
    {
        var sut = new BookRepository();

        Assert.Throws<KeyNotFoundException>(() => sut.Update(new BookDto { Id = 999, Title = "Missing" }));
    }
}
