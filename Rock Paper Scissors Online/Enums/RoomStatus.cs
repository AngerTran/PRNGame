using System.Text.Json.Serialization;

namespace Rock_Paper_Scissors_Online.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoomStatus
    {
        Waiting,
        Playing,
        InProgress,
        Completed,
        Finished
    }
}
