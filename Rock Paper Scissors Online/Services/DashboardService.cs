using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUserRepository _userRepository;
        private readonly IHistoryRepository _historyRepository;
        private readonly IPointTransactionRepository _pointTransactionRepository;

        public DashboardService(
            IUserRepository userRepository,
            IHistoryRepository historyRepository,
            IPointTransactionRepository pointTransactionRepository)
        {
            _userRepository = userRepository;
            _historyRepository = historyRepository;
            _pointTransactionRepository = pointTransactionRepository;
        }

        public async Task<DashboardStatsDto?> GetDashboardStatsAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var winRate = user.TotalGames > 0 ? (double)user.Wins / user.TotalGames * 100 : 0;
            var pointsEarned = await _pointTransactionRepository.SumPositiveDeltaForUserAsync(userId);
            var pointsLost = await _pointTransactionRepository.SumNegativeDeltaAbsoluteForUserAsync(userId);
            const int averageGameDuration = 120;

            return new DashboardStatsDto
            {
                TotalGames = user.TotalGames,
                GamesWon = user.Wins,
                GamesLost = user.Losses,
                WinRate = Math.Round(winRate, 1),
                CurrentStreak = user.CurrentWinStreak,
                LongestStreak = user.LongestWinStreak,
                TotalPoints = user.Points,
                PointsEarned = (int)pointsEarned,
                PointsLost = (int)pointsLost,
                AverageGameDuration = averageGameDuration,
                LastPlayed = (user.LastPlayedAt ?? user.CreatedAt).UtcDateTime
            };
        }

        public async Task<RecentGamesDto?> GetRecentGamesAsync(Guid userId, int limit, int offset)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var histories = await _historyRepository.GetPagedForUserAsync(userId, offset, limit);
            var totalCount = await _historyRepository.CountForUserAsync(userId);

            var games = histories.Select(h => new RecentGameDto
            {
                Id = h.Id.ToString(),
                Opponent = h.CreatorUserId == userId
                    ? (h.Opponent != null ? h.Opponent.Username : "Unknown")
                    : (h.CreatorUser != null ? h.CreatorUser.Username : "Unknown"),
                Result = h.Status == "completed"
                    ? (h.CreatorUserId == userId ? "win" : "loss")
                    : "draw",
                PointsEarned = h.Points,
                Moves = h.MaxRounds,
                Timestamp = h.CreatedAt.UtcDateTime,
                Duration = 120
            }).ToList();

            return new RecentGamesDto
            {
                Games = games,
                TotalCount = totalCount
            };
        }

        public async Task<AchievementsDto?> GetAchievementsAsync(Guid userId, int limit, int offset)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var achievements = new List<AchievementDto>();

            if (user.Wins >= 1)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "first_win",
                    Title = "FIRST VICTORY",
                    Description = "Win your first game",
                    Rarity = "common",
                    UnlockedAt = user.CreatedAt.AddDays(1).UtcDateTime,
                    Icon = "trophy"
                });
            }

            if (user.LongestWinStreak >= 5)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "streak_5",
                    Title = "HOT STREAK",
                    Description = "Win 5 games in a row",
                    Rarity = "rare",
                    UnlockedAt = user.CreatedAt.AddDays(7).UtcDateTime,
                    Icon = "flame"
                });
            }

            if (user.LongestWinStreak >= 10)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "streak_10",
                    Title = "UNSTOPPABLE",
                    Description = "Win 10 games in a row",
                    Rarity = "epic",
                    UnlockedAt = user.CreatedAt.AddDays(14).UtcDateTime,
                    Icon = "zap"
                });
            }

            if (user.Points >= 1000)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "points_1000",
                    Title = "POINT MASTER",
                    Description = "Earn 1000 points",
                    Rarity = "common",
                    UnlockedAt = user.CreatedAt.AddDays(3).UtcDateTime,
                    Icon = "star"
                });
            }

            if (user.Points >= 5000)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "points_5000",
                    Title = "HIGH ROLLER",
                    Description = "Earn 5000 points",
                    Rarity = "rare",
                    UnlockedAt = user.CreatedAt.AddDays(10).UtcDateTime,
                    Icon = "diamond"
                });
            }

            if (user.TotalGames >= 10)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "games_10",
                    Title = "DEDICATED",
                    Description = "Play 10 games",
                    Rarity = "common",
                    UnlockedAt = user.CreatedAt.AddDays(5).UtcDateTime,
                    Icon = "target"
                });
            }

            if (user.TotalGames >= 50)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "games_50",
                    Title = "VETERAN",
                    Description = "Play 50 games",
                    Rarity = "epic",
                    UnlockedAt = user.CreatedAt.AddDays(20).UtcDateTime,
                    Icon = "shield"
                });
            }

            var winRate = user.TotalGames > 0 ? (double)user.Wins / user.TotalGames * 100 : 0;
            if (winRate >= 80 && user.TotalGames >= 10)
            {
                achievements.Add(new AchievementDto
                {
                    Id = "winrate_80",
                    Title = "LEGENDARY",
                    Description = "Maintain 80% win rate over 10+ games",
                    Rarity = "legendary",
                    UnlockedAt = user.CreatedAt.AddDays(15).UtcDateTime,
                    Icon = "crown"
                });
            }

            var paginated = achievements
                .OrderByDescending(a => a.UnlockedAt)
                .Skip(offset)
                .Take(limit)
                .ToList();

            return new AchievementsDto
            {
                Achievements = paginated,
                TotalCount = achievements.Count
            };
        }
    }
}
