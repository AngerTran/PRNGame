using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendGlobalMessage(string userId, string username, string content)
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
            // Broadcast tới tất cả client
            await Clients.All.SendAsync("ReceiveGlobalMessage", message);
        }
    }
}
