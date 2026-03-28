using System.ComponentModel.DataAnnotations;

namespace Rock_Paper_Scissors_Online.DTOs
{
    public class SendMessageRequest
    {
        [Required]
        public string UserId { get; set; } = default!;

        [Required]
        [StringLength(20, MinimumLength = 1, ErrorMessage = "Username must be between 1 and 20 characters")]
        public string Username { get; set; } = default!;

        [Required]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 100 characters")]
        public string Content { get; set; } = default!;
    }
}
