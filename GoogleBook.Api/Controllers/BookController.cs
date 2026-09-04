using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using GoogleBook.Api.Security;
using GoogleBook.Models.Dto;
using GoogleBook.Services.Interfaces;
using FluentValidation.Results;

namespace GoogleBook.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [ApiKeyAuthorize]
    public class BookController : ControllerBase
    {
        private readonly IBookCatalogService _bookCatalogService;
        private readonly IValidator<GoogleBooksIngestRequest> _ingestValidator;
        private readonly ILogger<BookController> _logger;

        public BookController(
            IBookCatalogService bookCatalogService,
            IValidator<GoogleBooksIngestRequest> ingestValidator,
            ILogger<BookController> logger)
        {
            _bookCatalogService = bookCatalogService;
            _ingestValidator = ingestValidator;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetAll([FromQuery] string? author = null)
        {
            _logger.LogInformation("GetAll books endpoint called (author filter: {Author})", author ?? "none");
            IReadOnlyList<BookDto>? books = string.IsNullOrWhiteSpace(author)
                ? _bookCatalogService.GetAll()
                : _bookCatalogService.GetByAuthor(author);
            return Ok(books);
        }

        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            _logger.LogInformation("GetById book endpoint called with id: {BookId}", id);
            BookDto? book = _bookCatalogService.GetById(id);
            if (book is null)
            {
                _logger.LogWarning("Book with id {BookId} not found", id);
                return NotFound();
            }
            return Ok(book);
        }

        [HttpGet("tags")]
        public IActionResult GetTags()
        {
            return Ok(_bookCatalogService.GetAvailableTags());
        }

        [HttpGet("by-tag/{tag}")]
        public IActionResult GetByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return BadRequest("Tag is required.");

            _logger.LogInformation("GetByTag book endpoint called with tag: {Tag}", tag);
            return Ok(_bookCatalogService.GetByTag(tag));
        }

        [HttpGet("google/search")]
        public async Task<IActionResult> SearchGoogle(
            [FromQuery] string query,
            [FromQuery] int maxResults = 20,
            [FromQuery] bool autoTag = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query is required.");

            _logger.LogInformation("Searching Google Books with query: {Query}", query);
            IReadOnlyList<BookDto>? books = await _bookCatalogService.SearchGoogleBooksAsync(query, maxResults, autoTag, cancellationToken);
            return Ok(new { count = books.Count, books });
        }

        [HttpGet("google/by-tag/{tag}")]
        public async Task<IActionResult> SearchGoogleByTag(
            string tag,
            [FromQuery] int maxResults = 20,
            [FromQuery] bool autoTag = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return BadRequest("Tag is required.");

            _logger.LogInformation("Searching Google Books by tag: {Tag}", tag);
            IReadOnlyList<BookDto>? books = await _bookCatalogService.SearchGoogleBooksByTagAsync(tag, maxResults, autoTag, cancellationToken);
            return Ok(new { count = books.Count, books });
        }

        [HttpPost("ingest/google")]
        public async Task<IActionResult> IngestFromGoogle([FromBody] GoogleBooksIngestRequest request, CancellationToken cancellationToken = default)
        {
            ValidationResult? validation = await _ingestValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Ingest validation failed: {Errors}", string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
            }

            _logger.LogInformation("Ingesting from Google Books with query: {Query}", request.Query);
            IReadOnlyList<BookDto>? ingested = await _bookCatalogService.IngestFromGoogleBooksAsync(request, cancellationToken);
            return Ok(new { count = ingested.Count, books = ingested });
        }

        [HttpPost("{id:int}/enrich")]
        public async Task<IActionResult> EnrichAuthors(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Enrich authors endpoint called for book: {BookId}", id);
            BookDto? book = await _bookCatalogService.EnrichAuthorsAsync(id, cancellationToken);
            if (book is null)
            {
                _logger.LogWarning("Book with id {BookId} not found for enrichment", id);
                return NotFound();
            }
            return Ok(book);
        }

        [HttpPost("{id:int}/tags")]
        public IActionResult AddTags(int id, [FromBody] AddTagsRequest request)
        {
            if (request?.Tags is null || request.Tags.Count == 0)
                return BadRequest("At least one tag is required.");

            _logger.LogInformation("AddTags endpoint called for book: {BookId} with {Count} tags", id, request.Tags.Count);
            BookDto? book = _bookCatalogService.AddTags(id, request.Tags);
            if (book is null)
            {
                _logger.LogWarning("Book with id {BookId} not found for tagging", id);
                return NotFound();
            }
            return Ok(book);
        }
    }
}