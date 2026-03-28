using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository.Interfaces;

namespace Rock_Paper_Scissors_Online.Repository
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly ApplicationDbContext _context;

        public RefreshTokenRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task<RefreshToken?> GetValidTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token && rt.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<RefreshToken> AddAsync(RefreshToken refreshToken)
        {
            if (refreshToken.Id == Guid.Empty)
            {
                refreshToken.Id = Guid.NewGuid();
            }

            refreshToken.CreatedAt = DateTime.UtcNow;

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
            {
                return false;
            }

            _context.RefreshTokens.Remove(refreshToken);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokeAllUserTokensAsync(Guid userId)
        {
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            if (!userTokens.Any())
            {
                return false;
            }

            _context.RefreshTokens.RemoveRange(userTokens);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<RefreshToken>> GetUserTokensAsync(Guid userId)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .OrderByDescending(rt => rt.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteExpiredTokensAsync()
        {
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (!expiredTokens.Any())
            {
                return false;
            }

            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
