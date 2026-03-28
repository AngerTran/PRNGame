namespace Rock_Paper_Scissors_Online.Repository.Interfaces
{
    public interface IApplicationMetricsRepository
    {
        Task<int> GetUserCountAsync();
        Task<int> GetHistoryCountByStatusAsync(string status);
        Task<int> GetHistorySumMaxRoundsAsync();
        Task<int> GetUserCountCreatedOnUtcDateAsync(DateTime utcDate);
        Task<int> GetUserCountLastPlayedOnUtcDateAsync(DateTime utcDate);
        Task<int> GetHistoryCountFinishedOnUtcDateAsync(DateTime utcDate);
    }
}
