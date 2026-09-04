using Microsoft.Extensions.Logging;
using GoogleBook.Models.Dto;
using GoogleBook.Repository.Interfaces;
using GoogleBook.Services.Interfaces;

namespace GoogleBook.Services.Implementations
{
    public class BookCatalogService : IBookCatalogService
    {
        private readonly IBookRepository _bookRepository;
        private readonly IGoogleBooksClient _googleBooksClient;
        private readonly IWikidataClient _wikidataClient;
        private readonly ITaggingService _taggingService;
        private readonly ILogger<BookCatalogService> _logger;

        public BookCatalogService(
            IBookRepository bookRepository,
            IGoogleBooksClient googleBooksClient,
            IWikidataClient wikidataClient,
            ITaggingService taggingService,
            ILogger<BookCatalogService> logger)
        {
            _bookRepository = bookRepository;
            _googleBooksClient = googleBooksClient;
            _wikidataClient = wikidataClient;
            _taggingService = taggingService;
            _logger = logger;
        }

        public IReadOnlyList<BookDto> GetAll() => _bookRepository.GetAll().ToList();

        public BookDto? GetById(int id) => _bookRepository.GetById(id);

        public IReadOnlyList<BookDto> GetByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return Array.Empty<BookDto>();
            return _bookRepository.GetByTag(tag.Trim()).ToList();
        }

        public IReadOnlyList<BookDto> GetByAuthor(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return Array.Empty<BookDto>();
            return _bookRepository.GetByAuthor(author.Trim()).ToList();
        }

        public IReadOnlyList<string> GetAvailableTags() =>
            _bookRepository.GetAll()
                .SelectMany(book => book.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public async Task<IReadOnlyList<BookDto>> SearchGoogleBooksAsync(string query, int maxResults, bool autoTag = true, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<BookDto>? books = await _googleBooksClient.SearchAsync(query, maxResults, cancellationToken);
            if (!autoTag)
                return books;

            foreach (BookDto? book in books)
                MergeTags(book, _taggingService.InferTags(book));

            return books;
        }

        public async Task<IReadOnlyList<BookDto>> SearchGoogleBooksByTagAsync(string tag, int maxResults, bool autoTag = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return Array.Empty<BookDto>();

            string normalizedTag = tag.Trim();
            foreach (string query in GetGoogleBooksQueriesForTag(normalizedTag))
            {
                IReadOnlyList<BookDto>? books = await SearchGoogleBooksAsync(query, maxResults, autoTag, cancellationToken);
                if (books.Count == 0)
                {
                    _logger.LogInformation("Google Books returned no results for tag {Tag} with query '{Query}'", normalizedTag, query);
                    continue;
                }

                foreach (BookDto? book in books)
                    MergeTags(book, new[] { normalizedTag });

                _logger.LogInformation("Google Books tag search for {Tag} succeeded with query '{Query}' ({Count} results)", normalizedTag, query, books.Count);
                return books;
            }

            _logger.LogWarning("Google Books returned no results for tag {Tag} after trying all fallback queries", normalizedTag);
            return Array.Empty<BookDto>();
        }

        public async Task<IReadOnlyList<BookDto>> IngestFromGoogleBooksAsync(GoogleBooksIngestRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyList<BookDto>? candidates = await _googleBooksClient.SearchAsync(request.Query, request.MaxResults, cancellationToken);
            var ingested = new List<BookDto>();

            foreach (BookDto? candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BookDto? existing = candidate.ExternalId is not null
                    ? _bookRepository.GetByExternalId(candidate.Source, candidate.ExternalId)
                    : null;
                if (existing is not null)
                {
                    _logger.LogInformation("Skipping already ingested volume '{Title}' ({ExternalId})", candidate.Title, candidate.ExternalId);
                    continue;
                }

                MergeTags(candidate, request.Tags);

                if (request.EnrichAuthors)
                    await EnrichAuthorProfilesAsync(candidate, cancellationToken);

                if (request.AutoTag)
                    MergeTags(candidate, _taggingService.InferTags(candidate));

                BookDto? saved = _bookRepository.Add(candidate);
                ingested.Add(saved);
            }

            _logger.LogInformation("Ingested {Count} new books for query '{Query}'", ingested.Count, request.Query);
            return ingested;
        }

        public async Task<BookDto?> EnrichAuthorsAsync(int bookId, CancellationToken cancellationToken = default)
        {
            BookDto? book = _bookRepository.GetById(bookId);
            if (book is null)
            {
                _logger.LogWarning("Cannot enrich authors: book {BookId} not found", bookId);
                return null;
            }

            await EnrichAuthorProfilesAsync(book, cancellationToken);
            MergeTags(book, _taggingService.InferTags(book));
            return _bookRepository.Update(book);
        }

        public BookDto? AddTags(int bookId, IEnumerable<string> tags)
        {
            BookDto? book = _bookRepository.GetById(bookId);
            if (book is null)
            {
                _logger.LogWarning("Cannot add tags: book {BookId} not found", bookId);
                return null;
            }

            MergeTags(book, tags);
            return _bookRepository.Update(book);
        }

        private async Task EnrichAuthorProfilesAsync(BookDto book, CancellationToken cancellationToken)
        {
            foreach (string? authorName in book.Authors)
            {
                if (book.AuthorProfiles.Any(p => string.Equals(p.Name, authorName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                AuthorDto? profile = await _wikidataClient.GetAuthorProfileAsync(authorName, cancellationToken);
                if (profile is not null)
                    book.AuthorProfiles.Add(profile);
            }
        }

        private static void MergeTags(BookDto book, IEnumerable<string> tags)
        {
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                string normalized = tag.Trim();
                if (!book.Tags.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                    book.Tags.Add(normalized);
            }
        }

        private static IReadOnlyList<string> GetGoogleBooksQueriesForTag(string tag) =>
            new[] { $"subject:{tag}", $"{tag} books", tag };
    }
}
