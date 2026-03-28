using System.ComponentModel.DataAnnotations;

namespace Rock_Paper_Scissors_Online.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Username or email is required")]
        public string login_credential { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string password { get; set; } = string.Empty;
    }
}
