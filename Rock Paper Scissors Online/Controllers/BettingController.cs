using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BettingController : ControllerBase
    {
        private readonly IBettingService _bettingService;
        private readonly IRoomService _roomService;
        private readonly IHubContext<GameHub> _gameHubContext;

        public BettingController(IBettingService bettingService, IRoomService roomService, IHubContext<GameHub> gameHubContext)
        {
            _bettingService = bettingService;
            _roomService = roomService;
            _gameHubContext = gameHubContext;
        }

        [HttpPost("rooms/{roomId}/place-bet")]
        public async Task<IActionResult> PlaceBet(string roomId, [FromBody] PlaceBetRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[BETTING API] Placing bet for room {roomId}, user {userId}, amount: {request.Amount}, target: {request.TargetPlayerId}");

                // Validate bet amount
                if (request.Amount <= 0)
                {
                    return BadRequest(new { success = false, message = "Bet amount must be greater than 0" });
                }

                // Get room details
                var room = await _roomService.GetRoomAsync(roomId);
                if (room == null)
                {
                    return NotFound(new { success = false, message = "Room not found" });
                }

                // Validate target player
                if (request.TargetPlayerId != room.Player1?.UserId && request.TargetPlayerId != room.Player2?.UserId)
                {
                    return BadRequest(new { success = false, message = "Invalid target player" });
                }

                // Place bet via service
                var betRequest = new BetRequestDto
                {
                    PlayerId = userId, // Spectator who placed the bet
                    TargetPlayerId = request.TargetPlayerId // Game player they bet on
                    // Amount is now fixed to room's PointsPerWin
                };
                var betResult = await _bettingService.PlaceBet(roomId, betRequest);
                // Note: BetResponseDto doesn't have Success/Message properties, so we'll assume success if no exception

                // Get updated betting pool
                var bettingPool = _bettingService.GetPool(roomId, room.Player1?.UserId, room.Player2?.UserId);

                // Broadcast bet placed via SignalR
                var betData = new
                {
                    success = true,
                    message = "Bet placed successfully",
                    data = new
                    {
                        roomId = roomId,
                        betId = betResult.BetId,
                        userId = userId,
                        amount = request.Amount,
                        targetPlayerId = request.TargetPlayerId,
                        timestamp = DateTime.UtcNow,
                        bettingPool = bettingPool
                    }
                };

                await _gameHubContext.Clients.Group(roomId).SendAsync("BetPlaced", betData);
                Console.WriteLine($"[BETTING API] Bet placed and broadcasted to room {roomId}");

                // Also broadcast updated betting pool
                await _gameHubContext.Clients.Group(roomId).SendAsync("BettingPoolUpdated", new
                {
                    success = true,
                    message = "Betting pool updated",
                    data = new
                    {
                        roomId = roomId,
                        bettingPool = bettingPool
                    }
                });

                return Ok(new { success = true, message = "Bet placed successfully", data = betData.data });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BETTING API] Error placing bet: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("rooms/{roomId}/pool")]
        public async Task<IActionResult> GetBettingPool(string roomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[BETTING API] Getting betting pool for room {roomId}");

                // Get room details
                var room = await _roomService.GetRoomAsync(roomId);
                if (room == null)
                {
                    return NotFound(new { success = false, message = "Room not found" });
                }

                // Get betting pool via service with actual player IDs
                var bettingPool = _bettingService.GetPool(roomId, room.Player1?.UserId, room.Player2?.UserId);
                if (bettingPool == null)
                {
                    return NotFound(new { success = false, message = "Betting pool not found" });
                }

                return Ok(new { success = true, data = bettingPool });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BETTING API] Error getting betting pool: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("rooms/{roomId}/statistics")]
        public async Task<IActionResult> GetBettingStatistics(string roomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[BETTING API] Getting betting statistics for room {roomId}");

                // Get room details
                var room = await _roomService.GetRoomAsync(roomId);
                if (room == null)
                {
                    return NotFound(new { success = false, message = "Room not found" });
                }

                // Get betting statistics via service
                var statistics = _bettingService.GetBettingStatistics(roomId);
                if (statistics == null)
                {
                    return NotFound(new { success = false, message = "Betting statistics not found" });
                }

                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BETTING API] Error getting betting statistics: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }

    // DTOs for the Betting API
    public class PlaceBetRequest
    {
        public decimal Amount { get; set; }
        public string TargetPlayerId { get; set; } = string.Empty;
    }
}
