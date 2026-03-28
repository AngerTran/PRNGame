using System.ComponentModel.DataAnnotations;

namespace Rock_Paper_Scissors_Online.DTOs
{
    public class CreateRoomDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Room name must be between 3 and 50 characters")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(2, 10, ErrorMessage = "Max players must be between 2 and 10")]
        public int MaxPlayers { get; set; } = 2;

        public bool IsPrivate { get; set; } = false;

        [Required]
        [Range(1, 15, ErrorMessage = "Best of rounds must be between 1 and 15")]
        public int BestOfRounds { get; set; } = 3;

        [Required]
        [Range(10, 1000, ErrorMessage = "Points per win must be between 10 and 1000")]
        public int PointsPerWin { get; set; } = 50;

        public bool AllowSpectators { get; set; } = true;
        public bool AllowBetting { get; set; } = true;
    }

    public class JoinRoomDto
    {
        [Required]
        public string RoomId { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
    }

    public class JoinRoomByPinDto
    {
        [Required(ErrorMessage = "PIN code is required")]
        [StringLength(6, MinimumLength = 4, ErrorMessage = "PIN code must be between 4 and 6 characters")]
        [RegularExpression(@"^[A-Z0-9]+$", ErrorMessage = "PIN code can only contain uppercase letters and numbers")]
        public string PinCode { get; set; } = string.Empty;
    }

    public class LeaveRoomDto
    {
        [Required]
        public string RoomId { get; set; } = string.Empty;
    }

    public class JoinAsSpectatorDto
    {
        [Required]
        public string RoomId { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
    }


    public class DeleteRoomDto
    {
        [Required]
        public string RoomId { get; set; } = string.Empty;
    }

    public class RoomResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MaxPlayers { get; set; }
        public int CurrentPlayers { get; set; }
        public bool IsPrivate { get; set; }
        public string? PinCode { get; set; }
        public int BestOfRounds { get; set; }
        public int PointsPerWin { get; set; }
        public bool AllowSpectators { get; set; }
        public bool AllowBetting { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public string CreatedByDisplayName { get; set; } = string.Empty;
        public PlayerDto? Player1 { get; set; }
        public PlayerDto? Player2 { get; set; }
        public List<string> Spectators { get; set; } = new List<string>();
        public int CurrentRound { get; set; }
        public Dictionary<string, int> PlayerScores { get; set; } = new Dictionary<string, int>();
    }

    public class PlayerDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsReady { get; set; } = false;
        public string? CurrentChoice { get; set; }
    }
}
