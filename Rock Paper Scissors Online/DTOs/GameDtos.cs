using System.ComponentModel.DataAnnotations;

namespace Rock_Paper_Scissors_Online.DTOs
{
    public class SubmitMoveDto
    {
        [Required]
        [RegularExpression("^(rock|paper|scissors)$", ErrorMessage = "Move must be rock, paper, or scissors")]
        public string Move { get; set; } = string.Empty;
    }

    public class SetReadyDto
    {
        [Required]
        public bool Ready { get; set; }
    }

    public class UpdateProgressDto
    {
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [Required]
        [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
        public int Progress { get; set; }
    }

    public class GameStateDto
    {
        public string Id { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public List<string> Players { get; set; } = new List<string>();
        public string Status { get; set; } = string.Empty;
        public List<RoundDto> Rounds { get; set; } = new List<RoundDto>();
        public int CurrentRound { get; set; }
        public int BestOfRounds { get; set; }
        public Dictionary<string, int> Scores { get; set; } = new Dictionary<string, int>();
        public DateTime CreatedAt { get; set; }
    }

    public class RoundDto
    {
        public int RoundNumber { get; set; }
        public Dictionary<string, string> Moves { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Results { get; set; } = new Dictionary<string, string>();
        public string? Winner { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class GameHistoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Opponent { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public int Rounds { get; set; }
        public int PointsEarned { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class GameHistoryResponseDto
    {
        public List<GameHistoryDto> RecentGames { get; set; } = new List<GameHistoryDto>();
        public int TotalGames { get; set; }
        public double WinRate { get; set; }
    }

    public class MoveSubmissionResponseDto
    {
        public string RoundResult { get; set; } = string.Empty;
        public string? OpponentMove { get; set; }
    }

    public class PlayerReadyStateDto
    {
        public bool Player1Ready { get; set; }
        public bool Player2Ready { get; set; }
    }

    public class GameProgressDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int MaxProgress { get; set; } = 100;
        public Dictionary<string, int> ProgressBar { get; set; } = new Dictionary<string, int>();
    }
}
