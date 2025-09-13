namespace DecorMateBackend.Models.DTOs
{
    public class GeneratedImageDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = null!;
        public string? PublicId { get; set; }
        public string? Title { get; set; }
        public string Prompt { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
