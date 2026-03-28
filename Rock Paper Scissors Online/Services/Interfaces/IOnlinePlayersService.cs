using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IOnlinePlayersService
    {
        Task<OnlinePlayersResponse> GetOnlinePlayersAsync();
        Task<OnlinePlayersResponse> SearchPlayersAsync(string query);
        Task<DetailedPlayerStatsDto?> GetDetailedPlayerStatsAsync(Guid userId);
    }
}
