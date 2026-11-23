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

    [Fact]
    public void Returned_books_are_copies_so_callers_cannot_mutate_the_store()
    {
        var sut = new BookRepository();
        BookDto added = sut.Add(new BookDto { Title = "Stored", Tags = { "History" } });

        BookDto copy = sut.GetById(added.Id)!;
        copy.Tags.Add("Mutated");
        added.Tags.Add("AlsoMutated");

        Assert.Equal(new[] { "History" }, sut.GetById(added.Id)!.Tags);
    }

    [Fact]
    public void Atomic_update_applies_mutation_and_returns_null_for_missing_book()
    {
        var sut = new BookRepository();
        BookDto added = sut.Add(new BookDto { Title = "Stored" });

        BookDto? updated = sut.Update(added.Id, b => b.Tags.Add("Fiction"));
        BookDto? missing = sut.Update(999, b => b.Tags.Add("Fiction"));

        Assert.Equal(new[] { "Fiction" }, updated!.Tags);
        Assert.Equal(new[] { "Fiction" }, sut.GetById(added.Id)!.Tags);
        Assert.Null(missing);
    }

    [Fact]
    public void Atomic_update_leaves_book_untouched_when_mutation_throws()
    {
        var sut = new BookRepository();
        BookDto added = sut.Add(new BookDto { Title = "Stored" });

        Assert.Throws<InvalidOperationException>(() => sut.Update(added.Id, b =>
        {
            b.Tags.Add("Partial");
            throw new InvalidOperationException();
        }));

        Assert.Empty(sut.GetById(added.Id)!.Tags);
    }

    [Fact]
    public void Concurrent_atomic_updates_do_not_lose_tags()
    {
        var sut = new BookRepository();
        BookDto added = sut.Add(new BookDto { Title = "Busy" });

        Parallel.For(0, 200, i => sut.Update(added.Id, b => b.Tags.Add("tag-" + i)));

        Assert.Equal(200, sut.GetById(added.Id)!.Tags.Count);
    }

    [Fact]
    public void Remove_deletes_book_and_ids_are_not_reused()
    {
        var sut = new BookRepository();
        sut.Add(new BookDto { Title = "One" });
        BookDto two = sut.Add(new BookDto { Title = "Two" });

        Assert.True(sut.Remove(two.Id));
        Assert.False(sut.Remove(two.Id));
        Assert.Null(sut.GetById(two.Id));
        Assert.Equal(3, sut.Add(new BookDto { Title = "Three" }).Id);
    }

    [Fact]
    public void Find_combines_title_author_and_tag_filters()
    {
        var sut = new BookRepository();
        sut.Add(new BookDto { Title = "Mrs Dalloway", Authors = new List<string> { "Virginia Woolf" }, Tags = { "Classics" } });
        sut.Add(new BookDto { Title = "Orlando", Authors = new List<string> { "Virginia Woolf" }, Tags = { "Classics" } });
        sut.Add(new BookDto { Title = "Mrs Something", Authors = new List<string> { "Someone Else" }, Tags = { "Classics" } });

        List<BookDto> result = sut.Find(title: "mrs", author: "woolf", tag: "classics").ToList();

        Assert.Single(result);
        Assert.Equal("Mrs Dalloway", result[0].Title);
    }
}
