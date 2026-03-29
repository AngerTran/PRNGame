using AutoMapper;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Ultilities;

/// <summary>Chỉ chủ phòng (CreatedBy) được thấy PIN phòng riêng trong DTO.</summary>
public static class RoomPinRedaction
{
    public static void RedactPrivatePinUnlessHost(RoomResponseDto? dto, string? userId, GameRoom room)
    {
        if (dto == null)
            return;
        if (!room.IsPrivate)
        {
            dto.PinCode = null;
            return;
        }

        if (string.IsNullOrEmpty(userId) || !string.Equals(room.CreatedBy, userId, StringComparison.OrdinalIgnoreCase))
            dto.PinCode = null;
    }

    public static RoomResponseDto CloneWithoutPin(IMapper mapper, GameRoom room)
    {
        var dto = mapper.Map<RoomResponseDto>(room);
        dto.PinCode = null;
        return dto;
    }
}
