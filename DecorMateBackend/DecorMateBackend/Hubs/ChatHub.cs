using DecorMateBackend.Models;
using DecorMateBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace DecorMateBackend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatService _chatService;

        public ChatHub(ChatService chatService)
        {
            _chatService = chatService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }
            await base.OnConnectedAsync();
        }

        public async Task SendMessage(string receiverId, string messageContent)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderId)) return;

            // Save and encrypt message
            var message = await _chatService.SaveMessageAsync(senderId, receiverId, messageContent);

            // Send to receiver
            await Clients.Group(receiverId).SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.SenderId,
                message.Content, // This is the decrypted content returned by SaveMessageAsync
                message.Timestamp,
                message.ConversationId
            });

            // Send back to sender (so they see it confirmed/decrypted if needed, or just to update UI)
            await Clients.Group(senderId).SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.SenderId,
                message.Content,
                message.Timestamp,
                message.ConversationId
            });
        }

        public async Task LoadHistory(string otherUserId)
        {
            var currentUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return;

            var history = await _chatService.GetConversationHistoryAsync(currentUserId, otherUserId);
            await Clients.Caller.SendAsync("ReceiveHistory", history);
        }
    }
}
