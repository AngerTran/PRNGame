using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
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
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                if (!BettingService.AllowedBetStakes.Contains(request.Amount))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Mức cược phải là một trong: {string.Join(", ", BettingService.AllowedBetStakes.OrderBy(x => x))}"
                    });
                }

                var room = await _roomService.GetRoomAsync(roomId);
                if (room == null)
                    return NotFound(new { success = false, message = "Room not found" });

                if (request.TargetPlayerId != room.Player1?.UserId && request.TargetPlayerId != room.Player2?.UserId)
                    return BadRequest(new { success = false, message = "Invalid target player" });

                var betRequest = new BetRequestDto
                {
                    PlayerId = userId,
                    TargetPlayerId = request.TargetPlayerId,
                    Amount = request.Amount,
                    PinCode = request.PinCode
                };

                var betResult = await _bettingService.PlaceBet(roomId, betRequest);

                var bettingPool = _bettingService.GetPool(roomId, room.Player1?.UserId, room.Player2?.UserId);

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
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
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
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var room = await _roomService.GetRoomAsync(roomId);
                if (room == null)
                    return NotFound(new { success = false, message = "Room not found" });

                var bettingPool = _bettingService.GetPool(roomId, room.Player1?.UserId, room.Player2?.UserId);
                if (bettingPool == null)
                    return NotFound(new { success = false, message = "Betting pool not found" });

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
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var room = await _roomService.GetRoomAsync(roomId);
                if (room == null)
                    return NotFound(new { success = false, message = "Room not found" });

                var statistics = _bettingService.GetBettingStatistics(roomId);
                if (statistics == null)
                    return NotFound(new { success = false, message = "Betting statistics not found" });

                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BETTING API] Error getting betting statistics: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>Mức cược hợp lệ cho UI.</summary>
        [HttpGet("allowed-stakes")]
        public IActionResult GetAllowedStakes() =>
            Ok(new { success = true, data = BettingService.AllowedBetStakes.OrderBy(x => x).ToList() });
    }
}
