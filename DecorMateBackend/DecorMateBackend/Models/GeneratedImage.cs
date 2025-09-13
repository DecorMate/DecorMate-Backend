// Models/ImageGenerationRecord.cs
using System;

namespace DecorMateBackend.Models
{
    public class GeneratedImage
    {
        public int Id { get; set; }
        public string ApplicationUserId { get; set; } = null!;
        public string ProjectTitle { get; set; } = null!;
        public string Prompt { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public string? CloudinaryPublicId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
