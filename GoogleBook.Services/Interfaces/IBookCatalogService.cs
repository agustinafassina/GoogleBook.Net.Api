using GoogleBook.Models.Dto;

namespace GoogleBook.Services.Interfaces
{
    public interface IBookCatalogService
    {
        IReadOnlyList<BookDto> GetAll();
        BookDto? GetById(int id);
        IReadOnlyList<BookDto> GetByTag(string tag);
        IReadOnlyList<BookDto> GetByAuthor(string author);
        IReadOnlyList<string> GetAvailableTags();
        Task<IReadOnlyList<BookDto>> SearchGoogleBooksAsync(string query, int maxResults, bool autoTag = true, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BookDto>> SearchGoogleBooksByTagAsync(string tag, int maxResults, bool autoTag = true, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BookDto>> IngestFromGoogleBooksAsync(GoogleBooksIngestRequest request, CancellationToken cancellationToken = default);
        Task<BookDto?> EnrichAuthorsAsync(int bookId, CancellationToken cancellationToken = default);
        BookDto? AddTags(int bookId, IEnumerable<string> tags);
    }
}
