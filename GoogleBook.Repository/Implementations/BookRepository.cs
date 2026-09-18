using GoogleBook.Models.Dto;
using GoogleBook.Repository.Interfaces;

namespace GoogleBook.Repository.Implementations
{
    public class BookRepository : IBookRepository
    {
        private readonly List<BookDto> _books = new();
        private readonly object _lock = new();

        public IEnumerable<BookDto> GetAll()
        {
            lock (_lock)
            {
                return _books.ToList();
            }
        }

        public BookDto? GetById(int id)
        {
            lock (_lock)
            {
                return _books.FirstOrDefault(b => b.Id == id);
            }
        }

        public BookDto? GetByExternalId(string source, string externalId)
        {
            lock (_lock)
            {
                return _books.FirstOrDefault(b =>
                    string.Equals(b.Source, source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public IEnumerable<BookDto> GetByTag(string tag)
        {
            lock (_lock)
            {
                return _books
                    .Where(b => b.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        public IEnumerable<BookDto> GetByAuthor(string author)
        {
            lock (_lock)
            {
                return _books
                    .Where(b => b.Authors.Any(a => a.Contains(author, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        public BookDto Add(BookDto book)
        {
            lock (_lock)
            {
                book.Id = _books.Count != 0 ? _books.Max(b => b.Id) + 1 : 1;
                _books.Add(book);
                return book;
            }
        }

        public BookDto Update(BookDto book)
        {
            lock (_lock)
            {
                int index = _books.FindIndex(b => b.Id == book.Id);
                if (index < 0)
                    throw new KeyNotFoundException($"Book with id {book.Id} was not found.");
                _books[index] = book;
                return book;
            }
        }
    }
}
