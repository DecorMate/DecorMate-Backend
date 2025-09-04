using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models.DTOs
{
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;

        public bool RememberMe { get; set; } = false;
    }
}
