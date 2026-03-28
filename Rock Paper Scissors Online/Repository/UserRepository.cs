using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository.Interfaces;

namespace Rock_Paper_Scissors_Online.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.HistoryCreatorUsers)
                .Include(u => u.HistoryOpponents)
                .Include(u => u.PointTransactions)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.HistoryCreatorUsers)
                .Include(u => u.HistoryOpponents)
                .Include(u => u.PointTransactions)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.HistoryCreatorUsers)
                .Include(u => u.HistoryOpponents)
                .Include(u => u.PointTransactions)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<bool> UserExistsByUsernameAsync(string username)
        {
            return await _context.Users
                .AnyAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<bool> UserExistsByEmailAsync(string email)
        {
            return await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<User> AddAsync(User user)
        {
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> DeleteAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetTopPlayersByPointsAsync(int count = 10)
        {
            return await _context.Users
                .OrderByDescending(u => u.Points)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetTopPlayersByWinsAsync(int count = 10)
        {
            return await _context.Users
                .OrderByDescending(u => u.Wins)
                .ThenByDescending(u => u.Points)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> ids)
        {
            return await _context.Users
                .Where(u => ids.Contains(u.Id))
                .ToListAsync();
        }

        public async Task CreateUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Update(user);
            await _context.SaveChangesAsync();
        }
        public async Task<IEnumerable<User>> SearchUsersByUsernameAsync(string username)
        {
            return await _context.Users
                .Where(u => u.Username.ToLower().Contains(username.ToLower()))
                .ToListAsync();
        }

        public async Task<User?> GetUserStatsByIdAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.HistoryCreatorUsers)
                .Include(u => u.HistoryOpponents)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<int> CountAsync()
        {
            return await _context.Users.CountAsync();
        }

        public async Task<IReadOnlyList<User>> GetUsersOrderedByPointsDescendingAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.Points)
                .ToListAsync();
        }
    }
}
