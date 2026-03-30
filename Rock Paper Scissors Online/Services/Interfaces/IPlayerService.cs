using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IPlayerService
    {
        Task<OnlinePlayerDetailDto> GetOnlinePlayersAndStatsAsync();

        void SendInvitation(string fromUserId, string fromUsername, string toPlayerId, InviteRequestDto request);

        IReadOnlyList<PlayerInvitationDto> GetPendingInvitations(string toPlayerId);

        bool TryAcceptInvitation(string toPlayerId, string inviteId, out PlayerInvitationDto? invitation);

        bool TryDeclineInvitation(string toPlayerId, string inviteId);

        Task<IEnumerable<OnlinePlayerDetailDto>> SearchPlayersByUsernameAsync(string username);

        Task<PlayerStatsDto?> GetPlayerStatsAsync(string playerId);
    }
}
