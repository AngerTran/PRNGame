namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IHealthMetricsService
    {
        Task<int> GetUserCountAsync();
        Task<int> GetHistoryCountByStatusAsync(string status);
        Task<int> GetHistorySumMaxRoundsAsync();
        Task<int> GetNewUsersOnUtcDateAsync(DateTime utcDate);
        Task<int> GetActiveUsersOnUtcDateAsync(DateTime utcDate);
        Task<int> GetFinishedGamesOnUtcDateAsync(DateTime utcDate);
    }
}
