using DecorMate_Backend_Web_app.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DecorMateBackend.Models
{
    public class ConversationParticipant
    {
        [Key]
        public int Id { get; set; }

        public int ConversationId { get; set; }
        [ForeignKey("ConversationId")]
        public Conversation Conversation { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;
    }
}
