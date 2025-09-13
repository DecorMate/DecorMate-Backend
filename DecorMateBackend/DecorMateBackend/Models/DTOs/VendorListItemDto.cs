using DecorMateBackend.Models.Enums;

namespace DecorMateBackend.Models.DTOs
{
    public class VendorListItemDto
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? CompanyName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public ProfessionalCategory? ProfessionalCategory { get; set; }
        public string? Location { get; set; }
        public double Rating { get; set; }
        public bool IsSponsored { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
