using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IPlayerService
    {
        Task<OnlinePlayerDetailDto> GetOnlinePlayersAndStatsAsync();

        void SendInvitation(string toPlayerId, InviteRequestDto request);
        IEnumerable<InviteRequestDto> GetInvitations(string playerId);
        Task<IEnumerable<OnlinePlayerDetailDto>> SearchPlayersByUsernameAsync(string username);
        Task<PlayerStatsDto?> GetPlayerStatsAsync(string playerId);
    }
}
