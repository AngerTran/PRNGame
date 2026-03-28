using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Collections.Concurrent;

namespace Rock_Paper_Scissors_Online.Services
{
    public class EnhancedRoomManagementService : IEnhancedRoomManagementService
    {
        private readonly IRoomService _roomService;
        private readonly ISessionManagementService _sessionManagementService;

        public EnhancedRoomManagementService(IRoomService roomService, ISessionManagementService sessionManagementService)
        {
            _roomService = roomService;
            _sessionManagementService = sessionManagementService;
        }

        public async Task<RoomManagementStats> GetRoomManagementStatsAsync()
        {
            var allRooms = await _roomService.GetAllRoomsAsync();
            
            return new RoomManagementStats
            {
                TotalRooms = allRooms.Count,
                ActiveRooms = allRooms.Count(r => r.Status == RoomStatus.Waiting || r.Status == RoomStatus.InProgress),
                WaitingRooms = allRooms.Count(r => r.Status == RoomStatus.Waiting),
                InProgressRooms = allRooms.Count(r => r.Status == RoomStatus.InProgress),
                CompletedRooms = allRooms.Count(r => r.Status == RoomStatus.Completed),
                PrivateRooms = allRooms.Count(r => r.IsPrivate),
                PublicRooms = allRooms.Count(r => !r.IsPrivate),
                RoomsWithSpectators = allRooms.Count(r => r.Spectators.Count > 0),
                TotalSpectators = allRooms.Sum(r => r.Spectators.Count),
                AveragePlayersPerRoom = allRooms.Count > 0 ? (double)allRooms.Sum(r => r.CurrentPlayers) / allRooms.Count : 0,
                OldestRoom = allRooms.OrderBy(r => r.CreatedAt).FirstOrDefault(),
                NewestRoom = allRooms.OrderByDescending(r => r.CreatedAt).FirstOrDefault()
            };
        }

        public async Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status)
        {
            var allRooms = await _roomService.GetAllRoomsAsync();
            return allRooms.Where(r => r.Status == status).ToList();
        }

        public async Task<List<GameRoom>> GetRoomsByCreatorAsync(string creatorId)
        {
            var allRooms = await _roomService.GetAllRoomsAsync();
            return allRooms.Where(r => r.CreatedBy == creatorId).ToList();
        }

        public async Task<bool> CleanupInactiveRoomsAsync()
        {
            try
            {
                var allRooms = await _roomService.GetAllRoomsAsync();
                var inactiveRooms = allRooms.Where(r => 
                    r.Status == RoomStatus.Waiting && 
                    r.CreatedAt < DateTime.UtcNow.AddMinutes(-30) && 
                    r.CurrentPlayers == 0
                ).ToList();

                foreach (var GameRoom in inactiveRooms)
                {
                    await _roomService.DeleteRoomAsync(GameRoom.Id, GameRoom.CreatedBy);
                    Console.WriteLine($"[GameRoom MANAGEMENT] Cleaned up inactive GameRoom: {GameRoom.Name} ({GameRoom.Id})");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRoom MANAGEMENT] Error cleaning up inactive rooms: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ForceCloseRoomAsync(string roomId, string reason)
        {
            try
            {
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null) return false;

                // Update GameRoom status to completed
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Completed);

                Console.WriteLine($"[GameRoom MANAGEMENT] Force closed GameRoom {roomId}: {reason}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRoom MANAGEMENT] Error force closing GameRoom {roomId}: {ex.Message}");
                return false;
            }
        }

        public async Task<List<GameRoom>> GetRoomsWithLowActivityAsync(int minutesThreshold)
        {
            var allRooms = await _roomService.GetAllRoomsAsync();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-minutesThreshold);
            
            return allRooms.Where(r => 
                r.Status == RoomStatus.Waiting && 
                r.CreatedAt < cutoffTime &&
                r.CurrentPlayers < 2
            ).ToList();
        }

        public async Task<bool> TransferRoomOwnershipAsync(string roomId, string fromUserId, string toUserId)
        {
            try
            {
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null || GameRoom.CreatedBy != fromUserId) return false;

                // Update GameRoom creator
                GameRoom.CreatedBy = toUserId;
                GameRoom.CreatedByUsername = toUserId; // This would need to be updated with actual username

                Console.WriteLine($"[GameRoom MANAGEMENT] Transferred GameRoom {roomId} ownership from {fromUserId} to {toUserId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameRoom MANAGEMENT] Error transferring GameRoom ownership: {ex.Message}");
                return false;
            }
        }

        public async Task<RoomHealthStatus> GetRoomHealthStatusAsync(string roomId)
        {
            var GameRoom = await _roomService.GetRoomAsync(roomId);
            if (GameRoom == null)
            {
                return new RoomHealthStatus
                {
                    RoomId = roomId,
                    IsHealthy = false,
                    Issues = new List<string> { "GameRoom not found" },
                    HealthScore = 0
                };
            }

            var issues = new List<string>();
            var healthScore = 100;

            // Check if GameRoom has been waiting too long
            if (GameRoom.Status == RoomStatus.Waiting && GameRoom.CreatedAt < DateTime.UtcNow.AddMinutes(-15))
            {
                issues.Add("GameRoom has been waiting for over 15 minutes");
                healthScore -= 20;
            }

            // Check if GameRoom has no players
            if (GameRoom.CurrentPlayers == 0)
            {
                issues.Add("GameRoom has no players");
                healthScore -= 30;
            }

            // Check if GameRoom has only one player for too long
            if (GameRoom.CurrentPlayers == 1 && GameRoom.CreatedAt < DateTime.UtcNow.AddMinutes(-10))
            {
                issues.Add("GameRoom has only one player for over 10 minutes");
                healthScore -= 15;
            }

            // Check if GameRoom has too many spectators
            if (GameRoom.Spectators.Count > 50)
            {
                issues.Add("GameRoom has too many spectators");
                healthScore -= 10;
            }

            return new RoomHealthStatus
            {
                RoomId = roomId,
                IsHealthy = issues.Count == 0,
                Issues = issues,
                HealthScore = Math.Max(0, healthScore),
                CreatedAt = GameRoom.CreatedAt,
                CurrentPlayers = GameRoom.CurrentPlayers,
                SpectatorCount = GameRoom.Spectators.Count,
                Status = GameRoom.Status.ToString()
            };
        }
    }
}
