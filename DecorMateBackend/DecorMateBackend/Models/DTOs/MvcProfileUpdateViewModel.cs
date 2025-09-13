using DecorMateBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DecorMateBackend.Models.DTOs
{
    public class MvcProfileUpdateViewModel
    {
        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [StringLength(200)]
        public string? CompanyName { get; set; }

        public IFormFile? ProfileImage { get; set; }

        // اختياري: عرض الحقول الجديدة (category, location) إن أردت
        public string? Location { get; set; }
        public ProfessionalCategory? ProfessionalCategory { get; set; } // أو استخدم enum لو موجود
    }
}
