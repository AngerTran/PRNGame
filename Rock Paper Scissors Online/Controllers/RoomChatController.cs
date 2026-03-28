using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/chat/rooms/{roomId}/messages")]
    [Authorize]
    public class RoomChatController : ControllerBase
    {
        private readonly IRoomChatService _roomChatService;
        private readonly IHubContext<GameHub> _gameHubContext;

        public RoomChatController(IRoomChatService roomChatService, IHubContext<GameHub> gameHubContext)
        {
            _roomChatService = roomChatService;
            _gameHubContext = gameHubContext;
        }

        [HttpGet]
        public IActionResult GetMessages(string roomId, [FromQuery] int limit = 500)
        {
            Console.WriteLine($"[ROOM CHAT] Getting messages for room {roomId}, limit: {limit}");
            var messages = _roomChatService.GetMessages(roomId, limit);
            Console.WriteLine($"[ROOM CHAT] Found {messages.Count()} messages");
            return Ok(new { success = true, data = new { messages, hasMore = messages.Count() == limit } });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string roomId, [FromBody] SendMessageRequest request)
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid message format", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            // Additional content validation
            var validationResult = ValidateChatMessage(request.Content);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { success = false, message = validationResult.ErrorMessage });
            }

            Console.WriteLine($"[ROOM CHAT] Sending message to room {roomId} from user {request.Username}: {request.Content}");
            var msg = _roomChatService.AddMessage(roomId, request.UserId, request.Username, request.Content);
            Console.WriteLine($"[ROOM CHAT] Message added with ID: {msg.Id}");

            // Broadcast message to all users in the room via SignalR
            try
            {
                await _gameHubContext.Clients.Group(roomId).SendAsync("ReceiveRoomMessage", msg);
                Console.WriteLine($"[ROOM CHAT] Message broadcasted to room group: {roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ROOM CHAT] Failed to broadcast message to room {roomId}: {ex.Message}");
            }

            return Ok(new { success = true, message = "Message sent successfully", data = new { messageId = msg.Id, timestamp = msg.Timestamp } });
        }

        private static (bool IsValid, string ErrorMessage) ValidateChatMessage(string content)
        {
            // Check for empty or whitespace-only messages
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, "Message cannot be empty");
            }

            // Check message length (already handled by StringLength attribute, but double-check)
            if (content.Length > 100)
            {
                return (false, "Message too long (max 100 characters)");
            }

            // Check for spam patterns (repeated characters)
            if (HasRepeatedCharacters(content, 5))
            {
                return (false, "Message contains too many repeated characters");
            }

            // Check for inappropriate content (basic filtering)
            var inappropriateWords = new[] { "spam", "hack", "cheat", "bot", "scam", "fake" };
            var lowerContent = content.ToLower();
            foreach (var word in inappropriateWords)
            {
                if (lowerContent.Contains(word))
                {
                    return (false, "Message contains inappropriate content");
                }
            }

            return (true, string.Empty);
        }

        private static bool HasRepeatedCharacters(string text, int maxRepeats)
        {
            if (text.Length < maxRepeats) return false;

            for (int i = 0; i <= text.Length - maxRepeats; i++)
            {
                char currentChar = text[i];
                int repeatCount = 1;

                for (int j = i + 1; j < text.Length && text[j] == currentChar; j++)
                {
                    repeatCount++;
                }

                if (repeatCount >= maxRepeats)
                {
                    return true;
                }
            }

            return false;
        }
    }

}
