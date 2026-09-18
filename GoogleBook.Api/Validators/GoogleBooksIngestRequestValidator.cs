using FluentValidation;
using GoogleBook.Models.Dto;

namespace GoogleBook.Api.Validators
{
    public class GoogleBooksIngestRequestValidator : AbstractValidator<GoogleBooksIngestRequest>
    {
        public GoogleBooksIngestRequestValidator()
        {
            RuleFor(x => x.Query)
                .NotEmpty().WithMessage("Query is required.")
                .MaximumLength(500).WithMessage("Query must not exceed 500 characters.");

            RuleFor(x => x.MaxResults)
                .InclusiveBetween(1, 40).WithMessage("MaxResults must be between 1 and 40 (Google Books limit).");
        }
    }
}
