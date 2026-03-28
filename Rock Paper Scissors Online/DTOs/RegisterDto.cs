using System.ComponentModel.DataAnnotations;

namespace Rock_Paper_Scissors_Online.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "Password must be between 8 and 128 characters")]
        public string Password { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Display name must not exceed 50 characters")]
        public string? DisplayName { get; set; }

        public int Avatar { get; set; } = 1;
    }
}
