using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/player-stats")]
    [Authorize]
    public class PlayersController : ControllerBase
    {
        private readonly IOnlinePlayersService _onlinePlayersService;

        public PlayersController(IOnlinePlayersService onlinePlayersService)
        {
            _onlinePlayersService = onlinePlayersService;
        }

        [HttpGet("Online")]
        public async Task<ActionResult<ApiResponse<OnlinePlayersResponse>>> GetOnlinePlayers()
        {
            try
            {
                var response = await _onlinePlayersService.GetOnlinePlayersAsync();
                return Ok(new ApiResponse<OnlinePlayersResponse>
                {
                    Success = true,
                    Message = "Online players retrieved successfully",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<OnlinePlayersResponse>
                {
                    Success = false,
                    Message = $"Failed to retrieve online players: {ex.Message}"
                });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<OnlinePlayersResponse>>> SearchPlayers([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new ApiResponse<OnlinePlayersResponse>
                    {
                        Success = false,
                        Message = "Search query is required"
                    });
                }

                var response = await _onlinePlayersService.SearchPlayersAsync(query);
                return Ok(new ApiResponse<OnlinePlayersResponse>
                {
                    Success = true,
                    Message = "Player search completed successfully",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<OnlinePlayersResponse>
                {
                    Success = false,
                    Message = $"Failed to search players: {ex.Message}"
                });
            }
        }

        [HttpGet("{playerId}/stats")]
        public async Task<ActionResult<ApiResponse<DetailedPlayerStatsDto>>> GetPlayerStats(string playerId)
        {
            try
            {
                if (!Guid.TryParse(playerId, out var userId))
                {
                    return BadRequest(new ApiResponse<DetailedPlayerStatsDto>
                    {
                        Success = false,
                        Message = "Invalid player ID format"
                    });
                }

                var stats = await _onlinePlayersService.GetDetailedPlayerStatsAsync(userId);
                if (stats == null)
                {
                    return NotFound(new ApiResponse<DetailedPlayerStatsDto>
                    {
                        Success = false,
                        Message = "Player not found"
                    });
                }

                return Ok(new ApiResponse<DetailedPlayerStatsDto>
                {
                    Success = true,
                    Message = "Player stats retrieved successfully",
                    Data = stats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<DetailedPlayerStatsDto>
                {
                    Success = false,
                    Message = $"Failed to retrieve player stats: {ex.Message}"
                });
            }
        }
    }
}
