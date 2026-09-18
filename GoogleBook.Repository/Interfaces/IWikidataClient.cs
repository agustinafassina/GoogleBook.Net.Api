using GoogleBook.Models.Dto;

namespace GoogleBook.Repository.Interfaces
{
    public interface IWikidataClient
    {
        Task<AuthorDto?> GetAuthorProfileAsync(string authorName, CancellationToken cancellationToken = default);
    }
}