using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/chat/global/messages")]
    public class GlobalChatController : ControllerBase
    {
        private readonly IGlobalChatService _chatService;
        private readonly IHubContext<ChatHub> _hubContext;

        public GlobalChatController(IGlobalChatService chatService, IHubContext<ChatHub> hubContext)
        {
            _chatService = chatService;
            _hubContext = hubContext;
        }

        [HttpGet]
        public IActionResult GetMessages([FromQuery] int limit = 500)
        {
            var result = _chatService.GetMessages(limit);
            return Ok(new
            {
                success = true,
                data = new
                {
                    messages = result,
                    hasMore = result.Count() == limit
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
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

            var message = _chatService.AddMessage(request.UserId, request.Username, request.Content);

            // Broadcast to all clients via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveGlobalMessage", message);

            return Ok(new
            {
                success = true,
                data = message
            });
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

    [ApiController]
    [Route("api/v1/chat/activity-feed")]
    public class ActivityFeedController : ControllerBase
    {
        private readonly IGlobalChatService _chatService;

        public ActivityFeedController(IGlobalChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Get global activity feed
        /// </summary>
        /// <param name="limit">Number of activities to return</param>
        /// <param name="cursor">Cursor for pagination</param>
        /// <returns>Activity feed</returns>
        [HttpGet]
        public IActionResult GetActivityFeed([FromQuery] int limit = 20, [FromQuery] DateTime? cursor = null)
        {
            try
            {
                // Mock activity feed data for now
                var activities = new List<ActivityDto>
                {
                    new ActivityDto
                    {
                        Id = "activity-123",
                        Type = "game_completed",
                        Message = "PlayerOne defeated PlayerTwo in Epic Battle Arena",
                        Timestamp = DateTime.UtcNow.AddMinutes(-5),
                        Participants = new List<string> { "user-123", "user-456" }
                    },
                    new ActivityDto
                    {
                        Id = "activity-124",
                        Type = "achievement_unlocked",
                        Message = "PlayerThree unlocked the 'Win Streak' achievement",
                        Timestamp = DateTime.UtcNow.AddMinutes(-10),
                        Participants = new List<string> { "user-789" }
                    },
                    new ActivityDto
                    {
                        Id = "activity-125",
                        Type = "game_completed",
                        Message = "RockMaster won against PaperQueen in Championship Room",
                        Timestamp = DateTime.UtcNow.AddMinutes(-15),
                        Participants = new List<string> { "user-101", "user-102" }
                    },
                    new ActivityDto
                    {
                        Id = "activity-126",
                        Type = "room_created",
                        Message = "NewPlayer created a new room 'Quick Match Arena'",
                        Timestamp = DateTime.UtcNow.AddMinutes(-20),
                        Participants = new List<string> { "user-103" }
                    }
                };

                // Apply cursor filtering if provided
                if (cursor.HasValue)
                {
                    activities = activities.Where(a => a.Timestamp < cursor.Value).ToList();
                }

                // Apply limit
                var hasMore = activities.Count > limit;
                var pagedActivities = activities.Take(limit).ToList();

                return Ok(new
                {
                    success = true,
                    data = new ActivityFeedResponseDto
                    {
                        Activities = pagedActivities,
                        HasMore = hasMore
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving activity feed",
                    error = ex.Message
                });
            }
        }
    }
}
