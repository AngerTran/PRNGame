using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class PointTransaction
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public long Delta { get; set; }

    public string Reason { get; set; } = null!;

    public string Type { get; set; } = null!;

    public Guid? HistoryId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual History? History { get; set; }

    public virtual User User { get; set; } = null!;
}
