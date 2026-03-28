using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto);
        Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
        Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeRefreshTokenAsync(string refreshToken);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByIdAsync(Guid userId);
        Task<bool> IsUserAlreadyLoggedInAsync(string userId);
    }
}