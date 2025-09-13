using DecorMate_Backend_Web_app.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DecorMateBackend.Models
{
    public class VendorRating
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = null!; // the vendor being rated (FK to AspNetUsers)

        [Required]
        public string RatedByUserId { get; set; } = null!; // who rated (FK to AspNetUsers)

        [Range(1, 5)]
        public int Score { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // navigation (optional)
        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? Vendor { get; set; }
    }
}
