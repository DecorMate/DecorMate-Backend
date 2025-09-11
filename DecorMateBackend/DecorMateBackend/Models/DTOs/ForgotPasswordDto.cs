using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models.DTOs
{
    public class ForgotPasswordDto
    {
        public string Email { get; set; } = null!;
    }
}
