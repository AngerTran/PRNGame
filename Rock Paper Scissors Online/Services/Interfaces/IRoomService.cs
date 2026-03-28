using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IRoomService
    {
        Task<GameRoom> CreateRoomAsync(string roomName, int bestOfRounds, int pointsPerWin, string createdBy, string creatorUsername, string creatorDisplayName, bool isPrivate = false, int maxPlayers = 2, bool allowSpectators = true, bool allowBetting = false);
        Task<GameRoom?> GetRoomAsync(string roomId);
        Task<GameRoom?> GetRoomByPinAsync(string pinCode);
        Task<List<GameRoom>> GetAllRoomsAsync();
        Task<bool> UpdateRoomStatusAsync(string roomId, RoomStatus status);
        Task<bool> RoomExistsAsync(string roomName);
        Task<(bool Success, string Message, GameRoom? Room)> JoinRoomAsync(string roomId, string userId, string username, string? pinCode = null);
        Task<(bool Success, string Message, GameRoom? Room)> JoinRoomByPinAsync(string pincode, string userId, string username);
        Task<(bool Success, string Message, GameRoom? Room)> JoinAsSpectatorAsync(string roomId, string userId, string username, string? pinCode = null);
        Task<(bool Success, string Message, GameRoom? Room)> LeaveAsSpectatorAsync(string roomId, string userId);
        Task<(bool Success, string Message, GameRoom? Room)> LeaveRoomAsync(string roomId, string userId);
        Task<(bool Success, string Message)> DeleteRoomAsync(string roomId, string userId);
        Task<bool> RemoveEmptyRoomAsync(string roomId);
        Task<List<GameRoom>> GetTimeoutRoomAsync();
        Task<bool> IsUserInRoomAsync(string roomId, string userId);
        Task<(bool Success, string Message, GameRoom? Room)> GetRoomIfUserInRoomAsync(string roomId, string userId);
        Task<bool> SetPlayerReadyAsync(string roomId, string userId, bool isReady);
    }
}
