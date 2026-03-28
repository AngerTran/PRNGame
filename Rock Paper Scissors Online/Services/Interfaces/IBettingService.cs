using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IBettingService
    {
        Task<BetResponseDto> PlaceBet(string gameId, BetRequestDto request);
        object GetPool(string gameId, string? player1Id = null, string? player2Id = null);
        object GetUserBets(string userId);
        Task<ClaimResponseDto> ClaimWinnings(string gameId, string winnerId);
        object GetBettingStatistics(string gameId);
        Task<List<Bet>> GetBetsForRoomAsync(string roomId);
    }
}
