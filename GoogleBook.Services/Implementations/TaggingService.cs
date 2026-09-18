using Microsoft.Extensions.Logging;
using GoogleBook.Models.Dto;
using GoogleBook.Services.Interfaces;

namespace GoogleBook.Services.Implementations
{
    public class TaggingService : ITaggingService
    {
        private readonly ILogger<TaggingService> _logger;

        public TaggingService(ILogger<TaggingService> logger)
        {
            _logger = logger;
        }

        public IReadOnlyList<string> InferTags(BookDto book)
        {
            ArgumentNullException.ThrowIfNull(book);

            List<string> tags = book.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogDebug("Inferred {Count} tags for book '{Title}'", tags.Count, book.Title);
            return tags;
        }
    }
}
