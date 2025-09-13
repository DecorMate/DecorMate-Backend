using DecorMateBackend.Models.Enums;

namespace DecorMateBackend.Models.DTOs
{
    public class VendorFilterDto
    {
        public string? Location { get; set; }
        public ProfessionalCategory? Category { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
