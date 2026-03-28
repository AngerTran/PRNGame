using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly IUserRepository _userRepository;

        public LeaderboardService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<object> GetLeaderboardAsync()
        {
            var allUsers = await _userRepository.GetUsersOrderedByPointsDescendingAsync();
            var leaderboard = allUsers
                .Where(u => u.TotalGames > 0)
                .Select(u => new LeaderBoardDto
                {
                    UserId = u.Id,
                    Username = u.Username,
                    Points = u.Points,
                    gamesWon = u.Wins,
                    gamesPlayed = u.TotalGames,
                    WinRate = u.TotalGames > 0 ? Math.Round((double)u.Wins / u.TotalGames * 100, 1) : 0,
                    CurrentStreak = u.CurrentWinStreak,
                    LongestStreak = u.LongestWinStreak,
                })
                .ToList();

            var rank = 1;
            foreach (var row in leaderboard)
            {
                row.Rank = rank++;
            }

            var totalPlayer = await _userRepository.CountAsync();
            return new
            {
                success = true,
                data = new Leaderboard
                {
                    Entities = leaderboard,
                    TotalPlayers = totalPlayer,
                    lastUpdated = DateTime.UtcNow,
                }
            };
        }

        public async Task<object?> GetPlayerRankAsync(Guid userId)
        {
            var ordered = await _userRepository.GetUsersOrderedByPointsDescendingAsync();
            var leaderboardPlayer = ordered.ToList();
            var rank = 1;
            foreach (var user in leaderboardPlayer)
            {
                if (userId == user.Id)
                {
                    return new
                    {
                        sucess = true,
                        data = new LeaderboardPlayerDto
                        {
                            Rank = rank,
                            TotalPlayers = leaderboardPlayer.Count,
                        }
                    };
                }
                rank++;
            }
            return null;
        }

        public async Task<object> GetTopLeaderboardAsync(int take)
        {
            var allUsers = await _userRepository.GetUsersOrderedByPointsDescendingAsync();
            var leaderboard = allUsers
                .Where(u => u.TotalGames > 0)
                .Take(take)
                .Select(u => new LeaderBoardDto
                {
                    UserId = u.Id,
                    Username = u.Username,
                    Points = u.Points,
                    gamesWon = u.Wins,
                    gamesPlayed = u.TotalGames,
                    WinRate = u.TotalGames > 0 ? Math.Round((double)u.Wins / u.TotalGames * 100, 1) : 0,
                    CurrentStreak = u.CurrentWinStreak,
                    LongestStreak = u.LongestWinStreak,
                })
                .ToList();

            var rank = 1;
            foreach (var row in leaderboard)
            {
                row.Rank = rank++;
            }

            var totalPlayer = await _userRepository.CountAsync();
            return new
            {
                success = true,
                data = new Leaderboard
                {
                    Entities = leaderboard,
                    TotalPlayers = totalPlayer,
                    lastUpdated = DateTime.UtcNow,
                }
            };
        }
    }
}
