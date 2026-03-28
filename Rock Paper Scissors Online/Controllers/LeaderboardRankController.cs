using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    public class LeaderboardRankController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardRankController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        [HttpGet("/api/v1/leaderboard")]
        public async Task<ActionResult<IEnumerable<LeaderBoardDto>>> GetDataLeaderboard()
        {
            var response = await _leaderboardService.GetLeaderboardAsync();
            return Ok(response);
        }

        [HttpGet("/api/v1/leaderboard/player/{playerId:guid}/rank")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<LeaderBoardDto>>> GetDataPlayer(Guid playerId)
        {
            var userIdClaims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaims == null || !Guid.TryParse(userIdClaims.Value, out Guid userId))
            {
                return Unauthorized(new { message = "Invalid Token" });
            }

            if (playerId != userId)
                return Forbid();

            var result = await _leaderboardService.GetPlayerRankAsync(userId);
            if (result == null)
            {
                return NotFound(new { message = "User not found in leaderboard" });
            }

            return Ok(result);
        }

        [HttpGet("/api/v1/leaderboard/top")]
        public async Task<ActionResult<IEnumerable<LeaderBoardDto>>> GetTop5Ranking()
        {
            var response = await _leaderboardService.GetTopLeaderboardAsync(5);
            return Ok(response);
        }
    }
}
