using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? DisplayName { get; set; }

    public int Avatar { get; set; }

    public long Points { get; set; }

    public string Role { get; set; } = null!;

    public int TotalGames { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Ties { get; set; }

    public int CurrentWinStreak { get; set; }

    public int LongestWinStreak { get; set; }

    public DateTimeOffset? LastPlayedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<History> HistoryCreatorUsers { get; set; } = new List<History>();

    public virtual ICollection<History> HistoryOpponents { get; set; } = new List<History>();

    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
