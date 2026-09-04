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
        BookDto Add(BookDto book);
        BookDto Update(BookDto book);
    }
}
