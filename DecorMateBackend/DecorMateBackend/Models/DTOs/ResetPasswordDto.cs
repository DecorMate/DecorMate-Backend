using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models.DTOs
{
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Token { get; set; } = null!; 

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = null!;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
    public class UpdatePasswordDto
    {
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = null!;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
    public class ResetPasswordWithOtpDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be 6 characters")]
        public string Otp { get; set; } = null!;

        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        public string NewPassword { get; set; } = null!;
    }

    public class VerifyResetOtpDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be 6 characters")]
        public string Otp { get; set; } = null!;
    }

    public class SetNewPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        public string NewPassword { get; set; } = null!;
    }
}
