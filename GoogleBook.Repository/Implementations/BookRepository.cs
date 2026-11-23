using GoogleBook.Models.Dto;
using GoogleBook.Repository.Interfaces;

namespace GoogleBook.Repository.Implementations
{
    public class BookRepository : IBookRepository
    {
        private readonly List<BookDto> _books = new();
        private readonly object _lock = new();
        private int _lastId;

        public IEnumerable<BookDto> GetAll()
        {
            lock (_lock)
            {
                return _books.Select(b => b.Clone()).ToList();
            }
        }

        public BookDto? GetById(int id)
        {
            lock (_lock)
            {
                return _books.FirstOrDefault(b => b.Id == id)?.Clone();
            }
        }

        public BookDto? GetByExternalId(string source, string externalId)
        {
            lock (_lock)
            {
                return _books.FirstOrDefault(b =>
                    string.Equals(b.Source, source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.ExternalId, externalId, StringComparison.OrdinalIgnoreCase))?.Clone();
            }
        }

        public IEnumerable<BookDto> GetByTag(string tag) => Find(title: null, author: null, tag: tag);

        public IEnumerable<BookDto> GetByAuthor(string author) => Find(title: null, author: author, tag: null);

        public IEnumerable<BookDto> Find(string? title, string? author, string? tag)
        {
            lock (_lock)
            {
                IEnumerable<BookDto> query = _books;

                if (!string.IsNullOrWhiteSpace(title))
                    query = query.Where(b => b.Title.Contains(title, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(author))
                    query = query.Where(b => b.Authors.Any(a => a.Contains(author, StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrWhiteSpace(tag))
                    query = query.Where(b => b.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));

                return query.Select(b => b.Clone()).ToList();
            }
        }

        public BookDto Add(BookDto book)
        {
            ArgumentNullException.ThrowIfNull(book);
            lock (_lock)
            {
                // Ids are never reused, even after a delete.
                BookDto stored = book.Clone();
                stored.Id = ++_lastId;
                _books.Add(stored);
                return stored.Clone();
            }
        }

        public BookDto Update(BookDto book)
        {
            ArgumentNullException.ThrowIfNull(book);
            lock (_lock)
            {
                int index = _books.FindIndex(b => b.Id == book.Id);
                if (index < 0)
                    throw new KeyNotFoundException($"Book with id {book.Id} was not found.");
                _books[index] = book.Clone();
                return book.Clone();
            }
        }

        public BookDto? Update(int id, Action<BookDto> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);
            lock (_lock)
            {
                int index = _books.FindIndex(b => b.Id == id);
                if (index < 0)
                    return null;

                // Mutate a copy first so a throwing callback leaves the stored book untouched.
                BookDto updated = _books[index].Clone();
                mutate(updated);
                updated.Id = id;
                _books[index] = updated;
                return updated.Clone();
            }
        }

        public bool Remove(int id)
        {
            lock (_lock)
            {
                return _books.RemoveAll(b => b.Id == id) > 0;
            }
        }
    }
}
