using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class OnlinePlayersService : IOnlinePlayersService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPointTransactionRepository _pointTransactionRepository;
        private readonly ISessionManagementService _sessionManagementService;

        public OnlinePlayersService(
            IUserRepository userRepository,
            IPointTransactionRepository pointTransactionRepository,
            ISessionManagementService sessionManagementService)
        {
            _userRepository = userRepository;
            _pointTransactionRepository = pointTransactionRepository;
            _sessionManagementService = sessionManagementService;
        }

        public async Task<OnlinePlayersResponse> GetOnlinePlayersAsync()
        {
            var activeSessions = await _sessionManagementService.GetAllActiveSessionsAsync();
            var onlineGuids = activeSessions
                .Select(s => s.UserId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .Distinct()
                .ToList();

            var users = (await _userRepository.GetUsersByIdsAsync(onlineGuids)).ToList();
            var onlineUsers = users.Select(u => new OnlinePlayerDto
            {
                Id = u.Id.ToString(),
                Username = u.Username,
                Points = u.Points,
                GamesWon = u.Wins,
                GamesPlayed = u.TotalGames,
                CurrentStreak = u.CurrentWinStreak,
                IsInGame = false,
                LastActive = DateTime.UtcNow,
                Status = "online",
                Avatar = u.Avatar
            }).ToList();

            return new OnlinePlayersResponse
            {
                Players = onlineUsers,
                TotalCount = onlineUsers.Count,
                OnlineCount = onlineUsers.Count
            };
        }

        public async Task<OnlinePlayersResponse> SearchPlayersAsync(string query)
        {
            var users = (await _userRepository.SearchUsersByUsernameAsync(query)).Take(20).ToList();
            var dtos = users.Select(u => new OnlinePlayerDto
            {
                Id = u.Id.ToString(),
                Username = u.Username,
                Points = u.Points,
                GamesWon = u.Wins,
                GamesPlayed = u.TotalGames,
                CurrentStreak = u.CurrentWinStreak,
                IsInGame = false,
                LastActive = DateTime.UtcNow,
                Status = "online",
                Avatar = u.Avatar
            }).ToList();

            return new OnlinePlayersResponse
            {
                Players = dtos,
                TotalCount = dtos.Count,
                OnlineCount = dtos.Count
            };
        }

        public async Task<DetailedPlayerStatsDto?> GetDetailedPlayerStatsAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var winRate = user.TotalGames > 0 ? (double)user.Wins / user.TotalGames * 100 : 0;
            var recent = await _pointTransactionRepository.GetRecentForUserAsync(userId, 10);
            var recentTransactions = recent.Select(pt => new PointTransactionDto
            {
                Id = (int)pt.Id,
                Delta = pt.Delta,
                Description = pt.Reason,
                CreatedAt = pt.CreatedAt.UtcDateTime
            }).ToList();

            return new DetailedPlayerStatsDto
            {
                Id = user.Id.ToString(),
                Username = user.Username,
                Points = user.Points,
                GamesPlayed = user.TotalGames,
                GamesWon = user.Wins,
                GamesLost = user.Losses,
                WinRate = Math.Round(winRate, 1),
                CurrentStreak = user.CurrentWinStreak,
                LongestStreak = user.LongestWinStreak,
                LastPlayed = user.LastPlayedAt?.UtcDateTime,
                RecentTransactions = recentTransactions
            };
        }
    }
}
