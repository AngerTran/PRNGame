using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository.Interfaces;

namespace Rock_Paper_Scissors_Online.Repository
{
    public class HistoryRepository : IHistoryRepository
    {
        private readonly ApplicationDbContext _context;

        public HistoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CountForUserAsync(Guid userId)
        {
            return await _context.Histories
                .CountAsync(h => h.CreatorUserId == userId || h.OpponentId == userId);
        }

        public async Task<IReadOnlyList<History>> GetPagedForUserAsync(Guid userId, int skip, int take)
        {
            return await _context.Histories
                .AsNoTracking()
                .Where(h => h.CreatorUserId == userId || h.OpponentId == userId)
                .Include(h => h.CreatorUser)
                .Include(h => h.Opponent)
                .OrderByDescending(h => h.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> CountByStatusAsync(string status)
        {
            return await _context.Histories.CountAsync(h => h.Status == status);
        }

        public async Task<int> SumMaxRoundsAsync()
        {
            return await _context.Histories.Select(h => h.MaxRounds).DefaultIfEmpty().SumAsync();
        }

        public async Task<int> CountByStatusAndFinishedUtcDateAsync(string status, DateTime utcDate)
        {
            var dayStart = NormalizeDateForTimestampWithoutTimeZone(utcDate);
            var dayEnd = dayStart.AddDays(1);
            return await _context.Histories
                .CountAsync(h =>
                    h.Status == status &&
                    h.FinishedAt >= dayStart &&
                    h.FinishedAt < dayEnd);
        }

        private static DateTime NormalizeDateForTimestampWithoutTimeZone(DateTime date) =>
            DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
    }
}
