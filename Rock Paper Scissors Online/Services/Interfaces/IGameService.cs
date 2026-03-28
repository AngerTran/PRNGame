using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IGameService
    {
        Task<GameStateDto> GetGameStateAsync(string gameId);
        Task<MoveSubmissionResponseDto> SubmitMoveAsync(string gameId, string playerId, string move);
        Task<bool> SetPlayerReadyAsync(string gameId, string playerId, bool ready);
        Task<bool> StartGameAsync(string gameId);
        Task<bool> UpdateProgressAsync(string gameId, string playerId, int progress);
        Task<GameHistoryResponseDto> GetGameHistoryAsync(string userId, int limit = 20, int offset = 0);
        Task<PlayerReadyStateDto> GetPlayerReadyStatesAsync(string gameId);
    }
}
