using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models.DTOs
{
    public class ExternalAuthDto
    {
        [Required]
        public string Provider { get; set; } = null!;

        [Required]
        public string IdToken { get; set; } = null!; 

        public string? DeviceInfo { get; set; }
    }
}
