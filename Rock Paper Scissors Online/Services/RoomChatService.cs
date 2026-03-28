using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class RoomChatService : IRoomChatService
    {
        private readonly Dictionary<string, List<ChatMessage>> _roomMessages = new();

        public IEnumerable<ChatMessage> GetMessages(string roomId, int limit = 500)
        {
            Console.WriteLine($"[ROOM CHAT SERVICE] Getting messages for room {roomId}, limit: {limit}");

            if (!_roomMessages.ContainsKey(roomId))
            {
                Console.WriteLine($"[ROOM CHAT SERVICE] No messages found for room {roomId}");
                return Enumerable.Empty<ChatMessage>();
            }

            var allMessages = _roomMessages[roomId];
            Console.WriteLine($"[ROOM CHAT SERVICE] Total messages in room {roomId}: {allMessages.Count}");

            var result = allMessages
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .ToList();

            Console.WriteLine($"[ROOM CHAT SERVICE] Returning {result.Count} messages");
            return result;
        }

        public ChatMessage AddMessage(string roomId, string userId, string username, string content)
        {
            Console.WriteLine($"[ROOM CHAT SERVICE] Adding message to room {roomId} from {username}: {content}");

            if (!_roomMessages.ContainsKey(roomId))
            {
                _roomMessages[roomId] = new List<ChatMessage>();
                Console.WriteLine($"[ROOM CHAT SERVICE] Created new message list for room {roomId}");
            }

            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                UserId = userId,
                Username = username,
                Content = content,
                Type = "user",
                Timestamp = DateTime.UtcNow
            };

            _roomMessages[roomId].Add(message);
            Console.WriteLine($"[ROOM CHAT SERVICE] Message added. Total messages in room {roomId}: {_roomMessages[roomId].Count}");
            return message;
        }
    }
}
