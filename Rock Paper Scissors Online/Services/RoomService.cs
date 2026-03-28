using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using Rock_Paper_Scissors_Online.Ultilities;
using System.Collections.Concurrent;

namespace Rock_Paper_Scissors_Online.Services
{
    public class RoomService : IRoomService
    {
        private readonly ConcurrentDictionary<string, GameRoom> _rooms = new ConcurrentDictionary<string, GameRoom>();
        private readonly TimeSpan _emptyRoomTimeout;
        // TimeSpan đại diện cho khoảng thời gian

        public RoomService(IConfiguration configuration)
        {
            if (int.TryParse(configuration["RoomSettings:EmptyRoomTimeoutMinutes"], out int timeoutMinutes))
            {
                _emptyRoomTimeout = TimeSpan.FromMinutes(timeoutMinutes);
            }
            else
            {
                _emptyRoomTimeout = TimeSpan.FromMinutes(5); // mặc định 5 phút
            }
        }

        private void UpdateCurrentPlayers(GameRoom GameRoom)
        {
            int count = 0;
            if (GameRoom.Player1 != null) count++;
            if (GameRoom.Player2 != null) count++;
            GameRoom.CurrentPlayers = count;
            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m UpdateCurrentPlayers - GameRoom: {GameRoom.Name}, Player1: {GameRoom.Player1?.Username}, Player2: {GameRoom.Player2?.Username}, CurrentPlayers: {GameRoom.CurrentPlayers}");
        }

        public async Task<GameRoom> CreateRoomAsync(
            string roomName,
            int bestOfRounds,
            int pointsPerWin,
            string createdBy,
            string creatorUsername,
            string creatorDisplayName,
            bool isPrivate = false,
            int maxPlayers = 2,
            bool allowSpectators = true,
            bool allowBetting = false)
        {
            await Task.CompletedTask;

            var roomId = Guid.NewGuid().ToString("N");
            var pinCode = PinCodeGenerator.GeneratePinCode(); // Generate PIN code for all rooms

            var GameRoom = new GameRoom
            {
                Id = roomId,
                Name = roomName,
                MaxPlayers = maxPlayers,
                IsPrivate = isPrivate,
                PinCode = pinCode,
                BestOfRounds = bestOfRounds,
                PointsPerWin = pointsPerWin,
                AllowSpectators = allowSpectators,
                AllowBetting = allowBetting,
                Status = RoomStatus.Waiting,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                CreatedByUsername = creatorUsername,
                CreatedByDisplayName = creatorDisplayName,
                Player1 = new RoomPlayer
                {
                    UserId = createdBy,
                    Username = creatorUsername,
                    ConnectionId = string.Empty,
                    IsReady = false
                },
                CurrentRound = 0,
                TimeoutAt = DateTime.UtcNow.Add(_emptyRoomTimeout)
            };

            UpdateCurrentPlayers(GameRoom);

            _rooms.TryAdd(roomId, GameRoom);
            
            // Add GameRoom to BettingService
            BettingService.AddRoom(GameRoom);
            
            return GameRoom;
        }

        public async Task<GameRoom?> GetRoomAsync(string roomId)
        {
            await Task.CompletedTask;
            _rooms.TryGetValue(roomId, out var GameRoom);
            return GameRoom;
        }

        public async Task<GameRoom?> GetRoomByPinAsync(string pinCode)
        {
            await Task.CompletedTask;
            var GameRoom = _rooms.Values.FirstOrDefault(r => r.PinCode == pinCode);
            if (GameRoom != null)
            {
                // Update the current players count to ensure it's accurate
                UpdateCurrentPlayers(GameRoom);
            }
            return GameRoom;
        }

        public async Task<List<GameRoom>> GetAllRoomsAsync()
        {
            await Task.CompletedTask;
            return _rooms.Values.ToList();
        }

        public async Task<bool> UpdateRoomStatusAsync(string roomId, RoomStatus status)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return false;

            GameRoom.Status = status;
            return true;
        }

        public async Task<bool> RoomExistsAsync(string roomName)
        {
            await Task.CompletedTask;
            return _rooms.Values.Any(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));
        }

        private bool CheckUserExist(string userId)
        {
            return _rooms.Values.Any(r => (r.Player1?.UserId == userId) || (r.Player2?.UserId == userId));
        }

        private bool CheckUserExistInOtherRoom(string userId, string currentRoomId)
        {
            return _rooms.Values.Any(r => r.Id != currentRoomId && ((r.Player1?.UserId == userId) || (r.Player2?.UserId == userId)));
        }

        public async Task<(bool Success, string Message, GameRoom? Room)> GetRoomIfUserInRoomAsync(string roomId, string userId)
        {
            await Task.CompletedTask;
            
            if (!_rooms.TryGetValue(roomId, out var GameRoom))
            {
                return (false, "GameRoom not found", null);
            }

            // Check if user is already in this GameRoom as a player
            if (GameRoom.Player1?.UserId == userId || GameRoom.Player2?.UserId == userId)
            {
                return (true, "User already in GameRoom", GameRoom);
            }

            return (false, "User not in GameRoom", null);
        }

        public async Task<(bool Success, string Message, GameRoom? Room)> JoinRoomAsync(
            string roomId, string userId, string username, string? pinCode = null)
        {
            await Task.CompletedTask;

            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m JoinRoomAsync called - RoomId: {roomId}, UserId: {userId}, Username: {username}");

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom not found: {roomId}");
                return (false, "GameRoom not found", null);
            }

            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom found - CurrentPlayers: {GameRoom.CurrentPlayers}, Player1: {GameRoom.Player1?.Username}, Player2: {GameRoom.Player2?.Username}");

            if (CheckUserExistInOtherRoom(userId, roomId))
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m User {username} already exists in another GameRoom");
                return (false, "You are already in another GameRoom", null);
            }

            if (GameRoom.IsPrivate)
            {
                if (string.IsNullOrEmpty(pinCode) || GameRoom.PinCode != pinCode)
                {
                    Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Invalid PIN code for private GameRoom");
                    return (false, "Invalid or missing pin code", null);
                }
            }

            if (GameRoom.CurrentPlayers >= GameRoom.MaxPlayers)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom is full - CurrentPlayers: {GameRoom.CurrentPlayers}, MaxPlayers: {GameRoom.MaxPlayers}");
                return (false, "GameRoom is full", null);
            }

            if (GameRoom.Player1?.UserId == userId || GameRoom.Player2?.UserId == userId)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m User {username} already joined this GameRoom");
                return (false, "You already joined this GameRoom", GameRoom);
            }

            // Prevent GameRoom creator from re-entering their own GameRoom after disconnection
            if (GameRoom.CreatedBy == userId)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom creator {username} cannot re-enter their own GameRoom after disconnection");
                return (false, "You cannot re-enter your own GameRoom after disconnection. The GameRoom will be deleted shortly.", null);
            }

            if (GameRoom.Player1 == null)
            {
                GameRoom.Player1 = new RoomPlayer { UserId = userId, Username = username, ConnectionId = string.Empty, IsReady = false };
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Added {username} as Player1");
            }
            else if (GameRoom.Player2 == null)
            {
                GameRoom.Player2 = new RoomPlayer { UserId = userId, Username = username, ConnectionId = string.Empty, IsReady = false };
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Added {username} as Player2");
            }
            else
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom already has 2 players");
                return (false, "GameRoom already has 2 players", null);
            }

            UpdateCurrentPlayers(GameRoom);
            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Updated GameRoom - CurrentPlayers: {GameRoom.CurrentPlayers}, Player1: {GameRoom.Player1?.Username}, Player2: {GameRoom.Player2?.Username}");

            return (true, "Joined GameRoom successfully", GameRoom);
        }

        public async Task<(bool Success, string Message, GameRoom? Room)> JoinRoomByPinAsync(string pinCode, string userId, string username)
        {
            await Task.CompletedTask;

            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Searching for GameRoom with PIN: {pinCode}");
            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Available rooms and their PINs:");
            foreach (var r in _rooms.Values)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom {r.Id}: PIN={r.PinCode}, IsPrivate={r.IsPrivate}, Name={r.Name}");
            }

            var GameRoom = _rooms.Values.FirstOrDefault(r => r.PinCode == pinCode);
            if (GameRoom == null)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m No GameRoom found with PIN: {pinCode}");
                return (false, "Invalid or not found pin code", null);
            }

            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Found GameRoom: {GameRoom.Id} (IsPrivate: {GameRoom.IsPrivate})");

            // All rooms can be joined by PIN code - no need to check if GameRoom is private

            if (CheckUserExistInOtherRoom(userId, GameRoom.Id))
                return (false, "You are already in another GameRoom", null);

            if (GameRoom.CurrentPlayers >= GameRoom.MaxPlayers)
            {
                return (false, "GameRoom is full", null);
            }

            if (GameRoom.Player1?.UserId == userId || GameRoom.Player2?.UserId == userId)
                return (false, "You already joined this GameRoom", GameRoom);

            // Prevent GameRoom creator from re-entering their own GameRoom after disconnection
            if (GameRoom.CreatedBy == userId)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m GameRoom creator {username} cannot re-enter their own GameRoom after disconnection (PIN)");
                return (false, "You cannot re-enter your own GameRoom after disconnection. The GameRoom will be deleted shortly.", null);
            }

            if (GameRoom.Player1 == null)
            {
                GameRoom.Player1 = new RoomPlayer { UserId = userId, Username = username, ConnectionId = string.Empty, IsReady = false };
            }
            else if (GameRoom.Player2 == null)
            {
                GameRoom.Player2 = new RoomPlayer { UserId = userId, Username = username, ConnectionId = string.Empty, IsReady = false };
            }
            else
            {
                return (false, "GameRoom already has 2 players", null);
            }

            UpdateCurrentPlayers(GameRoom);
            return (true, "Joined GameRoom successfully", GameRoom);
        }

        public async Task<(bool Success, string Message, GameRoom? Room)> LeaveRoomAsync(string roomId, string userId)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return (false, "GameRoom not found", null);

            bool playerFound = false;

            if (GameRoom.Player1?.UserId == userId)
            {
                GameRoom.Player1 = null;
                playerFound = true;
            }
            else if (GameRoom.Player2?.UserId == userId)
            {
                GameRoom.Player2 = null;
                playerFound = true;
            }

            if (!playerFound)
                return (false, "You are not in this GameRoom", null);

            UpdateCurrentPlayers(GameRoom);

            if (GameRoom.CurrentPlayers == 0)
            {
                _rooms.TryRemove(roomId, out _);
                return (true, "Left GameRoom and GameRoom deleted (empty)", null);
            }

            return (true, "Left GameRoom successfully", GameRoom);
        }

        public async Task<bool> RemoveEmptyRoomAsync(string roomId)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return false;

            UpdateCurrentPlayers(GameRoom);

            if (GameRoom.CurrentPlayers <= 0)
            {
                return _rooms.TryRemove(roomId, out _);
            }

            return false;
        }

        public async Task<List<GameRoom>> GetTimeoutRoomAsync()
        {
            await Task.CompletedTask;
            var now = DateTime.UtcNow;
            return _rooms.Values
                .Where(r => r.CurrentPlayers == 1 && r.TimeoutAt.HasValue && r.TimeoutAt <= now)
                .ToList();
        }

        public async Task<bool> SetPlayerReadyAsync(string roomId, string userId, bool isReady)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return false;

            // Check if user is a player in this GameRoom
            if (GameRoom.Player1?.UserId == userId)
            {
                GameRoom.Player1.IsReady = isReady;
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Player1 {GameRoom.Player1.Username} ready state set to {isReady}");
                return true;
            }
            else if (GameRoom.Player2?.UserId == userId)
            {
                GameRoom.Player2.IsReady = isReady;
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Player2 {GameRoom.Player2.Username} ready state set to {isReady}");
                return true;
            }

            return false;
        }

        public async Task<(bool Success, string Message, GameRoom? Room)> JoinAsSpectatorAsync(
            string roomId, string userId, string username, string? pinCode = null)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return (false, "GameRoom not found", null);

            if (!GameRoom.AllowSpectators)
                return (false, "This GameRoom does not allow spectators", null);

            // Check if user is already a player in any other GameRoom
            if (CheckUserExistInOtherRoom(userId, roomId))
                return (false, "You are already a player in another GameRoom", null);

            // Check if user is already a spectator in another GameRoom
            var existingSpectatorRoom = _rooms.Values.FirstOrDefault(r => r.Spectators.Contains(userId));
            if (existingSpectatorRoom != null && existingSpectatorRoom.Id != roomId)
            {
                // Remove user from the previous GameRoom as spectator
                existingSpectatorRoom.Spectators.Remove(userId);
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Removed {username} as spectator from GameRoom {existingSpectatorRoom.Id}");
            }

            if (GameRoom.IsPrivate)
            {
                if (string.IsNullOrEmpty(pinCode) || GameRoom.PinCode != pinCode)
                    return (false, "Invalid or missing pin code", null);
            }

            // Check if user is already a spectator in this GameRoom
            if (GameRoom.Spectators.Contains(userId))
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m User {username} is already spectating GameRoom {roomId}, returning current GameRoom data");
                return (true, "You are already spectating this GameRoom", GameRoom);
            }

            // Check if user is already a player in this GameRoom
            if (GameRoom.Player1?.UserId == userId || GameRoom.Player2?.UserId == userId)
                return (false, "You are already a player in this GameRoom", GameRoom);

            // Add user as spectator
            GameRoom.Spectators.Add(userId);
            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Added {username} as spectator to GameRoom {roomId}");
            return (true, "Joined GameRoom as spectator successfully", GameRoom);
        }


        public async Task<(bool Success, string Message, GameRoom? Room)> LeaveAsSpectatorAsync(string roomId, string userId)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return (false, "GameRoom not found", null);

            // Check if user is a spectator
            if (!GameRoom.Spectators.Contains(userId))
                return (false, "You are not spectating this GameRoom", null);

            // Remove user from spectators
            GameRoom.Spectators.Remove(userId);
            Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m User {userId} left as spectator from GameRoom {roomId}. Remaining spectators: {GameRoom.Spectators.Count}");

            return (true, "Left GameRoom as spectator successfully", GameRoom);
        }

        public async Task<(bool Success, string Message)> DeleteRoomAsync(string roomId, string userId)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return (false, "GameRoom not found");

            // Only the GameRoom creator can delete the GameRoom
            if (GameRoom.CreatedBy != userId)
                return (false, "Only the GameRoom creator can delete this GameRoom");

            // If game is in progress, end it first
            if (GameRoom.Status == RoomStatus.InProgress)
            {
                await EndGameDueToRoomDeletion(GameRoom);
            }

            // Remove the GameRoom
            _rooms.TryRemove(roomId, out _);
            
            // Remove GameRoom from BettingService
            BettingService.RemoveRoom(roomId);
            
            return (true, "GameRoom deleted successfully");
        }

        private Task EndGameDueToRoomDeletion(GameRoom GameRoom)
        {
            try
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Ending game due to GameRoom deletion: {GameRoom.Id}");
                
                // Update GameRoom status to completed
                GameRoom.Status = RoomStatus.Completed;
                
                // Notify all players and spectators about GameRoom deletion
                // This will be handled by the GameHub when it receives the deletion signal
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Game ended due to GameRoom deletion for GameRoom {GameRoom.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[32m[GameRoom SERVICE]\u001b[0m Error ending game due to GameRoom deletion: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        public async Task<bool> IsUserInRoomAsync(string roomId, string userId)
        {
            await Task.CompletedTask;

            if (!_rooms.TryGetValue(roomId, out var GameRoom))
                return false;

            // Check if user is player1, player2, or spectator
            return GameRoom.Player1?.UserId == userId ||
                   GameRoom.Player2?.UserId == userId ||
                   GameRoom.Spectators.Contains(userId);
        }
    }
}
