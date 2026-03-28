using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IMatchHistoryService
    {
        Task<MatchHistoryResponse?> GetMatchHistoryAsync(Guid userId, int page, int pageSize);
        Task<PointTransactionHistoryResponse?> GetPointTransactionsAsync(Guid userId, int page, int pageSize);
    }
}
