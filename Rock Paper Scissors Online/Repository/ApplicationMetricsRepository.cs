using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository.Interfaces;

namespace Rock_Paper_Scissors_Online.Repository{
    public class ApplicationMetricsRepository : IApplicationMetricsRepository
    {
        private readonly ApplicationDbContext _context;

        public ApplicationMetricsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<int> GetUserCountAsync() => _context.Users.CountAsync();

        public Task<int> GetHistoryCountByStatusAsync(string status) =>
            _context.Histories.CountAsync(h => h.Status == status);

        public Task<int> GetHistorySumMaxRoundsAsync() =>
            _context.Histories.Select(h => h.MaxRounds).DefaultIfEmpty().SumAsync();

        public Task<int> GetUserCountCreatedOnUtcDateAsync(DateTime utcDate) =>
            _context.Users.CountAsync(u => u.CreatedAt.Date == utcDate.Date);

        public Task<int> GetUserCountLastPlayedOnUtcDateAsync(DateTime utcDate) =>
            _context.Users.CountAsync(u => u.LastPlayedAt.HasValue && u.LastPlayedAt.Value.Date == utcDate.Date);

        public Task<int> GetHistoryCountFinishedOnUtcDateAsync(DateTime utcDate) =>
            _context.Histories.CountAsync(h => h.Status == "Finished" && h.FinishedAt.Date == utcDate.Date);
    }
}
