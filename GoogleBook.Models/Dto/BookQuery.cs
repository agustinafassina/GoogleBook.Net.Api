using System.ComponentModel.DataAnnotations;

namespace GoogleBook.Models.Dto
{
    public class BookQuery
    {
        public const int MaxPageSize = 100;
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Tag { get; set; }
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, MaxPageSize)]
        public int PageSize { get; set; } = 20;
    }
}
