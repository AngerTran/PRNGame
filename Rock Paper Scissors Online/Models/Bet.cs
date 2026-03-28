using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class Bet
{
    public string Id { get; set; } = null!;

    public string GameId { get; set; } = null!;

    public string PlayerId { get; set; } = null!;

    public string TargetPlayerId { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Status { get; set; } = null!;

    public decimal? Payout { get; set; }

    public virtual Room? Game { get; set; }
}
