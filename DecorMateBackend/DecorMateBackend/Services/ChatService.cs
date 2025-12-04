using DecorMate_Backend_Web_app.Data;
using DecorMateBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace DecorMateBackend.Services
{
    public class ChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(ApplicationDbContext context, EncryptionService encryptionService, ILogger<ChatService> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
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
            try
            {
                _logger.LogInformation("Getting conversation history between {UserId} and {OtherUserId}", userId, otherUserId);

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

                if (conversationId == 0)
                {
                    _logger.LogInformation("No conversation found between {UserId} and {OtherUserId}", userId, otherUserId);
                    return new List<object>();
                }

                _logger.LogInformation("Found conversation {ConversationId}, loading messages", conversationId);

                var messages = await _context.ChatMessages
                    .Where(m => m.ConversationId == conversationId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                _logger.LogInformation("Loaded {MessageCount} messages from database for conversation {ConversationId}", 
                    messages.Count, conversationId);

                var decryptedMessages = new List<object>();
                int decryptionErrors = 0;

                foreach (var m in messages)
                {
                    string decryptedContent;
                    try
                    {
                        // Check if content is Base64 encoded (encrypted)
                        // If it's not valid Base64, it's likely plain text (unencrypted)
                        if (IsBase64String(m.Content))
                        {
                            decryptedContent = _encryptionService.Decrypt(m.Content);
                            _logger.LogDebug("Successfully decrypted message {MessageId}", m.Id);
                        }
                        else
                        {
                            // Content is not encrypted (plain text)
                            decryptedContent = m.Content;
                            _logger.LogWarning("Message {MessageId} is stored as plain text (not encrypted)", m.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        decryptionErrors++;
                        _logger.LogError(ex, "Failed to decrypt message {MessageId} in conversation {ConversationId}. " +
                            "Content length: {ContentLength}, Content preview: {ContentPreview}. Error: {ErrorMessage}", 
                            m.Id, conversationId, m.Content?.Length ?? 0, 
                            m.Content?.Length > 50 ? m.Content.Substring(0, 50) + "..." : m.Content,
                            ex.Message);
                        
                        // Use original content as fallback
                        decryptedContent = m.Content;
                    }

                    decryptedMessages.Add(new
                    {
                        m.Id,
                        m.SenderId,
                        Content = decryptedContent,
                        m.Timestamp,
                        m.ConversationId
                    });
                }

                if (decryptionErrors > 0)
                {
                    _logger.LogWarning("Failed to decrypt {ErrorCount} out of {TotalCount} messages in conversation {ConversationId}", 
                        decryptionErrors, messages.Count, conversationId);
                }

                return decryptedMessages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetConversationHistoryAsync for users {UserId} and {OtherUserId}: {ErrorMessage}", 
                    userId, otherUserId, ex.Message);
                throw;
            }
        }

        public string DecryptMessage(string encryptedContent)
        {
            return _encryptionService.Decrypt(encryptedContent);
        }

        private bool IsBase64String(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                // Attempt to convert from Base64
                Convert.FromBase64String(value);
                return true;
            }
            catch
            {
                return false;
            }
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
