using DecorMateBackend.Models.Enums;

namespace DecorMateBackend.Models.DTOs
{
    public class VendorDto
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? CompanyName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Location { get; set; }
        public string? Category { get; set; }
        public bool IsSponsored { get; set; } = false; // paid plan
        public double AverageRating { get; set; }
        public int RatingsCount { get; set; }
    }
}
