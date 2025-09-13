using DecorMateBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DecorMateBackend.Models.DTOs
{
        public class MvcRegisterViewModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = null!;

            [Required]
            [StringLength(100, MinimumLength = 6)]
            public string Password { get; set; } = null!;

            [Required]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = null!;

            [StringLength(100)]
            public string? FirstName { get; set; }

            [StringLength(100)]
            public string? LastName { get; set; }

            // خيارات إضافية للـ MVC (اختيارية)
            public bool IsCompany { get; set; } = true; // افتراضيًا للمواقع اللي بتسجل شركات
            public string? CompanyName { get; set; }
            public string? Location { get; set; }
             public ProfessionalCategory? ProfessionalCategory { get; set; }
        }
}
