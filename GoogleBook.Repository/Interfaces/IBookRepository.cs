using GoogleBook.Models.Dto;

namespace GoogleBook.Repository.Interfaces
{
    public interface IBookRepository
    {
        IEnumerable<BookDto> GetAll();
        BookDto? GetById(int id);
        BookDto? GetByExternalId(string source, string externalId);
        IEnumerable<BookDto> GetByTag(string tag);
        IEnumerable<BookDto> GetByAuthor(string author);
        IEnumerable<BookDto> Find(string? title, string? author, string? tag);
        BookDto Add(BookDto book);
        BookDto Update(BookDto book);
        BookDto? Update(int id, Action<BookDto> mutate);
        bool Remove(int id);
    }
}
