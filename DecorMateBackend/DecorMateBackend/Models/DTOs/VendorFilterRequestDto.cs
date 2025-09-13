namespace DecorMateBackend.Models.DTOs
{
    public class VendorFilterRequestDto
    {
        public string? Location { get; set; }        // e.g. neighborhood/city
        public string? Category { get; set; }        // ProfessionalCategory string or enum name
        public string? Search { get; set; }          // name/company search
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
