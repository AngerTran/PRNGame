using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Repository.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken?> GetValidTokenAsync(string token);
        Task<RefreshToken> AddAsync(RefreshToken refreshToken);
        Task<bool> RevokeTokenAsync(string token);
        Task<bool> RevokeAllUserTokensAsync(Guid userId);
        Task<IEnumerable<RefreshToken>> GetUserTokensAsync(Guid userId);
        Task<bool> DeleteExpiredTokensAsync();
    }
}
