namespace Rock_Paper_Scissors_Online.DTOs
{
    public class DashboardStatsDto
    {
        public int TotalGames { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public double WinRate { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public long TotalPoints { get; set; }
        public int PointsEarned { get; set; }
        public int PointsLost { get; set; }
        public int AverageGameDuration { get; set; }
        public DateTime LastPlayed { get; set; }
    }

    public class RecentGameDto
    {
        public string Id { get; set; } = null!;
        public string Opponent { get; set; } = null!;
        public string Result { get; set; } = null!; // "win", "loss", "draw"
        public int PointsEarned { get; set; }
        public int Moves { get; set; }
        public DateTime Timestamp { get; set; }
        public int Duration { get; set; } // in seconds
    }

    public class RecentGamesDto
    {
        public List<RecentGameDto> Games { get; set; } = new List<RecentGameDto>();
        public int TotalCount { get; set; }
    }

    public class AchievementDto
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Rarity { get; set; } = null!; // "common", "rare", "epic", "legendary"
        public DateTime UnlockedAt { get; set; }
        public string Icon { get; set; } = null!;
    }

    public class AchievementsDto
    {
        public List<AchievementDto> Achievements { get; set; } = new List<AchievementDto>();
        public int TotalCount { get; set; }
    }
}
