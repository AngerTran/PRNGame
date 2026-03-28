namespace Rock_Paper_Scissors_Online.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T Data { get; set; } = default!;
    }
    public class ProfileStatsDto
    {
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public long TotalPoints { get; set; }
        public int WinStreak { get; set; }
        public int BestStreak { get; set; }
        public double AverageGameTime { get; set; }
        public DateTime JoinDate { get; set; }
    }

    public class CompleteProfileDto
    {
        public UserDto User { get; set; } = new UserDto();
        public ProfileStatsDto Stats { get; set; } = new ProfileStatsDto();
    }
}
