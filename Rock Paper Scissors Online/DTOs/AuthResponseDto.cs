namespace Rock_Paper_Scissors_Online.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = new UserDto();
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public int Avatar { get; set; }
        public long Points { get; set; }
        public string Role { get; set; } = string.Empty;
        public int TotalGames { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Ties { get; set; }
        public int CurrentWinStreak { get; set; }
        public int LongestWinStreak { get; set; }
        public DateTime? LastPlayedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

