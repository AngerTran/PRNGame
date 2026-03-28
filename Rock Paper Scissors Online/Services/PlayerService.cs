using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Collections.Concurrent;

namespace Rock_Paper_Scissors_Online.Services
{
    public class PlayerService : IPlayerService
    {
        private readonly IUserTrackerService _userTrackerService;
        private readonly IUserRepository _userRepository;
        private readonly IRoomService _roomService;
        private static readonly ConcurrentDictionary<string, List<InviteRequestDto>> _invitations
            = new();

        public PlayerService(IUserTrackerService userTrackerService, IUserRepository userRepository, IRoomService roomService)
        {
            _userTrackerService = userTrackerService;
            _userRepository = userRepository;
            _roomService = roomService;
        }
        public async Task<OnlinePlayerDetailDto> GetOnlinePlayersAndStatsAsync()
        {
            // lấy list id của người chơi đang online
            var OnlinePlayersIds = (await _userTrackerService.GetOnlineUserId()).Select(Guid.Parse).ToList();

            var onlinePlayerList = new List<OnlinePlayerDetailDto>();

            if (OnlinePlayersIds.Any())
            {
                // 2. Lấy thông tin chi tiết từ DB cho những người dùng đó
                var usersFromDb = await _userRepository.GetUsersByIdsAsync(OnlinePlayersIds);

                // 3. Map từ User model sang DTO
                onlinePlayerList = usersFromDb.Select(u => new OnlinePlayerDetailDto
                {
                    UserId = u.Id.ToString(),
                    UserName = u.Username,
                    Point = u.Points,
                    GameWon = u.Wins,
                    GamePlayed = u.TotalGames,
                    CurrentStreak = u.CurrentWinStreak,
                    LastActive = u.LastPlayedAt?.UtcDateTime,
                    Avatar = u.Avatar
                }).ToList();
            }
            // 4. Lấy thông tin về số phòng hiện có và số kết nối trong game
            var allRooms = await _roomService.GetAllRoomsAsync();

            // tính total GameRoom
            var totalRoom = allRooms.Count;

            // tính total connection in game
            var totalConnectionInGame = allRooms
                                        .Where(r => r.Status == Enums.RoomStatus.Playing) // chỉ tính phòng đang chơi
                                        .Sum(r => r.CurrentPlayers); // tổng số người chơi trong các phòng đó

            // 5. Tạo và trả về DTO tổng hợp
            var response = new OnlinePlayerDetailDto
            {
                Players = onlinePlayerList,
                TotalRoom = totalRoom,
                TotalOnline = OnlinePlayersIds.Count,
                TotalConnectionInGame = totalConnectionInGame
            };

            return response;
        }

        public async Task<IEnumerable<OnlinePlayerDetailDto>> SearchPlayersByUsernameAsync(string username)
        {
            var users = await _userRepository.SearchUsersByUsernameAsync(username);

            return users.Select(u => new OnlinePlayerDetailDto
            {
                UserId = u.Id.ToString(),
                UserName = u.Username,
                Point = u.Points,
                GameWon = u.Wins,
                GamePlayed = u.TotalGames,
                CurrentStreak = u.CurrentWinStreak,
                LastActive = u.LastPlayedAt?.UtcDateTime,
                Avatar = u.Avatar
            });
        }

        public async Task<PlayerStatsDto?> GetPlayerStatsAsync(string playerId)
        {
            var user = await _userRepository.GetByIdAsync(Guid.Parse(playerId));
            if (user == null) return null;

            // check online status từ UserTracker
            var onlineIds = await _userTrackerService.GetOnlineUserId();
            var isOnline = onlineIds.Contains(user.Id.ToString());

            return new PlayerStatsDto
            {
                UserId = user.Id.ToString(),
                UserName = user.Username,
                Points = user.Points,
                GamesWon = user.Wins,
                GamesPlayed = user.TotalGames,
                CurrentStreak = user.CurrentWinStreak,
                LongestStreak = user.LongestWinStreak,
                LastActive = user.LastPlayedAt?.UtcDateTime,
                //Status = isOnline ? "online" : "offline",
                Avatar = user.Avatar,
                JoinedAt = user.CreatedAt.UtcDateTime
            };
        }



        public void SendInvitation(string toPlayerId, InviteRequestDto request)
        {
            if (!_invitations.ContainsKey(toPlayerId))
            {
                _invitations[toPlayerId] = new List<InviteRequestDto>();
            }

            _invitations[toPlayerId].Add(request);

            Console.WriteLine($"Invitation sent to Player {toPlayerId} with message: {request.Message}");
        }

        public IEnumerable<InviteRequestDto> GetInvitations(string playerId)
        {
            return _invitations.TryGetValue(playerId, out var list) ? list : Enumerable.Empty<InviteRequestDto>();
        }
    }
}
