using GoogleBook.Models.Dto;

namespace GoogleBook.Services.Interfaces
{
    public interface ITaggingService
    {
        IReadOnlyList<string> InferTags(BookDto book);
    }
}
