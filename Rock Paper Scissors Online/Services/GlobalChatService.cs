using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class GlobalChatService : IGlobalChatService
    {
        private readonly List<ChatMessage> _messages = new();

        public IEnumerable<ChatMessage> GetMessages(int limit = 500)
        {
            var result = _messages
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .ToList();
            return result;
        }

        public ChatMessage AddMessage(string userId, string username, string content)
        {
            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Username = username,
                Content = content,
                Type = "user",
                Timestamp = DateTime.UtcNow
            };
            _messages.Add(message);
            return message;
        }
    }

}
