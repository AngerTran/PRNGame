using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IProfileService
    {
        Task<CompleteProfileDto?> GetCompleteProfile(Guid userId);
        Task<ProfileStatsDto?> GetStatsDetail(Guid userId);
        Task<GameHistoryResponseDto> GetHistoryPaged(Guid userId, int limit, int offset);
    }
}
