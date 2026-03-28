using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class HealthMetricsService : IHealthMetricsService
    {
        private readonly IApplicationMetricsRepository _metrics;

        public HealthMetricsService(IApplicationMetricsRepository metrics)
        {
            _metrics = metrics;
        }

        public Task<int> GetUserCountAsync() => _metrics.GetUserCountAsync();

        public Task<int> GetHistoryCountByStatusAsync(string status) =>
            _metrics.GetHistoryCountByStatusAsync(status);

        public Task<int> GetHistorySumMaxRoundsAsync() =>
            _metrics.GetHistorySumMaxRoundsAsync();

        public Task<int> GetNewUsersOnUtcDateAsync(DateTime utcDate) =>
            _metrics.GetUserCountCreatedOnUtcDateAsync(utcDate);

        public Task<int> GetActiveUsersOnUtcDateAsync(DateTime utcDate) =>
            _metrics.GetUserCountLastPlayedOnUtcDateAsync(utcDate);

        public Task<int> GetFinishedGamesOnUtcDateAsync(DateTime utcDate) =>
            _metrics.GetHistoryCountFinishedOnUtcDateAsync(utcDate);
    }
}
