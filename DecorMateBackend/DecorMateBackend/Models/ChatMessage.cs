using DecorMate_Backend_Web_app.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DecorMateBackend.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        public int ConversationId { get; set; }
        [ForeignKey("ConversationId")]
        public Conversation Conversation { get; set; } = null!;

        public string SenderId { get; set; } = string.Empty;
        [ForeignKey("SenderId")]
        public ApplicationUser Sender { get; set; } = null!;

        [Required]
        public string Content { get; set; } = string.Empty; // Encrypted content

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
    }
}
