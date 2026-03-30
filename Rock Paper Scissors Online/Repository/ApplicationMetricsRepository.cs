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

        public Task<int> GetUserCountCreatedOnUtcDateAsync(DateTime utcDate)
        {
            var dayStart = NormalizeDateForTimestampWithoutTimeZone(utcDate);
            var dayEnd = dayStart.AddDays(1);
            return _context.Users.CountAsync(u => u.CreatedAt >= dayStart && u.CreatedAt < dayEnd);
        }

        public Task<int> GetUserCountLastPlayedOnUtcDateAsync(DateTime utcDate)
        {
            var dayStart = NormalizeDateForTimestampWithoutTimeZone(utcDate);
            var dayEnd = dayStart.AddDays(1);
            return _context.Users.CountAsync(u =>
                u.LastPlayedAt.HasValue &&
                u.LastPlayedAt.Value >= dayStart &&
                u.LastPlayedAt.Value < dayEnd);
        }

        public Task<int> GetHistoryCountFinishedOnUtcDateAsync(DateTime utcDate)
        {
            var dayStart = NormalizeDateForTimestampWithoutTimeZone(utcDate);
            var dayEnd = dayStart.AddDays(1);
            return _context.Histories.CountAsync(h =>
                h.Status == "Finished" &&
                h.FinishedAt >= dayStart &&
                h.FinishedAt < dayEnd);
        }

        private static DateTime NormalizeDateForTimestampWithoutTimeZone(DateTime date) =>
            DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
    }
}
