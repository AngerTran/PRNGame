using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Repository.Interfaces
{
    public interface IHistoryRepository
    {
        Task<int> CountForUserAsync(Guid userId);
        Task<IReadOnlyList<History>> GetPagedForUserAsync(Guid userId, int skip, int take);
        Task<int> CountByStatusAsync(string status);
        Task<int> SumMaxRoundsAsync();
        Task<int> CountByStatusAndFinishedUtcDateAsync(string status, DateTime utcDate);
    }
}
