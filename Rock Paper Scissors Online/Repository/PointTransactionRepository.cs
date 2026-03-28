using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository.Interfaces;

namespace Rock_Paper_Scissors_Online.Repository
{
    public class PointTransactionRepository : IPointTransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public PointTransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<long> SumPositiveDeltaForUserAsync(Guid userId)
        {
            return await _context.PointTransactions
                .Where(pt => pt.UserId == userId && pt.Delta > 0)
                .SumAsync(pt => pt.Delta);
        }

        public async Task<long> SumNegativeDeltaAbsoluteForUserAsync(Guid userId)
        {
            return await _context.PointTransactions
                .Where(pt => pt.UserId == userId && pt.Delta < 0)
                .SumAsync(pt => -pt.Delta);
        }

        public async Task<int> CountForUserAsync(Guid userId)
        {
            return await _context.PointTransactions.CountAsync(pt => pt.UserId == userId);
        }

        public async Task<IReadOnlyList<PointTransaction>> GetPagedForUserAsync(Guid userId, int skip, int take)
        {
            return await _context.PointTransactions
                .AsNoTracking()
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PointTransaction>> GetRecentForUserAsync(Guid userId, int take)
        {
            return await _context.PointTransactions
                .AsNoTracking()
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Take(take)
                .ToListAsync();
        }
    }
}
