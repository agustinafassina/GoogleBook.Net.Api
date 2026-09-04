using GoogleBook.Models.Dto;

namespace GoogleBook.Repository.Interfaces
{
    public interface IGoogleBooksClient
    {
        Task<IReadOnlyList<BookDto>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);
    }
}