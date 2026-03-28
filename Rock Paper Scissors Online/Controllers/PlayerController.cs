using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/players")]
    public class PlayerController : ControllerBase
    {
        private readonly IPlayerService _playerService;

        public PlayerController(IPlayerService playerService)
        {
            _playerService = playerService;
        }

        [HttpGet("Online")]
        public async Task<IActionResult> GetOnlinePlayer()
        {
            try
            {
                var data = await _playerService.GetOnlinePlayersAndStatsAsync();

                return Ok(new
                {
                    success = true,
                    data = data
                });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi ở đây
                return StatusCode(500, new
                {
                    success = false,
                    message = "An internal server error occurred",
                    error = ex.Message
                });
            }
        }


        [HttpGet("search")]
        public async Task<IActionResult> SearchPlayers([FromQuery] string Name)
        {
            var users = await _playerService.SearchPlayersByUsernameAsync(Name);

            var response = new PlayerSearchResponseDto
            {
                Players = users.Select(u => new PlayerSearchDto
                {
                    Id = u.UserId,
                    Username = u.UserName,
                    Points = u.Point,
                    GamesWon = u.GameWon,
                    GamesPlayed = u.GamePlayed,
                    Avatar = u.Avatar
                }).ToList(),
                Total = users.Count(),
                Query = Name
            };

            return Ok(new { success = true, data = response });
        }



        // GET /api/v1/players/{playerId}/stats
        [HttpGet("{playerId}/stats")]
        public async Task<IActionResult> GetPlayerStats(string playerId)
        {
            var stats = await _playerService.GetPlayerStatsAsync(playerId);
            if (stats == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Player not found"
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    player = stats
                }
            });
        }



        // POST /api/v1/players/:playerId/invite
        [HttpPost("{playerId}/invite")]
        public IActionResult SendInvitation(string playerId, [FromBody] InviteRequestDto request)
        {
            _playerService.SendInvitation(playerId, request);

            return Ok(new
            {
                success = true,
                message = "Invitation sent successfully"
            });
        }

        // GET /api/v1/players/:playerId/invitations
        [HttpGet("{playerId}/invitations")]
        public IActionResult GetInvitations(string playerId)
        {
            var invitations = _playerService.GetInvitations(playerId);

            return Ok(new
            {
                success = true,
                invitations
            });
        }
    }
}
