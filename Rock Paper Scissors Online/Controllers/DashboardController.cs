using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

    [HttpGet("{userId}/stats")]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetDashboardStats(Guid userId)
    {
        try
        {
            var stats = await _dashboardService.GetDashboardStatsAsync(userId);
            if (stats == null)
            {
                return NotFound(new ApiResponse<DashboardStatsDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            return Ok(new ApiResponse<DashboardStatsDto>
            {
                Success = true,
                Data = stats,
                Message = "Dashboard stats retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<DashboardStatsDto>
            {
                Success = false,
                Message = $"Error retrieving dashboard stats: {ex.Message}"
            });
        }
    }

    [HttpGet("{userId}/recent-games")]
    public async Task<ActionResult<ApiResponse<RecentGamesDto>>> GetRecentGames(
        Guid userId,
        [FromQuery] int limit = 5,
        [FromQuery] int offset = 0)
    {
        try
        {
            var response = await _dashboardService.GetRecentGamesAsync(userId, limit, offset);
            if (response == null)
            {
                return NotFound(new ApiResponse<RecentGamesDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            return Ok(new ApiResponse<RecentGamesDto>
            {
                Success = true,
                Data = response,
                Message = "Recent games retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<RecentGamesDto>
            {
                Success = false,
                Message = $"Error retrieving recent games: {ex.Message}"
            });
        }
    }

    [HttpGet("{userId}/achievements")]
    public async Task<ActionResult<ApiResponse<AchievementsDto>>> GetAchievements(
        Guid userId,
        [FromQuery] int limit = 5,
        [FromQuery] int offset = 0)
    {
        try
        {
            var response = await _dashboardService.GetAchievementsAsync(userId, limit, offset);
            if (response == null)
            {
                return NotFound(new ApiResponse<AchievementsDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            return Ok(new ApiResponse<AchievementsDto>
            {
                Success = true,
                Data = response,
                Message = "Achievements retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<AchievementsDto>
            {
                Success = false,
                Message = $"Error retrieving achievements: {ex.Message}"
            });
        }
    }
    }
}
