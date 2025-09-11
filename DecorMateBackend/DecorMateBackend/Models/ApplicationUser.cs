using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(6)]
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        public string? PasswordResetOtp { get; set; }
        public DateTime? PasswordResetOtpExpiry { get; set; }

        public string? ProfilePictureUrl { get; set; }

        public string? ProfilePicturePublicId { get; set; }
        public string? CompanyName { get; set; }

        public AuthProvider Provider { get; set; } = AuthProvider.Local;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DateOfBirth { get; set; }

        public ICollection<RefreshToken>? RefreshTokens { get; set; }
    }
}
