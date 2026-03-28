using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/room-management")]
    [Authorize]
    public class RoomManagementController : ControllerBase
    {
        private readonly IEnhancedRoomManagementService _roomManagementService;

        public RoomManagementController(IEnhancedRoomManagementService roomManagementService)
        {
            _roomManagementService = roomManagementService;
        }

        /// <summary>
        /// Get comprehensive room management statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetRoomManagementStats()
        {
            try
            {
                var stats = await _roomManagementService.GetRoomManagementStatsAsync();
                return Ok(new
                {
                    success = true,
                    data = stats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to get room management stats",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get rooms by status
        /// </summary>
        [HttpGet("rooms/by-status/{status}")]
        public async Task<IActionResult> GetRoomsByStatus(string status)
        {
            try
            {
                if (!Enum.TryParse<RoomStatus>(status, true, out var roomStatus))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid room status"
                    });
                }

                var rooms = await _roomManagementService.GetRoomsByStatusAsync(roomStatus);
                return Ok(new
                {
                    success = true,
                    data = new { rooms, count = rooms.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to get rooms by status",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get rooms created by a specific user
        /// </summary>
        [HttpGet("rooms/by-creator/{creatorId}")]
        public async Task<IActionResult> GetRoomsByCreator(string creatorId)
        {
            try
            {
                var rooms = await _roomManagementService.GetRoomsByCreatorAsync(creatorId);
                return Ok(new
                {
                    success = true,
                    data = new { rooms, count = rooms.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to get rooms by creator",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Clean up inactive rooms
        /// </summary>
        [HttpPost("cleanup/inactive")]
        public async Task<IActionResult> CleanupInactiveRooms()
        {
            try
            {
                var result = await _roomManagementService.CleanupInactiveRoomsAsync();
                return Ok(new
                {
                    success = result,
                    message = result ? "Inactive rooms cleaned up successfully" : "Failed to clean up inactive rooms"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to clean up inactive rooms",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Force close a room
        /// </summary>
        [HttpPost("rooms/{roomId}/force-close")]
        public async Task<IActionResult> ForceCloseRoom(string roomId, [FromBody] ForceCloseRequest request)
        {
            try
            {
                var result = await _roomManagementService.ForceCloseRoomAsync(roomId, request.Reason);
                return Ok(new
                {
                    success = result,
                    message = result ? "Room force closed successfully" : "Failed to force close room"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to force close room",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get rooms with low activity
        /// </summary>
        [HttpGet("rooms/low-activity")]
        public async Task<IActionResult> GetRoomsWithLowActivity([FromQuery] int minutesThreshold = 30)
        {
            try
            {
                var rooms = await _roomManagementService.GetRoomsWithLowActivityAsync(minutesThreshold);
                return Ok(new
                {
                    success = true,
                    data = new { rooms, count = rooms.Count, threshold = minutesThreshold }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to get rooms with low activity",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get room health status
        /// </summary>
        [HttpGet("rooms/{roomId}/health")]
        public async Task<IActionResult> GetRoomHealthStatus(string roomId)
        {
            try
            {
                var healthStatus = await _roomManagementService.GetRoomHealthStatusAsync(roomId);
                return Ok(new
                {
                    success = true,
                    data = healthStatus
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to get room health status",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Transfer room ownership
        /// </summary>
        [HttpPost("rooms/{roomId}/transfer-ownership")]
        public async Task<IActionResult> TransferRoomOwnership(string roomId, [FromBody] TransferOwnershipRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated"
                    });
                }

                var result = await _roomManagementService.TransferRoomOwnershipAsync(roomId, userId, request.ToUserId);
                return Ok(new
                {
                    success = result,
                    message = result ? "Room ownership transferred successfully" : "Failed to transfer room ownership"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to transfer room ownership",
                    error = ex.Message
                });
            }
        }
    }

    public class ForceCloseRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class TransferOwnershipRequest
    {
        public string ToUserId { get; set; } = string.Empty;
    }
}
