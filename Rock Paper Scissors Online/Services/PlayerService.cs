using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class PlayerService : IPlayerService
    {
        private readonly IUserTrackerService _userTrackerService;
        private readonly IUserRepository _userRepository;
        private readonly IRoomService _roomService;

        private static readonly object InviteLock = new();
        private static readonly Dictionary<string, List<PlayerInvitationDto>> InvitesByToUser = new();

        public PlayerService(IUserTrackerService userTrackerService, IUserRepository userRepository, IRoomService roomService)
        {
            _userTrackerService = userTrackerService;
            _userRepository = userRepository;
            _roomService = roomService;
        }

        public async Task<OnlinePlayerDetailDto> GetOnlinePlayersAndStatsAsync()
        {
            var onlinePlayersIds = (await _userTrackerService.GetOnlineUserId()).Select(Guid.Parse).ToList();

            var onlinePlayerList = new List<OnlinePlayerDetailDto>();

            if (onlinePlayersIds.Any())
            {
                var usersFromDb = await _userRepository.GetUsersByIdsAsync(onlinePlayersIds);

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

            var allRooms = await _roomService.GetAllRoomsAsync();
            var totalRoom = allRooms.Count;
            var totalConnectionInGame = allRooms
                .Where(r => r.Status == RoomStatus.Playing)
                .Sum(r => r.CurrentPlayers);

            return new OnlinePlayerDetailDto
            {
                Players = onlinePlayerList,
                TotalRoom = totalRoom,
                TotalOnline = onlinePlayersIds.Count,
                TotalConnectionInGame = totalConnectionInGame
            };
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
                Avatar = user.Avatar,
                JoinedAt = user.CreatedAt.UtcDateTime
            };
        }

        public void SendInvitation(string fromUserId, string fromUsername, string toPlayerId, InviteRequestDto request)
        {
            if (string.Equals(fromUserId, toPlayerId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không thể mời chính mình.");

            var inv = new PlayerInvitationDto
            {
                Id = Guid.NewGuid().ToString("N"),
                FromUserId = fromUserId,
                FromUsername = string.IsNullOrWhiteSpace(fromUsername) ? "Player" : fromUsername.Trim(),
                ToUserId = toPlayerId,
                Message = string.IsNullOrWhiteSpace(request.Message) ? "Bạn có muốn thi đấu không?" : request.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                Status = "pending"
            };

            lock (InviteLock)
            {
                if (!InvitesByToUser.TryGetValue(toPlayerId, out var list))
                {
                    list = new List<PlayerInvitationDto>();
                    InvitesByToUser[toPlayerId] = list;
                }

                list.Add(inv);
            }

            Console.WriteLine($"[INVITE] {fromUsername} -> {toPlayerId} ({inv.Id})");
        }

        public IReadOnlyList<PlayerInvitationDto> GetPendingInvitations(string toPlayerId)
        {
            lock (InviteLock)
            {
                if (!InvitesByToUser.TryGetValue(toPlayerId, out var list))
                    return Array.Empty<PlayerInvitationDto>();

                return list
                    .Where(i => string.Equals(i.Status, "pending", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList();
            }
        }

        public bool TryAcceptInvitation(string toPlayerId, string inviteId, out PlayerInvitationDto? invitation)
        {
            invitation = null;
            lock (InviteLock)
            {
                if (!InvitesByToUser.TryGetValue(toPlayerId, out var list))
                    return false;

                var item = list.FirstOrDefault(i =>
                    string.Equals(i.Id, inviteId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Status, "pending", StringComparison.OrdinalIgnoreCase));

                if (item == null)
                    return false;

                item.Status = "accepted";
                invitation = item;
                list.Remove(item);
                return true;
            }
        }

        public bool TryDeclineInvitation(string toPlayerId, string inviteId)
        {
            lock (InviteLock)
            {
                if (!InvitesByToUser.TryGetValue(toPlayerId, out var list))
                    return false;

                var item = list.FirstOrDefault(i =>
                    string.Equals(i.Id, inviteId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Status, "pending", StringComparison.OrdinalIgnoreCase));

                if (item == null)
                    return false;

                item.Status = "declined";
                list.Remove(item);
                return true;
            }
        }
    }
}
