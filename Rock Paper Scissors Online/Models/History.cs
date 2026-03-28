using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class History
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public Guid? CreatorUserId { get; set; }

    public int MaxRounds { get; set; }

    public int Points { get; set; }

    public string Status { get; set; } = null!;

    public Guid? OpponentId { get; set; }

    public int OpponentScore { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? CreatorUser { get; set; }

    public virtual User? Opponent { get; set; }

    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}
