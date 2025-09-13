using System.ComponentModel.DataAnnotations;

namespace DecorMateBackend.Models.DTOs
{
    public class RateVendorDto
    {
        [Range(1, 5)]
        public int Score { get; set; }

        public string? Comment { get; set; }
    }
}
