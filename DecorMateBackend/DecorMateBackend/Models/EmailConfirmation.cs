using DecorMate_Backend_Web_app.Models;

namespace DecorMateBackend.Models
{
    public class EmailConfirmation
    {
        public int Id { get; set; }
        public Guid ConfirmationGuid { get; set; } = Guid.NewGuid();
        public string Token { get; set; } = null!; // The actual Identity confirmation token
        public string ApplicationUserId { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } // Typically 24 hours
        public bool IsUsed { get; set; } = false;
        public DateTime? UsedAt { get; set; }

        public ApplicationUser? ApplicationUser { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsValid => !IsUsed && !IsExpired;
    }
}

