using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using System.Security.Claims;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/rooms")]
    [Authorize]
    public class RoomsController : ControllerBase
    {
        private readonly IRoomService _roomService;
        private readonly IMapper _mapper;
        private readonly IHubContext<GameHub> _gameHubContext;
        private readonly ISessionManagementService _sessionManagementService;

        public RoomsController(IRoomService roomService, IMapper mapper, IHubContext<GameHub> gameHubContext, ISessionManagementService sessionManagementService)
        {
            _roomService = roomService;
            _mapper = mapper;
            _gameHubContext = gameHubContext;
            _sessionManagementService = sessionManagementService;
        }

        /// <summary>
        /// Create a new game room
        /// </summary>
        /// <param name="createRoomDto">Room creation data</param>
        /// <returns>Created room information</returns>
        [HttpPost]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto createRoomDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid request data",
                        errors = ModelState.Where(x => x.Value?.Errors.Count > 0)
                            .ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                            )
                    });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                var displayName = User.FindFirst("DisplayName")?.Value ?? username ?? "Unknown User";

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated"
                    });
                }

                // Check if room name already exists
                if (await _roomService.RoomExistsAsync(createRoomDto.Name))
                {
                    return Conflict(new
                    {
                        success = false,
                        message = "Room with this name already exists"
                    });
                }

                // Create room
                var room = await _roomService.CreateRoomAsync(
                    createRoomDto.Name,
                    createRoomDto.BestOfRounds,
                    createRoomDto.PointsPerWin,
                    userId,
                    username,
                    displayName,
                    createRoomDto.IsPrivate,
                    createRoomDto.MaxPlayers,
                    createRoomDto.AllowSpectators,
                    createRoomDto.AllowBetting
                );

                var roomResponse = _mapper.Map<RoomResponseDto>(room);
                roomResponse.PinCode = room.PinCode;

                Console.WriteLine($"\u001b[34m[ROOM CREATION]\u001b[0m Room '{room.Name}' created by {username}");

                // Broadcast room list update via SignalR
                await BroadcastRoomListUpdate();

                return Ok(new
                {
                    success = true,
                    message = "Room created successfully",
                    data = new { room = roomResponse, userRole = "player" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all available rooms
        /// </summary>
        /// <returns>List of available rooms</returns>
        [HttpGet]
        public async Task<IActionResult> GetRooms()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var rooms = await _roomService.GetAllRoomsAsync();
                var roomResponses = _mapper.Map<List<RoomResponseDto>>(rooms);

                // Show PIN codes to all authenticated users
                foreach (var roomResponse in roomResponses)
                {
                    if (string.IsNullOrEmpty(userId))
                    {
                        roomResponse.PinCode = null;
                    }
                    // For authenticated users, always show PIN codes
                }

                return Ok(new
                {
                    success = true,
                    message = "Rooms retrieved successfully",
                    data = new { rooms = roomResponses }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get a specific room by ID
        /// </summary>
        /// <param name="roomId">Room ID</param>
        /// <returns>Room information</returns>
        [HttpGet("{roomId}")]
        public async Task<IActionResult> GetRoom(string roomId)
        {
            try
            {
                var room = await _roomService.GetRoomAsync(roomId);

                if (room == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Room not found"
                    });
                }

                var roomResponse = _mapper.Map<RoomResponseDto>(room);

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Show PIN codes to all authenticated users
                if (string.IsNullOrEmpty(userId))
                {
                    roomResponse.PinCode = null;
                }
                // For authenticated users, always show PIN codes

                return Ok(new
                {
                    success = true,
                    message = "Room retrieved successfully",
                    data = new { room = roomResponse, userRole = "player" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get room details for validation (lightweight)
        /// </summary>
        /// <param name="roomId">Room ID</param>
        /// <returns>Basic room information for validation</returns>
        [HttpGet("{roomId}/details")]
        public async Task<IActionResult> GetRoomDetails(string roomId)
        {
            try
            {
                var room = await _roomService.GetRoomAsync(roomId);

                if (room == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Room not found"
                    });
                }

                // Return only essential room details for validation
                var roomDetails = new
                {
                    id = room.Id,
                    name = room.Name,
                    pointsPerWin = room.PointsPerWin,
                    maxPlayers = room.MaxPlayers,
                    currentPlayers = room.CurrentPlayers,
                    allowSpectators = room.AllowSpectators,
                    allowBetting = room.AllowBetting,
                    isPrivate = room.IsPrivate,
                    status = room.Status.ToString()
                };

                return Ok(new
                {
                    success = true,
                    message = "Room details retrieved successfully",
                    data = new { room = roomDetails }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Join a room by room ID
        /// </summary>
        /// <param name="joinRoomDto">Join room data</param>
        /// <returns>Joined room information</returns>
        [HttpPost("join")]
        public async Task<IActionResult> JoinRoom([FromBody] JoinRoomDto joinRoomDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated"
                    });
                }

                // Get room details for point validation
                var roomDetails = await _roomService.GetRoomAsync(joinRoomDto.RoomId);
                if (roomDetails == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Room not found"
                    });
                }

                // TODO: Add user point validation here
                // This would require injecting IUserRepository and checking user points
                // For now, we'll add a comment indicating where this should go

                var (success, message, room) = await _roomService.JoinRoomAsync(joinRoomDto.RoomId, userId, username, joinRoomDto.PinCode);

                if (!success)
                {
                    Console.WriteLine($"[ROOM JOIN] Failed to join room {joinRoomDto.RoomId}: {message}");
                    return BadRequest(new
                    {
                        success = false,
                        message = message
                    });
                }

                var roomResponse = _mapper.Map<RoomResponseDto>(room);
                Console.WriteLine($"[ROOM JOIN] User {username} joined room '{room?.Name}' (ID: {room?.Id})");

                // Send PlayerJoined event to clients in the specific room only
                await _gameHubContext.Clients.Group(room.Id).SendAsync("PlayerJoined", new
                {
                    success = true,
                    message = $"{username} joined the room",
                    data = new { room = roomResponse }
                });

                // Broadcast room list update via SignalR
                await BroadcastRoomListUpdate();

                return Ok(new
                {
                    success = true,
                    message = message,
                    data = new { room = roomResponse, userRole = "player" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Join a private room by pin code
        /// </summary>
        /// <param name="joinRoomByPinDto">Join by pin data</param>
        /// <returns>Joined room information</returns>
        [HttpPost("join-by-pin")]
        public async Task<IActionResult> JoinRoomByPin([FromBody] JoinRoomByPinDto joinRoomByPinDto)
        {
            try
            {
                // Validate request model
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    Console.WriteLine($"[ROOM JOIN BY PIN] Validation failed: {string.Join(", ", errors)}");
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid PIN code format",
                        errors = errors
                    });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    Console.WriteLine($"[ROOM JOIN BY PIN] User not authenticated");
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated"
                    });
                }

                // Find room by PIN code
                var roomDetails = await _roomService.GetRoomByPinAsync(joinRoomByPinDto.PinCode);
                if (roomDetails == null)
                {
                    Console.WriteLine($"[ROOM JOIN BY PIN] Room not found for PIN: {joinRoomByPinDto.PinCode}");
                    return NotFound(new
                    {
                        success = false,
                        message = "Room not found for this PIN code"
                    });
                }

                Console.WriteLine($"[ROOM JOIN BY PIN] Found room {roomDetails.Id} for PIN: {joinRoomByPinDto.PinCode}");
                Console.WriteLine($"[ROOM JOIN BY PIN] Room details - CurrentPlayers: {roomDetails.CurrentPlayers}, MaxPlayers: {roomDetails.MaxPlayers}, AllowSpectators: {roomDetails.AllowSpectators}");

                // Check if room has space for players first
                if (roomDetails.CurrentPlayers >= roomDetails.MaxPlayers)
                {
                    // Room is full, try to join as spectator
                    if (roomDetails.AllowSpectators)
                    {
                        Console.WriteLine($"[ROOM JOIN BY PIN] Room is full, attempting to join as spectator");
                        var (spectatorSuccess, spectatorMessage, spectatorRoom) = await _roomService.JoinAsSpectatorAsync(roomDetails.Id, userId, username, joinRoomByPinDto.PinCode);

                        if (spectatorSuccess)
                        {
                            var roomResponse = _mapper.Map<RoomResponseDto>(spectatorRoom);
                            Console.WriteLine($"[ROOM JOIN BY PIN] User {username} joined room '{spectatorRoom?.Name}' as spectator (ID: {spectatorRoom?.Id})");

                            // Send SpectatorJoined event to clients in the specific room only
                            await _gameHubContext.Clients.Group(spectatorRoom.Id).SendAsync("SpectatorJoined", new
                            {
                                success = true,
                                message = $"{username} joined the room as spectator",
                                data = new { room = roomResponse }
                            });

                            // Broadcast room list update via SignalR
                            await BroadcastRoomListUpdate();

                            return Ok(new
                            {
                                success = true,
                                message = "Joined room as spectator (room was full)",
                                data = new { room = roomResponse, userRole = "spectator" }
                            });
                        }
                        else
                        {
                            Console.WriteLine($"[ROOM JOIN BY PIN] Failed to join as spectator: {spectatorMessage}");
                            return BadRequest(new
                            {
                                success = false,
                                message = spectatorMessage
                            });
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ROOM JOIN BY PIN] Room is full and does not allow spectators");
                        return BadRequest(new
                        {
                            success = false,
                            message = "Room is full and does not allow spectators"
                        });
                    }
                }
                else
                {
                    // Room has space, try to join as player
                    var (success, message, room) = await _roomService.JoinRoomAsync(roomDetails.Id, userId, username, joinRoomByPinDto.PinCode);

                    if (!success)
                    {
                        Console.WriteLine($"[ROOM JOIN BY PIN] Failed to join room: {message}");
                        return BadRequest(new
                        {
                            success = false,
                            message = message
                        });
                    }

                    var playerRoomResponse = _mapper.Map<RoomResponseDto>(room);
                    Console.WriteLine($"[ROOM JOIN BY PIN] User {username} joined room '{room?.Name}' as player (ID: {room?.Id})");

                    // Send PlayerJoined event to clients in the specific room only
                    await _gameHubContext.Clients.Group(room.Id).SendAsync("PlayerJoined", new
                    {
                        success = true,
                        message = $"{username} joined the room",
                        data = new { room = playerRoomResponse }
                    });

                    // Broadcast room list update via SignalR
                    await BroadcastRoomListUpdate();

                    return Ok(new
                    {
                        success = true,
                        message = message,
                        data = new { room = playerRoomResponse, userRole = "player" }
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Join a room as spectator by room ID
        /// </summary>
        /// <param name="joinAsSpectatorDto">Join as spectator data</param>
        /// <returns>Joined room information</returns>
        [HttpPost("join-as-spectator")]
        public async Task<IActionResult> JoinAsSpectator([FromBody] JoinAsSpectatorDto joinAsSpectatorDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated"
                    });
                }

                var (success, message, room) = await _roomService.JoinAsSpectatorAsync(
                    joinAsSpectatorDto.RoomId,
                    userId,
                    username,
                    joinAsSpectatorDto.PinCode);

                if (!success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = message
                    });
                }

                var roomResponse = _mapper.Map<RoomResponseDto>(room);
                Console.WriteLine($"[ROOM JOIN AS SPECTATOR] User {username} joined room '{room?.Name}' as spectator");

                // Broadcast room list update via SignalR
                await BroadcastRoomListUpdate();

                return Ok(new
                {
                    success = true,
                    message = message,
                    data = new { room = roomResponse, userRole = "spectator" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }


        /// <summary>
        /// Leave a room
        /// </summary>
        /// <param name="leaveRoomDto">Leave room data</param>
        /// <returns>Success message</returns>
        [HttpPost("leave")]
        public async Task<IActionResult> LeaveRoom([FromBody] LeaveRoomDto leaveRoomDto)
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

                var (success, message, room) = await _roomService.LeaveRoomAsync(leaveRoomDto.RoomId, userId);

                if (!success)
                {
                    Console.WriteLine($"[ROOM LEAVE] Failed to leave room: {message}");
                    return BadRequest(new
                    {
                        success = false,
                        message = message
                    });
                }

                Console.WriteLine($"[ROOM LEAVE] User left room successfully");

                // Broadcast room list update via SignalR
                await BroadcastRoomListUpdate();

                return Ok(new
                {
                    success = true,
                    message = message,
                    data = room != null ? new { room = _mapper.Map<RoomResponseDto>(room) } : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a room
        /// </summary>
        /// <param name="deleteRoomDto">Delete room data</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteRoom([FromBody] DeleteRoomDto deleteRoomDto)
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

                // Get room details before deletion
                var room = await _roomService.GetRoomAsync(deleteRoomDto.RoomId);
                if (room == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Room not found"
                    });
                }

                // Get creator's connection IDs to exclude them from the warning
                var creatorConnectionIds = await _sessionManagementService.GetUserConnectionsAsync(userId);

                // Send warning to all users in the room except the creator
                Console.WriteLine($"\u001b[31m[ROOM DELETE]\u001b[0m Warning users about room deletion for room {deleteRoomDto.RoomId}");
                await _gameHubContext.Clients.GroupExcept(deleteRoomDto.RoomId, creatorConnectionIds).SendAsync("RoomDeletionWarning", new
                {
                    success = true,
                    message = "Room creator is deleting the room. You will be kicked out in 5 seconds.",
                    countdown = 5
                });

                // Wait 5 seconds before kicking users out
                await Task.Delay(5000);

                // Send kickout message to all users in the room except the creator
                Console.WriteLine($"\u001b[31m[ROOM DELETE]\u001b[0m Kicking out all users from room {deleteRoomDto.RoomId}");
                await _gameHubContext.Clients.GroupExcept(deleteRoomDto.RoomId, creatorConnectionIds).SendAsync("KickedFromRoom", new
                {
                    success = true,
                    message = "Room has been deleted by the creator. You are being redirected to the lobby.",
                    reason = "room_deleted"
                });

                // Wait 1 second for kickout message to be processed
                await Task.Delay(1000);

                // Now delete the room
                var (success, message) = await _roomService.DeleteRoomAsync(deleteRoomDto.RoomId, userId);

                if (!success)
                {
                    Console.WriteLine($"\u001b[31m[ROOM DELETE]\u001b[0m Failed to delete room: {message}");
                    return BadRequest(new
                    {
                        success = false,
                        message = message
                    });
                }

                Console.WriteLine($"\u001b[31m[ROOM DELETE]\u001b[0m Room deleted successfully");

                // Broadcast room list update via SignalR
                await BroadcastRoomListUpdate();

                return Ok(new
                {
                    success = true,
                    message = message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Broadcast room list update to all connected clients via SignalR
        /// </summary>
        private async Task BroadcastRoomListUpdate()
        {
            try
            {
                Console.WriteLine("\u001b[36m[SIGNALR]\u001b[0m Broadcasting room list update");

                var rooms = await _roomService.GetAllRoomsAsync();
                var roomResponses = _mapper.Map<List<RoomResponseDto>>(rooms);

                // For broadcast to all clients, hide PIN codes for security
                // Individual clients can request specific room details with PIN codes via REST API
                foreach (var roomResponse in roomResponses)
                {
                    if (roomResponse.IsPrivate)
                    {
                        roomResponse.PinCode = null;
                    }
                }

                await _gameHubContext.Clients.All.SendAsync("RoomListUpdated", new
                {
                    success = true,
                    data = new { rooms = roomResponses }
                });

                Console.WriteLine($"\u001b[36m[SIGNALR]\u001b[0m Successfully broadcasted {roomResponses.Count} rooms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SIGNALR ERROR] Failed to broadcast room list: {ex.Message}");
            }
        }
    }
}
