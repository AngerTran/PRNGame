using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [Route("api/v1/user/")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IMapper _mapper;

        public UserController(IAuthService authService, IMapper mapper)
        {
            _authService = authService;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(registerDto);
            if (result == null)
            {
                // Check which field caused the conflict
                var usernameExists = await _authService.GetUserByUsernameAsync(registerDto.Username);
                var emailExists = await _authService.GetUserByEmailAsync(registerDto.Email);

                if (usernameExists != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Username is already taken. Please choose a different username.",
                        errorCode = "USERNAME_EXISTS",
                        details = "The username '" + registerDto.Username + "' is already in use by another account.",
                        field = "username"
                    });
                }
                else if (emailExists != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email address is already registered. Please use a different email or try logging in.",
                        errorCode = "EMAIL_EXISTS",
                        details = "The email address '" + registerDto.Email + "' is already associated with an existing account.",
                        field = "email"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Registration failed due to validation errors. Please check your information and try again.",
                        errorCode = "REGISTRATION_FAILED",
                        details = "Unable to create account. Please ensure all fields are valid and try again."
                    });
                }
            }

            return Ok(new
            {
                message = "Registration successful",
                data = result
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(loginDto);
            if (result == null)
            {
                // Check if it's a duplicate login issue by trying to find the user
                var user = await _authService.GetUserByUsernameAsync(loginDto.login_credential);
                if (user == null)
                {
                    user = await _authService.GetUserByEmailAsync(loginDto.login_credential);
                }

                if (user != null)
                {
                    // Check if user is already logged in
                    var isAlreadyLoggedIn = await _authService.IsUserAlreadyLoggedInAsync(user.Id.ToString());
                    if (isAlreadyLoggedIn)
                    {
                        return Unauthorized(new
                        {
                            success = false,
                            message = "A session of this account is already logged in",
                            errorCode = "DUPLICATE_LOGIN",
                            details = "You can only be logged in from one location at a time."
                        });
                    }
                    else
                    {
                        return Unauthorized(new
                        {
                            success = false,
                            message = "Invalid password. Please check your password and try again.",
                            errorCode = "INVALID_PASSWORD",
                            details = "The password you entered is incorrect."
                        });
                    }
                }
                else
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Account not found. Please check your username/email and try again.",
                        errorCode = "USER_NOT_FOUND",
                        details = "No account exists with the provided username or email address."
                    });
                }
            }

            return Ok(new
            {
                message = "Login successful",
                data = result
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { message = "Refresh token cannot be empty" });
            }

            var result = await _authService.RefreshTokenAsync(request.RefreshToken);
            if (result == null)
            {
                return Unauthorized(new { message = "Invalid or expired refresh token" });
            }

            return Ok(new
            {
                message = "Token refreshed successfully",
                data = result
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _authService.RevokeRefreshTokenAsync(request.RefreshToken);
            }

            return Ok(new { message = "Logout successful" });
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Use AutoMapper to convert User to UserDto
            var userDto = _mapper.Map<UserDto>(user);

            return Ok(new
            {
                message = "User information retrieved successfully",
                data = userDto
            });
        }
    }
}
