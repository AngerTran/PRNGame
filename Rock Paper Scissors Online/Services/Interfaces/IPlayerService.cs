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

        /// <summary>
        /// Đồng ý lời mời và tạo phòng đấu mới cho hai người chơi (không lưu vào database lời mời).
        /// </summary>
        Task<(bool Ok, PlayerInvitationDto? Invitation, string? RoomId, string? Error)> AcceptInvitationAndCreateRoomAsync(
            string toPlayerId,
            string inviteId,
            int bestOfRounds = 3,
            int pointsPerWin = 50);

        Task<IEnumerable<OnlinePlayerDetailDto>> SearchPlayersByUsernameAsync(string username);

        Task<PlayerStatsDto?> GetPlayerStatsAsync(string playerId);
    }
}
