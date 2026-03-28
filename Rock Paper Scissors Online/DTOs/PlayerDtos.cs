namespace Rock_Paper_Scissors_Online.DTOs
{
    public class OnlinePlayerDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public long Points { get; set; }
        public int GamesWon { get; set; }
        public int GamesPlayed { get; set; }
        public int CurrentStreak { get; set; }
        public bool IsInGame { get; set; }
        public DateTime LastActive { get; set; }
        public string Status { get; set; } = "online";
        public int Avatar { get; set; }
    }

    public class OnlinePlayersResponse
    {
        public List<OnlinePlayerDto> Players { get; set; } = new();
        public int TotalCount { get; set; }
        public int OnlineCount { get; set; }
    }

    public class DetailedPlayerStatsDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public long Points { get; set; }
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public double WinRate { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastPlayed { get; set; }
        public List<PointTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class PointTransactionDto
    {
        public int Id { get; set; }
        public long Delta { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class MatchHistoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string OpponentUsername { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty; // "win", "loss", "draw"
        public long PointsEarned { get; set; }
        public long PointsLost { get; set; }
        public int RoundsPlayed { get; set; }
        public DateTime PlayedAt { get; set; }
        public int Duration { get; set; } // in seconds
        public string RoomName { get; set; } = string.Empty;
    }

    public class MatchHistoryResponse
    {
        public List<MatchHistoryDto> Matches { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class PointTransactionHistoryResponse
    {
        public List<PointTransactionDto> Transactions { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
