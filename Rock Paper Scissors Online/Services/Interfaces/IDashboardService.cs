using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto?> GetDashboardStatsAsync(Guid userId);
        Task<RecentGamesDto?> GetRecentGamesAsync(Guid userId, int limit, int offset);
        Task<AchievementsDto?> GetAchievementsAsync(Guid userId, int limit, int offset);
    }
}
