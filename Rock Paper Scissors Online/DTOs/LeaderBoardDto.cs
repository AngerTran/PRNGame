namespace Rock_Paper_Scissors_Online.DTOs
{
    public class LeaderBoardDto
    {
        public int Rank { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public long Points { get; set; }
        public int gamesWon { get; set; }
        public int gamesPlayed { get; set; } // gameplayed
        public double WinRate { get; set; } // winrate percentage (0-100)
        public int CurrentStreak { get; set; } // current win streak
        public int LongestStreak { get; set; } // longest win streak
    }
    public class Leaderboard
    {
        public IEnumerable<LeaderBoardDto> Entities { get; set; } = new List<LeaderBoardDto>();
        public int TotalPlayers { get; set; }
        public DateTime lastUpdated { get; set; }
    }
    public class LeaderboardPlayerDto
    {
        public int Rank { get; set; }
        public int TotalPlayers { get; set; }
    }
}
