using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Repository.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid userId);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailAsync(string email);
        Task<bool> UserExistsByUsernameAsync(string username);
        Task<bool> UserExistsByEmailAsync(string email);
        Task<User> AddAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<bool> DeleteAsync(Guid userId);
        Task<IEnumerable<User>> GetAllAsync();
        Task<IEnumerable<User>> GetTopPlayersByPointsAsync(int count = 10);
        Task<IEnumerable<User>> GetTopPlayersByWinsAsync(int count = 10);
        Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> ids);
        Task CreateUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task<IEnumerable<User>> SearchUsersByUsernameAsync(string username);
        Task<User?> GetUserStatsByIdAsync(Guid userId);
        Task<int> CountAsync();
        Task<IReadOnlyList<User>> GetUsersOrderedByPointsDescendingAsync();
    }
}
