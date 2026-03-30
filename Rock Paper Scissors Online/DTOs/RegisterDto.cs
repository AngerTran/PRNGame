using System.ComponentModel.DataAnnotations;

namespace Rock_Paper_Scissors_Online.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Nhập tên đăng nhập")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhập mật khẩu")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 đến 128 ký tự")]
        public string Password { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Tên hiển thị tối đa 50 ký tự")]
        public string? DisplayName { get; set; }

        public int Avatar { get; set; } = 1;
    }
}
