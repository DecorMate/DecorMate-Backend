using DecorMateBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DecorMateBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpGet("history/{otherUserId}")]
        public async Task<IActionResult> GetHistory(string otherUserId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetHistory called without valid user ID");
                    return Unauthorized();
                }

                _logger.LogInformation("Loading chat history for user {UserId} with {OtherUserId}", userId, otherUserId);

                var history = await _chatService.GetConversationHistoryAsync(userId, otherUserId);
                
                _logger.LogInformation("Successfully loaded {MessageCount} messages for conversation between {UserId} and {OtherUserId}", 
                    history.Count, userId, otherUserId);

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat history for user {UserId} with {OtherUserId}: {ErrorMessage}", 
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, otherUserId, ex.Message);
                
                return StatusCode(500, new { 
                    error = "Failed to load chat history", 
                    message = ex.Message,
                    type = ex.GetType().Name
                });
            }
        }
    }
}
