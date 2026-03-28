namespace Rock_Paper_Scissors_Online.DTOs
{
    public class BetRequestDto
    {
        public string PlayerId { get; set; } = null!; // Spectator who placed the bet
        public string TargetPlayerId { get; set; } = null!; // Game player they bet on
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
