namespace DecorMateBackend.Models.DTOs
{
    public class GenerateWithFileRequestDto
    {
        // IFormFile will be bound from multipart form-data (key name "file")
        public IFormFile? File { get; set; }

        // textual prompt/description (form-data key "prompt")
        public string? Prompt { get; set; }

        // optional title / project name (form-data key "title")
        public string? Title { get; set; }
    }
}
