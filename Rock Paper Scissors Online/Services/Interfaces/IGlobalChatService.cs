using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IGlobalChatService
    {
        IEnumerable<ChatMessage> GetMessages(int limit);
        ChatMessage AddMessage(string userId, string username, string content);
    }
}
