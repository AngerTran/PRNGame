using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Repository.Interfaces
{
    public interface IPointTransactionRepository
    {
        Task<long> SumPositiveDeltaForUserAsync(Guid userId);
        Task<long> SumNegativeDeltaAbsoluteForUserAsync(Guid userId);
        Task<int> CountForUserAsync(Guid userId);
        Task<IReadOnlyList<PointTransaction>> GetPagedForUserAsync(Guid userId, int skip, int take);
        Task<IReadOnlyList<PointTransaction>> GetRecentForUserAsync(Guid userId, int take);
    }
}
