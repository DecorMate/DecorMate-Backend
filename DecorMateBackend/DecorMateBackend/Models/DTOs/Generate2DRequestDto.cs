namespace DecorMateBackend.Models.DTOs
{
    public class Generate2DRequestDto
    {
        public string Prompt { get; set; } = string.Empty;
        public IFormFile? Image { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
