using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class Room
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

    public int MaxSpectators { get; set; }

    public bool AllowBetting { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string CreatedByUsername { get; set; } = null!;

    public string CreatedByDisplayName { get; set; } = null!;

    public int CurrentRound { get; set; }

    public DateTimeOffset? TimeoutAt { get; set; }

    public string? GameState { get; set; }

    public virtual ICollection<Bet> Bets { get; set; } = new List<Bet>();
}
