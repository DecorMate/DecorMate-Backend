using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace DecorMate_Backend_Web_app.Models
{
    public class PaymentRecord
    {
        [Key]
        public int Id { get; set; }

        public string ApplicationUserId { get; set; } = null!;

        [Required]
        public string Plan { get; set; } = null!; // "Standard" or "Premium"

        [Required]
        [Precision(18, 4)]
        public decimal Amount { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = null!; // Vodafone Cash phone

        public string? TransactionId { get; set; }

        public string Status { get; set; } = "Pending"; // Pending / Completed / Failed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}
