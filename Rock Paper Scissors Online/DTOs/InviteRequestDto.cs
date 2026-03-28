namespace Rock_Paper_Scissors_Online.DTOs
{
    public class InviteRequestDto
    {
        public string Message { get; set; } = "Want to play a quick match?";
    }


    public class PlayerStatsDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public long Points { get; set; } = 0;
        public int GamesWon { get; set; } = 0;
        public int GamesPlayed { get; set; } = 0;
        public int CurrentStreak { get; set; } = 0;
        public int LongestStreak { get; set; } = 0;
        public DateTime? LastActive { get; set; } = null;
        //public string Status { get; set; } = "offline";
        public int Avatar { get; set; } = 1;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class PlayerSearchDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        //public int Level { get; set; } = 0;
        public long Points { get; set; } = 0;
        public int GamesWon { get; set; } = 0;
        public int GamesPlayed { get; set; } = 0;
        //public string Status { get; set; } = "offline";
        public int Avatar { get; set; } = 1;
    }


    public class PlayerSearchResponseDto
    {
        public List<PlayerSearchDto> Players { get; set; } = new List<PlayerSearchDto>();
        public int Total { get; set; }
        public string Query { get; set; } = string.Empty;
    }
}
