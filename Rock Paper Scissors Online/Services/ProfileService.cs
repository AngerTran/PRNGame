using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Rock_Paper_Scissors_Online.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IUserRepository _userRepo;
        private readonly ApplicationDbContext _context;

        public ProfileService(IUserRepository userRepo, ApplicationDbContext context)
        {
            _userRepo = userRepo;
            _context = context;
        }

        public async Task<CompleteProfileDto?> GetCompleteProfile(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return null;

            var stats = new ProfileStatsDto
            {
                GamesPlayed = user.TotalGames,
                GamesWon = user.Wins,
                TotalPoints = user.Points,
                WinStreak = user.CurrentWinStreak,
                BestStreak = user.LongestWinStreak,
                AverageGameTime = 45,
                JoinDate = user.CreatedAt.UtcDateTime
            };


            return new CompleteProfileDto
            {
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Avatar = user.Avatar,
                    Points = user.Points,
                    Role = user.Role,
                    TotalGames = user.TotalGames,
                    Wins = user.Wins,
                    Losses = user.Losses,
                    Ties = user.Ties,
                    CurrentWinStreak = user.CurrentWinStreak,
                    LongestWinStreak = user.LongestWinStreak,
                    LastPlayedAt = user.LastPlayedAt?.UtcDateTime,
                    CreatedAt = user.CreatedAt.UtcDateTime
                },
                Stats = stats,
            };
        }

        public async Task<ProfileStatsDto?> GetStatsDetail(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return null;

            return new ProfileStatsDto
            {
                GamesPlayed = user.TotalGames,
                GamesWon = user.Wins,
                TotalPoints = user.Points,
                WinStreak = user.CurrentWinStreak,
                BestStreak = user.LongestWinStreak,
                AverageGameTime = 45, // TODO
                JoinDate = user.CreatedAt.UtcDateTime
            };
        }

        public async Task<GameHistoryResponseDto> GetHistoryPaged(Guid userId, int limit, int offset)
        {
            // Get all histories where the user is either creator or opponent
            var histories = await _context.Histories
                .Include(h => h.CreatorUser)
                .Include(h => h.Opponent)
                .Where(h => h.CreatorUserId == userId || h.OpponentId == userId)
                .OrderByDescending(h => h.StartedAt)
                .ToListAsync();

            var total = histories.Count;
            var wins = histories.Count(h => h.Status == "Completed" && 
                ((h.CreatorUserId == userId && h.OpponentScore < h.Points) || 
                 (h.OpponentId == userId && h.OpponentScore > h.Points)));
            
            var paged = histories.Skip(offset).Take(limit);

            return new GameHistoryResponseDto
            {
                RecentGames = paged.Select(h => new GameHistoryDto
                {
                    Id = h.Id.ToString(),
                    Opponent = h.CreatorUserId == userId ? 
                        (h.Opponent?.Username ?? "Unknown") : 
                        (h.CreatorUser?.Username ?? "Unknown"),
                    Result = h.Status == "Completed" ? 
                        ((h.CreatorUserId == userId && h.OpponentScore < h.Points) || 
                         (h.OpponentId == userId && h.OpponentScore > h.Points)) ? "Win" : "Loss" : 
                        h.Status,
                    Rounds = h.MaxRounds,
                    Timestamp = h.StartedAt.UtcDateTime,
                    PointsEarned = h.CreatorUserId == userId ? h.Points : -h.Points
                }).ToList(),
                TotalGames = total,
                WinRate = total > 0 ? (double)wins / total * 100 : 0.0
            };
        }
    }
}
