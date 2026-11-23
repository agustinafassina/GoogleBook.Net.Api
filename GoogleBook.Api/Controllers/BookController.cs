using System.ComponentModel.DataAnnotations;
using FluentValidation;
using ValidationResult = FluentValidation.Results.ValidationResult;
using Microsoft.AspNetCore.Mvc;
using GoogleBook.Api.Security;
using GoogleBook.Models.Dto;
using GoogleBook.Services.Interfaces;

namespace GoogleBook.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [ApiKeyAuthorize]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class BookController : ControllerBase
    {
        private const int GoogleMaxResults = 40;
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
        [ProducesResponseType(typeof(PagedResult<BookDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<PagedResult<BookDto>> GetAll([FromQuery] BookQuery query)
        {
            _logger.LogInformation(
                "GetAll books endpoint called (title: {Title}, author: {Author}, tag: {Tag}, page: {Page}, pageSize: {PageSize})",
                query.Title ?? "none", query.Author ?? "none", query.Tag ?? "none", query.Page, query.PageSize);
            return Ok(_bookCatalogService.Search(query));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(BookDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<BookDto> GetById(int id)
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

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Delete(int id)
        {
            _logger.LogInformation("Delete book endpoint called with id: {BookId}", id);
            return _bookCatalogService.Delete(id) ? NoContent() : NotFound();
        }

        [HttpGet("tags")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<string>> GetTags()
        {
            return Ok(_bookCatalogService.GetAvailableTags());
        }

        [HttpGet("by-tag/{tag}")]
        [ProducesResponseType(typeof(IReadOnlyList<BookDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<IReadOnlyList<BookDto>> GetByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return BadRequest("Tag is required.");

            _logger.LogInformation("GetByTag book endpoint called with tag: {Tag}", tag);
            return Ok(_bookCatalogService.GetByTag(tag));
        }

        [HttpGet("google/search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchGoogle(
            [FromQuery] string query,
            [FromQuery, Range(1, GoogleMaxResults)] int maxResults = 20,
            [FromQuery] bool autoTag = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query is required.");

            _logger.LogInformation("Searching Google Books with query: {Query}", query);
            IReadOnlyList<BookDto> books = await _bookCatalogService.SearchGoogleBooksAsync(query, maxResults, autoTag, cancellationToken);
            return Ok(new { count = books.Count, books });
        }

        [HttpGet("google/by-tag/{tag}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchGoogleByTag(
            string tag,
            [FromQuery, Range(1, GoogleMaxResults)] int maxResults = 20,
            [FromQuery] bool autoTag = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return BadRequest("Tag is required.");

            _logger.LogInformation("Searching Google Books by tag: {Tag}", tag);
            IReadOnlyList<BookDto> books = await _bookCatalogService.SearchGoogleBooksByTagAsync(tag, maxResults, autoTag, cancellationToken);
            return Ok(new { count = books.Count, books });
        }

        [HttpPost("ingest/google")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> IngestFromGoogle([FromBody] GoogleBooksIngestRequest request, CancellationToken cancellationToken = default)
        {
            ValidationResult validation = await _ingestValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Ingest validation failed: {Errors}", string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
            }

            _logger.LogInformation("Ingesting from Google Books with query: {Query}", request.Query);
            IReadOnlyList<BookDto> ingested = await _bookCatalogService.IngestFromGoogleBooksAsync(request, cancellationToken);
            return Ok(new { count = ingested.Count, books = ingested });
        }

        [HttpPost("{id:int}/enrich")]
        [ProducesResponseType(typeof(BookDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookDto>> EnrichAuthors(int id, CancellationToken cancellationToken = default)
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
        [ProducesResponseType(typeof(BookDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<BookDto> AddTags(int id, [FromBody] AddTagsRequest request)
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

        [HttpDelete("{id:int}/tags/{tag}")]
        [ProducesResponseType(typeof(BookDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<BookDto> RemoveTag(int id, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return BadRequest("Tag is required.");

            _logger.LogInformation("RemoveTag endpoint called for book: {BookId} with tag: {Tag}", id, tag);
            BookDto? book = _bookCatalogService.RemoveTag(id, tag);
            if (book is null)
            {
                _logger.LogWarning("Book with id {BookId} not found for tag removal", id);
                return NotFound();
            }
            return Ok(book);
        }
    }
}