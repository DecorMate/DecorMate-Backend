namespace DecorMateBackend.Models.DTOs
{
    public class GenerateImageRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
    }
}
