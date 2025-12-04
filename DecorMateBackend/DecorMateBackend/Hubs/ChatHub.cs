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
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ChatService chatService, ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"Client connected: ConnectionId={Context.ConnectionId}, UserId={userId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"Client disconnected: ConnectionId={Context.ConnectionId}, UserId={userId}, Exception={exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderId)) return;

            _logger.LogInformation($"SendMessage called: SenderId={senderId}, ReceiverId={receiverId}");

            // Save message to DB
            var message = await _chatService.SaveMessageAsync(senderId, receiverId, content);

            // Create a DTO to avoid circular reference issues
            var messageDto = new
            {
                message.Id,
                message.SenderId,
                ReceiverId = receiverId,
                message.Content,
                message.Timestamp,
                message.ConversationId,
                message.IsRead
            };

            // Send to receiver
            await Clients.User(receiverId).SendAsync("ReceiveMessage", messageDto);

            // Send back to sender
            await Clients.User(senderId).SendAsync("ReceiveMessage", messageDto);
        }
    }
}


