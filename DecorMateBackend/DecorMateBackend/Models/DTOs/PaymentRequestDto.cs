using System.ComponentModel.DataAnnotations;

namespace DecorMateBackend.Models.DTOs
{
    public class PaymentRequestDto
    {
        [Required]
        public string Plan { get; set; } = null!; // Standard | Premium

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = null!;

        // optional field to show on UI
        public string? Notes { get; set; }
    }
}
