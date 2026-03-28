using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IRoomChatService
    {
        IEnumerable<ChatMessage> GetMessages(string roomId, int limit);
        ChatMessage AddMessage(string roomId, string userId, string username, string content);
    }
}
