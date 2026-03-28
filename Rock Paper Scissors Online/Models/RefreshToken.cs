using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
