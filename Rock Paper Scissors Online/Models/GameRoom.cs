using Rock_Paper_Scissors_Online.Enums;

namespace Rock_Paper_Scissors_Online.Models;

/// <summary>
/// Phòng chơi trong bộ nhớ (SignalR / RoomService). Khác với <see cref="Room"/> (bản ghi EF).
/// </summary>
public class GameRoom
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int MaxPlayers { get; set; }
    public int CurrentPlayers { get; set; }
    public bool IsPrivate { get; set; }
    public string? PinCode { get; set; }
    public int BestOfRounds { get; set; }
    public int PointsPerWin { get; set; }
    public bool AllowSpectators { get; set; }
    public int MaxSpectators { get; set; } = 10;
    public bool AllowBetting { get; set; }
    public RoomStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string CreatedByUsername { get; set; } = null!;
    public string CreatedByDisplayName { get; set; } = null!;
    public RoomPlayer? Player1 { get; set; }
    public RoomPlayer? Player2 { get; set; }
    public List<string> Spectators { get; set; } = new();
    public int CurrentRound { get; set; }
    public DateTime? TimeoutAt { get; set; }
    public Dictionary<string, int> PlayerScores { get; set; } = new();
    public string? GameState { get; set; }
}

public class RoomPlayer
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public string? CurrentChoice { get; set; }
}
