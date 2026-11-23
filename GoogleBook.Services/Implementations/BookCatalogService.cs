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

        public PagedResult<BookDto> Search(BookQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            int page = Math.Max(1, query.Page);
            int pageSize = Math.Clamp(query.PageSize, 1, BookQuery.MaxPageSize);

            List<BookDto> matches = _bookRepository
                .Find(query.Title?.Trim(), query.Author?.Trim(), query.Tag?.Trim())
                .OrderBy(book => book.Id)
                .ToList();

            return new PagedResult<BookDto>
            {
                Items = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = matches.Count
            };
        }

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
            BookDto? snapshot = _bookRepository.GetById(bookId);
            if (snapshot is null)
            {
                _logger.LogWarning("Cannot enrich authors: book {BookId} not found", bookId);
                return null;
            }

            // Call Wikidata on a copy (slow, outside the store lock), then merge the result atomically.
            await EnrichAuthorProfilesAsync(snapshot, cancellationToken);

            BookDto? updated = _bookRepository.Update(bookId, stored =>
            {
                foreach (AuthorDto profile in snapshot.AuthorProfiles)
                {
                    if (!stored.AuthorProfiles.Any(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
                        stored.AuthorProfiles.Add(profile);
                }
                MergeTags(stored, _taggingService.InferTags(stored));
            });

            if (updated is null)
                _logger.LogWarning("Book {BookId} was deleted while its authors were being enriched", bookId);
            return updated;
        }

        public BookDto? AddTags(int bookId, IEnumerable<string> tags)
        {
            ArgumentNullException.ThrowIfNull(tags);
            List<string> tagList = tags.ToList();

            BookDto? updated = _bookRepository.Update(bookId, book => MergeTags(book, tagList));
            if (updated is null)
                _logger.LogWarning("Cannot add tags: book {BookId} not found", bookId);
            return updated;
        }

        public BookDto? RemoveTag(int bookId, string tag)
        {
            string normalized = tag?.Trim() ?? string.Empty;

            BookDto? updated = _bookRepository.Update(bookId, book =>
                book.Tags.RemoveAll(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)));
            if (updated is null)
                _logger.LogWarning("Cannot remove tag: book {BookId} not found", bookId);
            return updated;
        }

        public bool Delete(int bookId)
        {
            bool removed = _bookRepository.Remove(bookId);
            if (removed)
                _logger.LogInformation("Deleted book {BookId}", bookId);
            else
                _logger.LogWarning("Cannot delete: book {BookId} not found", bookId);
            return removed;
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
