namespace DecorMateBackend.Models.DTOs
{
    public class AiGenerateDto
    {
        public string Description { get; set; } = "";
        public string? Title { get; set; }   // اختياري
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
