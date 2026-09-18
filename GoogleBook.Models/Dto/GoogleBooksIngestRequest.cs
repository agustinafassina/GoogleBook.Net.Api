namespace GoogleBook.Models.Dto
{
    public class GoogleBooksIngestRequest
    {
        public required string Query { get; set; }
        public int MaxResults { get; set; } = 10;
        public List<string> Tags { get; set; } = new();
        public bool AutoTag { get; set; } = true;
        public bool EnrichAuthors { get; set; } = false;
    }
}
