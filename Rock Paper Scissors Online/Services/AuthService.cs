using AutoMapper;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;
        private readonly ISessionManagementService _sessionManagementService;

        public AuthService(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IJwtService jwtService,
            IMapper mapper,
            ISessionManagementService sessionManagementService)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtService = jwtService;
            _mapper = mapper;
            _sessionManagementService = sessionManagementService;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            // Kiểm tra username đã tồn tại
            if (await _userRepository.UserExistsByUsernameAsync(registerDto.Username))
            {
                return null; // Username đã tồn tại
            }

            // Kiểm tra email đã tồn tại
            if (await _userRepository.UserExistsByEmailAsync(registerDto.Email))
            {
                return null; // Email đã tồn tại
            }

            // Hash password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // Tạo user mới từ RegisterDto bằng AutoMapper
            var user = _mapper.Map<User>(registerDto);
            user.PasswordHash = passwordHash;

            // Lưu user vào database
            await _userRepository.AddAsync(user);

            // Tạo JWT token
            string token = _jwtService.GenerateToken(user);
            string refreshToken = _jwtService.GenerateRefreshToken();

            // Lưu refresh token
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };

            await _refreshTokenRepository.AddAsync(refreshTokenEntity);

            // Tạo response bằng AutoMapper
            var response = _mapper.Map<AuthResponseDto>(user);
            response.Token = token;
            response.RefreshToken = refreshToken;
            response.ExpiresAt = DateTime.UtcNow.AddMinutes(60);

            return response;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            // Tìm user theo username hoặc email
            var user = await _userRepository.GetByUsernameAsync(loginDto.login_credential);

            // Nếu không tìm thấy theo username, thử tìm theo email
            if (user == null)
            {
                user = await _userRepository.GetByEmailAsync(loginDto.login_credential);
            }

            if (user == null)
            {
                return null; // User không tồn tại
            }

            // Kiểm tra password
            if (!BCrypt.Net.BCrypt.Verify(loginDto.password, user.PasswordHash))
            {
                return null; // Sai password
            }

            // Kiểm tra xem user đã đăng nhập từ nơi khác chưa
            var isAlreadyLoggedIn = await _sessionManagementService.IsUserLoggedInAsync(user.Id.ToString());
            if (isAlreadyLoggedIn)
            {
                Console.WriteLine($"[AUTH SERVICE] User {user.Username} ({user.Id}) is already logged in from another location - blocking login");
                return null; // User đã đăng nhập từ nơi khác
            }

            // Cập nhật thời gian login
            user.LastPlayedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            // Tạo JWT token
            string token = _jwtService.GenerateToken(user);
            string refreshToken = _jwtService.GenerateRefreshToken();

            // Xóa các refresh token cũ của user này
            await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id);

            // Lưu refresh token mới
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };

            await _refreshTokenRepository.AddAsync(refreshTokenEntity);

            // Tạo response bằng AutoMapper
            var response = _mapper.Map<AuthResponseDto>(user);
            response.Token = token;
            response.RefreshToken = refreshToken;
            response.ExpiresAt = DateTime.UtcNow.AddMinutes(60);

            return response;
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
        {
            // Tìm refresh token trong database
            var tokenEntity = await _refreshTokenRepository.GetValidTokenAsync(refreshToken);

            if (tokenEntity?.User == null)
            {
                return null; // Refresh token không hợp lệ hoặc đã hết hạn
            }

            var user = tokenEntity.User;

            // Tạo JWT token mới
            string newToken = _jwtService.GenerateToken(user);
            string newRefreshToken = _jwtService.GenerateRefreshToken();

            // Xóa refresh token cũ
            await _refreshTokenRepository.RevokeTokenAsync(refreshToken);

            // Tạo refresh token mới
            var newRefreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(), // Đảm bảo có GUID duy nhất
                Token = newRefreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            };

            await _refreshTokenRepository.AddAsync(newRefreshTokenEntity);

            // Tạo response bằng AutoMapper
            var response = _mapper.Map<AuthResponseDto>(user);
            response.Token = newToken;
            response.RefreshToken = newRefreshToken;
            response.ExpiresAt = DateTime.UtcNow.AddMinutes(60);

            return response;
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            return await _refreshTokenRepository.RevokeTokenAsync(refreshToken);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _userRepository.GetByUsernameAsync(username);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<bool> IsUserAlreadyLoggedInAsync(string userId)
        {
            return await _sessionManagementService.IsUserLoggedInAsync(userId);
        }
    }
}