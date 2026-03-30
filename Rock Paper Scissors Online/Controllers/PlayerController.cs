using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

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
                    data
                });
            }
            catch (Exception ex)
            {
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

        [Authorize]
        [HttpPost("{targetPlayerId}/invite")]
        public IActionResult SendInvitation(string targetPlayerId, [FromBody] InviteRequestDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập" });

            if (string.Equals(userId, targetPlayerId, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Không thể mời chính mình" });

            try
            {
                _playerService.SendInvitation(userId, username ?? "Player", targetPlayerId, request);
                return Ok(new
                {
                    success = true,
                    message = "Đã gửi lời mời"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("me/invitations")]
        public IActionResult GetMyInvitations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var list = _playerService.GetPendingInvitations(userId);
            return Ok(new { success = true, data = list });
        }

        [Authorize]
        [HttpPost("me/invitations/{inviteId}/accept")]
        public IActionResult AcceptInvitation(string inviteId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!_playerService.TryAcceptInvitation(userId, inviteId, out var inv))
                return NotFound(new { success = false, message = "Không tìm thấy lời mời hoặc đã xử lý" });

            return Ok(new
            {
                success = true,
                message = "Bạn đã đồng ý thi đấu",
                data = inv
            });
        }

        [Authorize]
        [HttpPost("me/invitations/{inviteId}/decline")]
        public IActionResult DeclineInvitation(string inviteId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!_playerService.TryDeclineInvitation(userId, inviteId))
                return NotFound(new { success = false, message = "Không tìm thấy lời mời hoặc đã xử lý" });

            return Ok(new { success = true, message = "Đã từ chối lời mời" });
        }

        /// <summary>Đọc hộp thư lời mời theo playerId (chỉ chính chủ).</summary>
        [Authorize]
        [HttpGet("{playerId}/invitations")]
        public IActionResult GetInvitations(string playerId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!string.Equals(userId, playerId, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var invitations = _playerService.GetPendingInvitations(playerId);

            return Ok(new
            {
                success = true,
                invitations
            });
        }
    }
}
