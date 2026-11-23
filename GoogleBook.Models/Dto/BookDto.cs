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

        /// <summary>Deep copy, so callers cannot mutate an instance held by the catalog store.</summary>
        public BookDto Clone() => new()
        {
            Id = Id,
            Title = Title,
            Authors = Authors.ToList(),
            Description = Description,
            Publisher = Publisher,
            PublishedDate = PublishedDate,
            Language = Language,
            ThumbnailUrl = ThumbnailUrl,
            Isbns = Isbns.ToList(),
            Categories = Categories.ToList(),
            Source = Source,
            ExternalId = ExternalId,
            Tags = Tags.ToList(),
            AuthorProfiles = AuthorProfiles.Select(profile => profile.Clone()).ToList()
        };
    }
}
