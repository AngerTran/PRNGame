using System;
using System.Collections.Generic;

namespace Rock_Paper_Scissors_Online.Models;

public partial class ChatMessage
{
    public string Id { get; set; } = null!;

    public string RoomId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Type { get; set; } = null!;

    public DateTimeOffset Timestamp { get; set; }
}
