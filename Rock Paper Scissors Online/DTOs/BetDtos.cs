namespace Rock_Paper_Scissors_Online.DTOs
{
    /// <summary>POST api/v1/Betting/rooms/&#123;id&#125;/place-bet</summary>
    public class PlaceBetRequest
    {
        public decimal Amount { get; set; }
        public string TargetPlayerId { get; set; } = string.Empty;
        /// <summary>Bắt buộc nếu phòng riêng — trùng mã PIN mới được cược.</summary>
        public string? PinCode { get; set; }
    }

    public class BetRequestDto
    {
        public string PlayerId { get; set; } = null!; // Spectator who placed the bet
        public string TargetPlayerId { get; set; } = null!; // Game player they bet on
        public decimal Amount { get; set; }
        /// <summary>So khớp mã PIN phòng khi phòng riêng.</summary>
        public string? PinCode { get; set; }
    }

    public class BetResponseDto
    {
        public string BetId { get; set; } = null!;
        public string GameId { get; set; } = null!;
        public string PlayerId { get; set; } = null!;
        public decimal Amount { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Status { get; set; } = null!;
        public decimal? Payout { get; set; }
    }

    public class ClaimResponseDto
    {
        public decimal Winnings { get; set; }
        public decimal TotalClaimed { get; set; }
    }
}
