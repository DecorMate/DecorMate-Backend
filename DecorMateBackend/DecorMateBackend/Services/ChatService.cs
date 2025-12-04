using DecorMate_Backend_Web_app.Data;
using DecorMateBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace DecorMateBackend.Services
{
    public class ChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;

        public ChatService(ApplicationDbContext context, EncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task<ChatMessage> SaveMessageAsync(string senderId, string receiverId, string content)
        {
            // Find or create conversation
            var conversation = await GetOrCreateConversationAsync(senderId, receiverId);

            // Encrypt content
            var encryptedContent = _encryptionService.Encrypt(content);

            var message = new ChatMessage
            {
                ConversationId = conversation.Id,
                SenderId = senderId,
                Content = encryptedContent,
                Timestamp = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);
            conversation.LastMessageAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            // Return message with DECRYPTED content for the caller to use
            message.Content = content; 
            return message;
        }

        public async Task<List<object>> GetConversationHistoryAsync(string userId, string otherUserId)
        {
            // Optimized query: Find conversation ID where both users are participants
            var conversationId = await _context.ConversationParticipants
                .Where(cp => cp.UserId == userId)
                .Select(cp => cp.ConversationId)
                .Intersect(
                    _context.ConversationParticipants
                    .Where(cp => cp.UserId == otherUserId)
                    .Select(cp => cp.ConversationId)
                )
                .FirstOrDefaultAsync();

            if (conversationId == 0) return new List<object>();

            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return messages.Select(m => 
            {
                string decryptedContent;
                try
                {
                    decryptedContent = _encryptionService.Decrypt(m.Content);
                }
                catch
                {
                    decryptedContent = m.Content;
                }

                return new
                {
                    m.Id,
                    m.SenderId,
                    Content = decryptedContent,
                    m.Timestamp,
                    m.ConversationId
                };
            }).ToList<object>();
        }

        public string DecryptMessage(string encryptedContent)
        {
            return _encryptionService.Decrypt(encryptedContent);
        }

        private async Task<Conversation> GetOrCreateConversationAsync(string user1Id, string user2Id)
        {
            // Check if conversation exists
            // We need a conversation that has BOTH participants.
            // This query can be tricky.
            // Find conversations where participant count is 2 and includes both users.
            
            var conversation = await _context.Conversations
                .Where(c => c.Participants.Any(p => p.UserId == user1Id) && 
                            c.Participants.Any(p => p.UserId == user2Id))
                .FirstOrDefaultAsync();

            if (conversation != null) return conversation;

            // Create new
            conversation = new Conversation();
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _context.ConversationParticipants.Add(new ConversationParticipant { ConversationId = conversation.Id, UserId = user1Id });
            _context.ConversationParticipants.Add(new ConversationParticipant { ConversationId = conversation.Id, UserId = user2Id });
            await _context.SaveChangesAsync();

            return conversation;
        }
    }
}
