namespace Rock_Paper_Scissors_Online.DTOs
{
    public class OnlinePlayerDetailDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public long Point { get; set; }
        public int GameWon { get; set; }
        public int GamePlayed { get; set; }
        public int CurrentStreak { get; set; }
        public DateTime? LastActive { get; set; }
        public int Avatar { get; set; }

        public List<OnlinePlayerDetailDto> Players { get; set; } = new List<OnlinePlayerDetailDto>();

        // Sử dụng JsonPropertyName để khớp chính xác tên key trong JSON
        [System.Text.Json.Serialization.JsonPropertyName("total_room")]
        public int TotalRoom { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total_online")]
        public int TotalOnline { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total_connection_inGame")]
        public int TotalConnectionInGame { get; set; }
    }

}
