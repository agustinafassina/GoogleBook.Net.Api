namespace GoogleBook.Models.Dto
{
    public class AuthorDto
    {
        public required string Name { get; set; }
        public string? WikidataId { get; set; }
        public string? Gender { get; set; }
        public string? SexualOrientation { get; set; }
        public string? CountryOfCitizenship { get; set; }
        public string? Description { get; set; }
        public IReadOnlyList<string> Occupations { get; set; } = new List<string>();
    }
}
