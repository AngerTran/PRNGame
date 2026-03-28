namespace Rock_Paper_Scissors_Online.DTOs
{
    public class ActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public List<string> Participants { get; set; } = new List<string>();
    }

    public class ActivityFeedResponseDto
    {
        public List<ActivityDto> Activities { get; set; } = new List<ActivityDto>();
        public bool HasMore { get; set; }
    }
}
