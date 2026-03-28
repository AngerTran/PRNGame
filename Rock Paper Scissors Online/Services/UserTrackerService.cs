using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Collections.Concurrent;

namespace Rock_Paper_Scissors_Online.Services
{
    public class UserTrackerService : IUserTrackerService
    {
        // ConcurrentDictionary là bắt buộc để đảm bảo an toàn khi nhiều luồng
        // (từ các kết nối SignalR khác nhau) truy cập cùng lúc.
        // Key: connectionId của SignalR (duy nhất cho mỗi kết nối/tab)
        // Value: userId (có thể trùng lặp nếu người dùng mở nhiều tab)
        private static readonly ConcurrentDictionary<string, string> OnlineUsers = new();
        public Task AddConnectionAsyns(string userId, string connectionId)
        {
            //thêm hoặc cập nhật kết nối 
            OnlineUsers[connectionId] = userId;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetOnlineUserId()
        {
            // trả về danh sách userId không trùng lặp
            var unipueUserIds = OnlineUsers.Values.Distinct();
            return Task.FromResult(unipueUserIds);
        }

        public Task RemoveConnectionAsync(string connectionId)
        {
            // xóa kết nối khi user ngắt kết nối
            OnlineUsers.TryRemove(connectionId, out _);
            return Task.CompletedTask;
        }
    }
}
