namespace GoogleBook.Models.Dto
{
    public class BookDto
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public IReadOnlyList<string> Authors { get; set; } = new List<string>();
        public string? Description { get; set; }
        public string? Publisher { get; set; }
        public string? PublishedDate { get; set; }
        public string? Language { get; set; }
        public string? ThumbnailUrl { get; set; }
        public IReadOnlyList<string> Isbns { get; set; } = new List<string>();
        public IReadOnlyList<string> Categories { get; set; } = new List<string>();
        public string Source { get; set; } = "GoogleBooks";
        public string? ExternalId { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<AuthorDto> AuthorProfiles { get; set; } = new();
    }
}
