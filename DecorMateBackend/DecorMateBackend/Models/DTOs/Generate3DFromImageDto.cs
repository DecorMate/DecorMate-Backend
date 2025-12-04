namespace DecorMateBackend.Models.DTOs
{
    public class Generate3DFromImageDto
    {
        public IFormFile Image { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
    }
}
