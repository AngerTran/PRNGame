using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [Route("api/v1/profile")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        // GET /api/v1/profile/:userId
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetProfile(string userId)
        {
            if (!Guid.TryParse(userId, out var guid))
                return BadRequest(new { message = "'userId' must be a valid GUID" });

            var profile = await _profileService.GetCompleteProfile(guid);
            if (profile == null)
                return NotFound(new { message = "User not found" });

            return Ok(new ApiResponse<CompleteProfileDto>
            {
                Success = true,
                Message = "Complete profile retrieved successfully",
                Data = profile
            });
        }

        // GET /api/v1/profile/:userId/stats
        [HttpGet("{userId}/stats")]
        public async Task<IActionResult> GetStats(string userId)
        {
            if (!Guid.TryParse(userId, out var guid))
                return BadRequest(new { message = "'userId' must be a valid GUID" });

            var stats = await _profileService.GetStatsDetail(guid);
            if (stats == null) return NotFound(new { message = "User not found" });

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Profile stats retrieved successfully",
                Data = new { stats }
            });
        }

        // GET /api/v1/profile/:userId/history?limit=20&offset=0
        [HttpGet("{userId}/history")]
        public async Task<IActionResult> GetHistory(string userId, [FromQuery] int limit = 20, [FromQuery] int offset = 0)
        {
            if (!Guid.TryParse(userId, out var guid))
                return BadRequest(new { message = "'userId' must be a valid GUID" });

            var history = await _profileService.GetHistoryPaged(guid, limit, offset);

            return Ok(new ApiResponse<GameHistoryResponseDto>
            {
                Success = true,
                Message = "Game history retrieved successfully",
                Data = history
            });
        }
    }
}
