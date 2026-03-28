using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface ILeaderboardService
    {
        Task<object> GetLeaderboardAsync();
        Task<object?> GetPlayerRankAsync(Guid userId);
        Task<object> GetTopLeaderboardAsync(int take);
    }
}
