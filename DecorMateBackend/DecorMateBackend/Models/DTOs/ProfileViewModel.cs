using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models.DTOs
{
    public class ProfileViewModel
    {
        [Display(Name = "User Id")]
        public string Id { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "First name")]
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last name")]
        [Required]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Profile picture URL")]
        [Url]
        public string? ProfilePictureUrl { get; set; }

        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Company name")]
        public string? CompanyName { get; set; }
    }
}
