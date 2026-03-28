using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IEnhancedRoomManagementService
    {
        Task<RoomManagementStats> GetRoomManagementStatsAsync();
        Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status);
        Task<List<GameRoom>> GetRoomsByCreatorAsync(string creatorId);
        Task<bool> CleanupInactiveRoomsAsync();
        Task<bool> ForceCloseRoomAsync(string roomId, string reason);
        Task<List<GameRoom>> GetRoomsWithLowActivityAsync(int minutesThreshold);
        Task<bool> TransferRoomOwnershipAsync(string roomId, string fromUserId, string toUserId);
        Task<RoomHealthStatus> GetRoomHealthStatusAsync(string roomId);
    }

    public class RoomManagementStats
    {
        public int TotalRooms { get; set; }
        public int ActiveRooms { get; set; }
        public int WaitingRooms { get; set; }
        public int InProgressRooms { get; set; }
        public int CompletedRooms { get; set; }
        public int PrivateRooms { get; set; }
        public int PublicRooms { get; set; }
        public int RoomsWithSpectators { get; set; }
        public int TotalSpectators { get; set; }
        public double AveragePlayersPerRoom { get; set; }
        public GameRoom? OldestRoom { get; set; }
        public GameRoom? NewestRoom { get; set; }
    }

    public class RoomHealthStatus
    {
        public string RoomId { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public List<string> Issues { get; set; } = new();
        public int HealthScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CurrentPlayers { get; set; }
        public int SpectatorCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
