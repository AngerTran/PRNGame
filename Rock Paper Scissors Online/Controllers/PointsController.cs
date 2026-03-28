using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/points")]
    [Authorize]
    public class PointsController : ControllerBase
    {
        private readonly IPointTransactionService _pointTransactionService;

        public PointsController(IPointTransactionService pointTransactionService)
        {
            _pointTransactionService = pointTransactionService;
        }

        /// <summary>
        /// Get current user's points
        /// GET /api/v1/points/current
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUserPoints()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var points = await _pointTransactionService.GetUserPointsAsync(userId);

                return Ok(new
                {
                    success = true,
                    message = "Points retrieved successfully",
                    data = new
                    {
                        userId = userId,
                        points = points
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[POINTS API] Error getting user points: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve points",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get points for a specific user (for admin or profile viewing)
        /// GET /api/v1/points/{userId}
        /// </summary>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserPoints(string userId)
        {
            try
            {
                if (!Guid.TryParse(userId, out Guid userGuid))
                {
                    return BadRequest(new { message = "Invalid user ID format" });
                }

                var points = await _pointTransactionService.GetUserPointsAsync(userGuid);

                return Ok(new
                {
                    success = true,
                    message = "Points retrieved successfully",
                    data = new
                    {
                        userId = userGuid,
                        points = points
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[POINTS API] Error getting user points for {userId}: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve points",
                    error = ex.Message
                });
            }
        }
    }
}
