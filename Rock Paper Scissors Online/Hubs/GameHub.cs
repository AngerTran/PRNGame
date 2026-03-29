using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

// Thay đổi các using sang namespace mới của bạn

using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Ultilities;

namespace Rock_Paper_Scissors_Online.Hubs
{
    [Authorize]
    public class GameHub : Hub
    {
        // GIỮ NGUYÊN TOÀN BỘ CODE CŨ CỦA BẠN TỪ ĐÂY TRỞ XUỐNG
        private readonly IRoomService _roomService;
        private readonly IMapper _mapper;
        private readonly IRoomChatService _roomChatService;
        private readonly IGameService _gameService;
        private readonly IBettingService _bettingService;
        private readonly IPointTransactionService _pointTransactionService;
        private readonly ISessionManagementService _sessionManagementService;

        // Static dictionary to track GameRoom timers
        private static readonly Dictionary<string, Timer> _roomTimers = new();
        private static readonly Dictionary<string, int> _roomTimerValues = new();
        private static readonly Dictionary<string, string> _roomTimerTypes = new();
        private static readonly Dictionary<string, bool> _gameCompleted = new();
        private static readonly Dictionary<string, bool> _betsRefunded = new();
        private static IHubContext<GameHub>? _hubContext;

        private const int RoundMoveSelectionSeconds = 10;
        private const int MoveRevealCountdownSeconds = 5;
        private const int MovesRevealedPauseBeforeRoundMs = 700;

        // Static service references for timeout handling
        private static IRoomService? _staticRoomService;
        private static IGameService? _staticGameService;
        private static IBettingService? _staticBettingService;
        private static IMapper? _staticMapper;
        private static IPointTransactionService? _staticPointTransactionService;
        private static IServiceScopeFactory? _staticServiceScopeFactory;

        public GameHub(IRoomService roomService, IMapper mapper, IRoomChatService roomChatService, IGameService gameService, IBettingService bettingService, IPointTransactionService pointTransactionService, IHubContext<GameHub> hubContext, ISessionManagementService sessionManagementService, IServiceScopeFactory serviceScopeFactory)
        {
            _roomService = roomService;
            _mapper = mapper;
            _roomChatService = roomChatService;
            _gameService = gameService;
            _bettingService = bettingService;
            _pointTransactionService = pointTransactionService;
            _hubContext = hubContext;
            _sessionManagementService = sessionManagementService;

            // Initialize static service references for timeout handling
            _staticRoomService = roomService;
            _staticGameService = gameService;
            _staticBettingService = bettingService;
            _staticMapper = mapper;
            _staticPointTransactionService = pointTransactionService;
            _staticServiceScopeFactory = serviceScopeFactory;
        }

        public async Task CreateRoom(CreateRoomDto createRoomDto)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: CreateRoom called by user {username} ({userId})");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Validate GameRoom name doesn't exist
                if (await _roomService.RoomExistsAsync(createRoomDto.Name))
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom with this name already exists");
                    return;
                }

                // Create GameRoom
                var displayName = Context.User?.FindFirst("DisplayName")?.Value ?? username;

                var GameRoom = await _roomService.CreateRoomAsync(
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

                // Update player1 connection ID
                if (GameRoom.Player1! != null)
                {
                    GameRoom.Player1!.ConnectionId = Context.ConnectionId;
                }

                // Join the creator to the GameRoom group
                await Groups.AddToGroupAsync(Context.ConnectionId, GameRoom.Id);

                var roomResponse = _mapper.Map<RoomResponseDto>(GameRoom);
                RoomPinRedaction.RedactPrivatePinUnlessHost(roomResponse, userId, GameRoom);

                // Send success response to caller
                await Clients.Caller.SendAsync("RoomCreated", new
                {
                    success = true,
                    message = "GameRoom created successfully",
                    data = new { GameRoom = roomResponse }
                });

                // Broadcast updated GameRoom list to all clients
                await BroadcastRoomList();
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to create GameRoom: {ex.Message}");
            }
        }

        public async Task GetRoomList()
        {
            try
            {
                var rooms = await _roomService.GetAllRoomsAsync();
                var roomResponses = _mapper.Map<List<RoomResponseDto>>(rooms);
                var userIdList = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                for (var i = 0; i < roomResponses.Count; i++)
                    RoomPinRedaction.RedactPrivatePinUnlessHost(roomResponses[i], userIdList, rooms[i]);

                await Clients.Caller.SendAsync("RoomList", new
                {
                    success = true,
                    data = new { rooms = roomResponses }
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to get GameRoom list: {ex.Message}");
            }
        }

        public async Task JoinRoom(JoinRoomDto joinRoomDto)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: JoinRoom called by user {username} ({userId})");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // First check if user is already in the GameRoom (from API call)
                var (alreadyInRoom, alreadyMessage, existingRoom) = await _roomService.GetRoomIfUserInRoomAsync(joinRoomDto.RoomId, userId);

                GameRoom? GameRoom;
                string message;
                bool success;

                if (alreadyInRoom)
                {
                    // User is already in the GameRoom, just use the existing GameRoom
                    GameRoom = existingRoom;
                    message = "User already in GameRoom";
                    success = true;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m User {username} already in GameRoom {joinRoomDto.RoomId}, adding to SignalR group only");
                }
                else
                {
                    // User is not in the GameRoom, try to join
                    (success, message, GameRoom) = string.IsNullOrEmpty(joinRoomDto.PinCode)
                        ? await _roomService.JoinRoomAsync(joinRoomDto.RoomId, userId, username)
                        : await _roomService.JoinRoomByPinAsync(joinRoomDto.PinCode, userId, username);

                    if (!success)
                    {
                        await Clients.Caller.SendAsync("JoinRoomFailed", new
                        {
                            success = false,
                            message = message
                        });
                        return;
                    }
                }

                if (GameRoom != null)
                {
                    // Update connection ID for the player who joined
                    if (GameRoom.Player1!?.UserId == userId)
                    {
                        GameRoom.Player1!.ConnectionId = Context.ConnectionId;
                    }
                    else if (GameRoom.Player2!?.UserId == userId)
                    {
                        GameRoom.Player2!.ConnectionId = Context.ConnectionId;
                    }

                    // Join the SignalR group
                    await Groups.AddToGroupAsync(Context.ConnectionId, GameRoom.Id);

                    var roomResponse = _mapper.Map<RoomResponseDto>(GameRoom);
                    RoomPinRedaction.RedactPrivatePinUnlessHost(roomResponse, userId, GameRoom);
                    var roomForGroup = RoomPinRedaction.CloneWithoutPin(_mapper, GameRoom);

                    // Send success response to the user who joined
                    await Clients.Caller.SendAsync("RoomJoined", new
                    {
                        success = true,
                        message = message,
                        data = new { GameRoom = roomResponse }
                    });

                    // Notify other players in the GameRoom about the new player
                    await Clients.Group(GameRoom.Id).SendAsync("PlayerJoined", new
                    {
                        success = true,
                        message = $"{username} joined the GameRoom",
                        data = new { GameRoom = roomForGroup }
                    });

                    // Broadcast spectator count update
                    await Clients.Group(GameRoom.Id).SendAsync("SpectatorCountUpdated", new
                    {
                        success = true,
                        data = new
                        {
                            roomId = GameRoom.Id,
                            spectators = GameRoom.Spectators,
                            spectatorCount = GameRoom.Spectators.Count
                        }
                    });

                    // Check if both players are present and transition to waiting_spectators
                    if (GameRoom.Player1 != null && GameRoom.Player2 != null)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players present in GameRoom {GameRoom.Id}, transitioning to waiting_spectators phase");
                        await SendPhaseChangedAsync(GameRoom.Id, "waiting_spectators");
                    }

                    // Broadcast updated GameRoom list to all clients
                    await BroadcastRoomList();
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to join GameRoom: {ex.Message}");
            }
        }

        public async Task LeaveRoom(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: LeaveRoom called by user {username} ({userId}) for GameRoom {roomId}");

                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Get GameRoom first to check if game is in progress
                var GameRoom = await _roomService.GetRoomAsync(roomId);

                // If game is in progress and a player leaves, end the game immediately
                if (GameRoom != null && GameRoom.Status == RoomStatus.InProgress && (GameRoom.Player1?.UserId == userId || GameRoom.Player2?.UserId == userId))
                {
                    await HandlePlayerRemovalDuringGame(GameRoom, userId);
                    return;
                }

                // First try to leave as a player
                var (success, message, updatedRoom) = await _roomService.LeaveRoomAsync(roomId, userId);
                GameRoom = updatedRoom;

                // If not a player, try to leave as a spectator
                if (!success && GameRoom != null && GameRoom.Spectators.Contains(userId))
                {
                    var (spectatorSuccess, spectatorMessage, spectatorRoom) = await _roomService.LeaveAsSpectatorAsync(roomId, userId);
                    if (spectatorSuccess)
                    {
                        success = true;
                        message = spectatorMessage;
                        GameRoom = spectatorRoom;
                    }
                }

                if (!success)
                {
                    await Clients.Caller.SendAsync("Error", message);
                    return;
                }

                // Remove from SignalR group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

                // Send response to the user who left
                await Clients.Caller.SendAsync("RoomLeft", new
                {
                    success = true,
                    message = message
                });

                // If GameRoom still exists, notify remaining players
                if (GameRoom != null)
                {
                    var roomResponse = RoomPinRedaction.CloneWithoutPin(_mapper, GameRoom);
                    await Clients.Group(roomId).SendAsync("PlayerLeft", new
                    {
                        success = true,
                        message = $"A player left the GameRoom",
                        data = new { GameRoom = roomResponse }
                    });

                    // Broadcast comprehensive spectator update
                    await BroadcastSpectatorUpdate(roomId, GameRoom);

                    // Check if game has started (betting_phase or later)
                    bool gameHasStarted = GameRoom.Status == RoomStatus.Playing || GameRoom.Status == RoomStatus.InProgress;

                    // Check if opponent left and GameRoom should reset to looking for opponent
                    if (GameRoom.Player1 == null || GameRoom.Player2 == null)
                    {
                        if (gameHasStarted)
                        {
                            // If game has started and a player left, delete the GameRoom
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player {userId} left GameRoom {roomId} after game started - starting deletion process");
                            _ = Task.Run(async () => await DeleteRoomWhenPlayerLeavesAfterGameStart(roomId, userId));
                        }
                        else
                        {
                            // If game hasn't started yet, just reset to looking for opponent
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player {userId} left GameRoom {roomId} before game started - resetting to looking for opponent state");

                            // Reset GameRoom status to waiting for opponent
                            await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Waiting);

                            // Send phase change to waiting_opponent
                            await SendPhaseChangedAsync(roomId, "waiting_opponent");

                            // Notify remaining players that GameRoom is looking for opponent
                            await Clients.Group(roomId).SendAsync("RoomStatusChanged", new
                            {
                                success = true,
                                message = "Opponent left. GameRoom is now looking for a new opponent.",
                                data = new { GameRoom = roomResponse, status = "waiting_opponent" }
                            });
                        }
                    }
                }

                // Broadcast updated GameRoom list to all clients
                await BroadcastRoomList();
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to leave GameRoom: {ex.Message}");
            }
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m User {username} ({userId}) connected with connection ID: {Context.ConnectionId}");

                if (!string.IsNullOrEmpty(userId))
                {
                    // Check if user is already logged in from another location
                    var isAlreadyLoggedIn = await _sessionManagementService.IsUserLoggedInAsync(userId);
                    if (isAlreadyLoggedIn)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m User {userId} is already logged in from another location - forcing logout of previous session");

                        // Force logout previous sessions
                        await _sessionManagementService.ForceLogoutUserAsync(userId);

                        // Notify previous connections about forced logout
                        var previousConnections = await _sessionManagementService.GetUserConnectionsAsync(userId);
                        foreach (var connectionId in previousConnections)
                        {
                            await Clients.Client(connectionId).SendAsync("ForcedLogout", new
                            {
                                success = true,
                                message = "You have been logged out because you logged in from another location.",
                                reason = "duplicate_login"
                            });
                        }
                    }

                    // Register new session
                    await _sessionManagementService.RegisterUserSessionAsync(userId, Context.ConnectionId);
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in OnConnectedAsync: {ex.Message}");
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    // Unregister session
                    await _sessionManagementService.UnregisterUserSessionAsync(userId, Context.ConnectionId);

                    // Find all rooms the user is in and handle disconnection
                    var rooms = await _roomService.GetAllRoomsAsync();
                    foreach (var GameRoom in rooms)
                    {
                        // Check if user is the GameRoom creator
                        if (GameRoom.CreatedBy == userId)
                        {
                            await HandleRoomCreatorDisconnection(GameRoom, userId);
                        }
                        // Check if user is a player
                        else if (GameRoom.Player1!?.UserId == userId || GameRoom.Player2!?.UserId == userId)
                        {
                            await HandlePlayerDisconnection(GameRoom, userId);
                        }
                        // Check if user is a spectator
                        else if (GameRoom.Spectators.Contains(userId))
                        {
                            await LeaveAsSpectator(GameRoom.Id);
                        }
                    }
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDisconnectedAsync: {ex.Message}");
                await base.OnDisconnectedAsync(exception);
            }
        }

        private async Task HandleRoomCreatorDisconnection(GameRoom GameRoom, string disconnectedUserId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom creator {disconnectedUserId} disconnected from GameRoom {GameRoom.Id}");

                // Check if game has started (betting_phase or later)
                bool gameHasStarted = GameRoom.Status == RoomStatus.Playing || GameRoom.Status == RoomStatus.InProgress;

                if (gameHasStarted)
                {
                    // If game has started, handle as auto-lose
                    await HandlePlayerAutoLose(GameRoom, disconnectedUserId);
                }
                else
                {
                    // If game hasn't started yet, immediately delete the GameRoom
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Immediately deleting GameRoom because creator disconnected before game started");

                    // Notify all users in the GameRoom that it's being deleted
                    await Clients.Group(GameRoom.Id).SendAsync("RoomDeleted", new
                    {
                        success = true,
                        message = "GameRoom has been deleted because the creator left. You are being redirected to the lobby.",
                        roomId = GameRoom.Id,
                        reason = "creator_disconnected"
                    });

                    // Delete the GameRoom immediately
                    var (success, message) = await _roomService.DeleteRoomAsync(GameRoom.Id, disconnectedUserId);

                    if (success)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {GameRoom.Id} immediately deleted because creator disconnected: {message}");

                        // Broadcast updated GameRoom list to all clients
                        await BroadcastRoomList();
                    }
                    else
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Failed to immediately delete GameRoom {GameRoom.Id}: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error handling GameRoom creator disconnection: {ex.Message}");
            }
        }

        private async Task HandlePlayerDisconnection(GameRoom GameRoom, string disconnectedUserId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player {disconnectedUserId} disconnected from GameRoom {GameRoom.Id}");

                // Check if game has started (betting_phase or later)
                bool gameHasStarted = GameRoom.Status == RoomStatus.Playing || GameRoom.Status == RoomStatus.InProgress;

                if (gameHasStarted)
                {
                    // If game has started, handle as auto-lose
                    await HandlePlayerAutoLose(GameRoom, disconnectedUserId);
                }
                else
                {
                    // If game hasn't started yet, just remove the player
                    await LeaveRoom(GameRoom.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error handling player disconnection: {ex.Message}");
            }
        }

        private async Task HandlePlayerAutoLose(GameRoom GameRoom, string disconnectedUserId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Handling auto-lose for disconnected player {disconnectedUserId} in GameRoom {GameRoom.Id}");

                // Check if game has already been completed to prevent duplicate processing
                if (IsGameCompleted(GameRoom.Id))
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game for GameRoom {GameRoom.Id} has already been completed - skipping auto-lose processing");
                    return;
                }

                // STOP ALL TIMERS FIRST to prevent further state transitions
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Stopping all timers for GameRoom {GameRoom.Id} due to auto-lose");
                StopRoomTimer(GameRoom.Id);

                // Determine winner (the remaining player)
                var winnerId = GameRoom.Player1!.UserId == disconnectedUserId ? GameRoom.Player2!.UserId : GameRoom.Player1!.UserId;
                var winnerUsername = GameRoom.Player1!.UserId == disconnectedUserId ? GameRoom.Player2!.Username : GameRoom.Player1!.Username;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Auto-lose result - Winner: {winnerUsername} ({winnerId}), Loser: {disconnectedUserId} (disconnected)");

                // Return all bets to spectators
                await ReturnAllBetsToSpectators(GameRoom.Id);

                // Process point transaction for the winner
                var pointResult = await ProcessGameResultStatic(winnerId, disconnectedUserId, GameRoom.PointsPerWin, GameRoom.Id, _staticServiceScopeFactory!, _hubContext);

                if (pointResult.Success)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Points processed for auto-lose - Winner: {pointResult.WinnerPoints}, Loser: {pointResult.LoserPoints}");
                }

                // Mark game as completed to prevent duplicate processing
                MarkGameAsCompleted(GameRoom.Id);

                // Update GameRoom status to completed
                await _roomService.UpdateRoomStatusAsync(GameRoom.Id, RoomStatus.Completed);

                // Send disconnection popup to all remaining players and spectators
                if (_hubContext != null)
                {
                    await _hubContext.Clients.Group(GameRoom.Id).SendAsync("PlayerDisconnectedPopup", new
                    {
                        success = true,
                        winner = winnerUsername,
                        winnerId = winnerId,
                        loser = "Disconnected Player",
                        reason = "disconnection",
                        winnerPoints = pointResult.WinnerPoints,
                        pointsPerWin = GameRoom.PointsPerWin,
                        roomId = GameRoom.Id,
                        message = $"{winnerUsername} wins by default - opponent disconnected"
                    });

                    // Send game result to remaining players and spectators
                    await _hubContext.Clients.Group(GameRoom.Id).SendAsync("GameResult", new
                    {
                        success = true,
                        winner = winnerUsername,
                        loser = "Disconnected Player",
                        reason = "disconnection",
                        winnerPoints = pointResult.WinnerPoints,
                        loserPoints = pointResult.LoserPoints,
                        message = $"{winnerUsername} wins by default - opponent disconnected"
                    });

                    // Send profile update to winner
                    await _hubContext.Clients.User(winnerId).SendAsync("ProfileUpdated", new
                    {
                        success = true,
                        points = pointResult.WinnerPoints,
                        message = "Profile updated after game win"
                    });

                    // Show result phase
                    await _hubContext.Clients.Group(GameRoom.Id).SendAsync("ShowResultPhase", new
                    {
                        success = true,
                        winner = winnerUsername,
                        loser = "Disconnected Player",
                        reason = "disconnection"
                    });
                }

                // DELETE GameRoom IMMEDIATELY after auto-lose (no delay)
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Deleting GameRoom {GameRoom.Id} immediately after auto-lose");
                var (success, message) = await _roomService.DeleteRoomAsync(GameRoom.Id, GameRoom.CreatedBy);

                if (success)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {GameRoom.Id} successfully deleted after auto-lose: {message}");

                    // Clear game completion status since GameRoom is deleted
                    ClearGameCompletionStatus(GameRoom.Id);
                    ClearBetRefundStatus(GameRoom.Id);

                    // Notify remaining players that GameRoom has been deleted due to auto-lose
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Notifying remaining players about GameRoom deletion due to auto-lose");
                    if (_hubContext != null)
                    {
                        await _hubContext.Clients.Group(GameRoom.Id).SendAsync("RoomDeleted", new
                        {
                            success = true,
                            message = "GameRoom has been deleted because a player disconnected during the game. You are being redirected to the lobby.",
                            roomId = GameRoom.Id,
                            reason = "auto_lose_disconnection"
                        });
                    }

                    // Broadcast updated GameRoom list to all clients
                    await BroadcastRoomList();
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Failed to delete GameRoom {GameRoom.Id} after auto-lose: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error handling auto-lose: {ex.Message}");
            }
        }

        private async Task ReturnAllBetsToSpectators(string roomId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Returning all bets to spectators in GameRoom {roomId}");

                // Check if bets have already been refunded to prevent duplicate refunds
                if (AreBetsRefunded(roomId))
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Bets for GameRoom {roomId} have already been refunded - skipping refund process");
                    return;
                }

                // Get all bets for this GameRoom
                var bets = await _bettingService.GetBetsForRoomAsync(roomId);

                if (bets.Any())
                {
                    // Return each bet to the spectator
                    foreach (var bet in bets)
                    {
                        try
                        {
                            // Add points back to the spectator
                            await _pointTransactionService.AddPointsAsync(Guid.Parse(bet.PlayerId), (int)bet.Amount, "Bet refunded due to player disconnection", roomId);
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Refunded {bet.Amount} points to spectator {bet.PlayerId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error refunding bet to spectator {bet.PlayerId}: {ex.Message}");
                        }
                    }

                    // Notify spectators about bet refunds
                    await _hubContext!.Clients.Group(roomId).SendAsync("BetRefunded", new
                    {
                        success = true,
                        message = "All bets have been refunded due to player disconnection",
                        refundedBets = bets.Count
                    });

                    // Mark bets as refunded to prevent duplicate refunds
                    MarkBetsAsRefunded(roomId);
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m No bets found for GameRoom {roomId}");
                    // Mark as refunded even if no bets to prevent future attempts
                    MarkBetsAsRefunded(roomId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error returning bets to spectators: {ex.Message}");
            }
        }

        private async Task HandlePlayerRemovalDuringGame(GameRoom GameRoom, string leavingUserId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player {leavingUserId} removed during game in GameRoom {GameRoom.Id}");

                // Determine winner (the remaining player)
                var winnerId = GameRoom.Player1!.UserId == leavingUserId ? GameRoom.Player2!.UserId : GameRoom.Player1!.UserId;
                var winnerUsername = GameRoom.Player1!.UserId == leavingUserId ? GameRoom.Player2!.Username : GameRoom.Player1!.Username;
                var leavingUsername = GameRoom.Player1!.UserId == leavingUserId ? GameRoom.Player1!.Username : GameRoom.Player2!.Username;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player removal result - Winner: {winnerUsername} ({winnerId}), Loser: {leavingUsername} ({leavingUserId})");

                // Return all bets to spectators
                await ReturnAllBetsToSpectators(GameRoom.Id);

                // Process point transaction for the winner
                var pointResult = await ProcessGameResultStatic(winnerId, leavingUserId, GameRoom.PointsPerWin, GameRoom.Id, _staticServiceScopeFactory!, _hubContext);

                if (pointResult.Success)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Points processed for player removal - Winner: {pointResult.WinnerPoints}, Loser: {pointResult.LoserPoints}");
                }

                // Update GameRoom status to completed
                await _roomService.UpdateRoomStatusAsync(GameRoom.Id, RoomStatus.Completed);

                // Send disconnection popup to all remaining players and spectators
                await _hubContext!.Clients.Group(GameRoom.Id).SendAsync("PlayerDisconnectedPopup", new
                {
                    success = true,
                    winner = winnerUsername,
                    winnerId = winnerId,
                    loser = leavingUsername,
                    reason = "player_removal",
                    winnerPoints = pointResult.WinnerPoints,
                    pointsPerWin = GameRoom.PointsPerWin,
                    roomId = GameRoom.Id,
                    message = $"{winnerUsername} wins by default - opponent left the game"
                });

                // Send game result to remaining players and spectators
                await _hubContext!.Clients.Group(GameRoom.Id).SendAsync("GameResult", new
                {
                    success = true,
                    winner = winnerUsername,
                    loser = leavingUsername,
                    reason = "player_removal",
                    winnerPoints = pointResult.WinnerPoints,
                    loserPoints = pointResult.LoserPoints,
                    message = $"{winnerUsername} wins by default - opponent left the game"
                });

                // Send profile update to winner
                await _hubContext.Clients.User(winnerId).SendAsync("ProfileUpdated", new
                {
                    success = true,
                    points = pointResult.WinnerPoints,
                    message = "Profile updated after game win"
                });

                // Show result phase
                await _hubContext.Clients.Group(GameRoom.Id).SendAsync("ShowResultPhase", new
                {
                    success = true,
                    winner = winnerUsername,
                    loser = leavingUsername,
                    reason = "player_removal"
                });

                // Remove the leaving player from the GameRoom
                await _roomService.LeaveRoomAsync(GameRoom.Id, leavingUserId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameRoom.Id);

                // Clean up GameRoom after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(15000); // 15 seconds delay
                    await _roomService.DeleteRoomAsync(GameRoom.Id, GameRoom.CreatedBy);
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {GameRoom.Id} deleted after player removal");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error handling player removal: {ex.Message}");
            }
        }

        private async Task BroadcastSpectatorUpdate(string roomId, GameRoom GameRoom)
        {
            try
            {
                // Get spectator usernames for display
                var spectatorUsernames = new List<string>();
                foreach (var spectatorId in GameRoom.Spectators)
                {
                    // In a real implementation, you'd fetch usernames from the database
                    // For now, we'll use the ID as a placeholder
                    spectatorUsernames.Add($"Spectator_{spectatorId.Substring(0, 8)}");
                }

                await Clients.Group(roomId).SendAsync("SpectatorCountUpdated", new
                {
                    success = true,
                    data = new
                    {
                        roomId = roomId,
                        spectators = GameRoom.Spectators,
                        spectatorUsernames = spectatorUsernames,
                        spectatorCount = GameRoom.Spectators.Count,
                        maxSpectators = GameRoom.MaxSpectators,
                        allowSpectators = GameRoom.AllowSpectators,
                        timestamp = DateTime.UtcNow
                    }
                });

                // Also broadcast to GameRoom list for lobby updates
                await BroadcastRoomList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error broadcasting spectator update: {ex.Message}");
            }
        }

        private async Task BroadcastRoomList()
        {
            try
            {
                var rooms = await _roomService.GetAllRoomsAsync();
                var roomResponses = _mapper.Map<List<RoomResponseDto>>(rooms);

                foreach (var roomResponse in roomResponses)
                    roomResponse.PinCode = null;

                await Clients.All.SendAsync("RoomListUpdated", new
                {
                    success = true,
                    data = new { rooms = roomResponses }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting GameRoom list: {ex.Message}");
            }
        }

        // ==== GameRoom CHAT LOGIC ====
        public async Task SendRoomMessage(string roomId, string content)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;

            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SendRoomMessage - GameRoom: {roomId}, User: {username}, Content: {content}");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
            {
                Console.WriteLine("\u001b[36m[GAME HUB]\u001b[0m SendRoomMessage - User not authenticated");
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                UserId = userId,
                Username = username,
                Content = content,
                Type = "user",
                Timestamp = DateTime.UtcNow
            };

            // Save message to service
            _roomChatService.AddMessage(roomId, message.UserId, message.Username, message.Content);
            await Clients.Group(roomId).SendAsync("ReceiveRoomMessage", message);
            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Message broadcasted to GameRoom group: {roomId}");
        }


        public async Task GetRoomMessages(string roomId, int limit = 500)
        {
            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GetRoomMessages - GameRoom: {roomId}, Limit: {limit}");
            var messages = _roomChatService.GetMessages(roomId, limit);
            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Found {messages.Count()} messages for GameRoom {roomId}");

            await Clients.Caller.SendAsync("RoomMessages", new
            {
                success = true,
                data = new { messages, hasMore = messages.Count() == limit }
            });
        }

        public async Task JoinRoomGroup(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: JoinRoomGroup called by user {username} ({userId}) for GameRoom {roomId}");

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m JoinRoomGroup - GameRoom: {roomId}, ConnectionId: {Context.ConnectionId}, User: {username} ({userId})");

                // Just join the SignalR group for real-time updates
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Successfully joined SignalR group for GameRoom: {roomId}");

                // Send confirmation to caller
                await Clients.Caller.SendAsync("RoomGroupJoined", new
                {
                    success = true,
                    message = $"Joined GameRoom group {roomId}",
                    roomId = roomId
                });

            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to join GameRoom group: {ex.Message}");
            }
        }

        public async Task LeaveRoomGroup(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: LeaveRoomGroup called by user {username} ({userId}) for GameRoom {roomId}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m LeaveRoomGroup - GameRoom: {roomId}, ConnectionId: {Context.ConnectionId}");

                // Just leave the SignalR group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Successfully left SignalR group for GameRoom: {roomId}");

                await Clients.Caller.SendAsync("RoomGroupLeft", new
                {
                    success = true,
                    message = $"Left GameRoom group {roomId}",
                    roomId = roomId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error leaving GameRoom group: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to leave GameRoom group: {ex.Message}");
            }
        }

        public async Task JoinAsSpectator(JoinRoomDto joinRoomDto)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: JoinAsSpectator called by user {username} ({userId})");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                bool success;
                string message;
                GameRoom? GameRoom;

                if (string.IsNullOrEmpty(joinRoomDto.PinCode))
                {
                    (success, message, GameRoom) = await _roomService.JoinAsSpectatorAsync(joinRoomDto.RoomId, userId, username);
                }
                else
                {
                    // Find GameRoom by PIN code first
                    var roomDetails = await _roomService.GetRoomByPinAsync(joinRoomDto.PinCode);
                    if (roomDetails == null)
                    {
                        await Clients.Caller.SendAsync("SpectatorJoinFailed", new
                        {
                            success = false,
                            message = "GameRoom not found for this PIN code"
                        });
                        return;
                    }

                    // Pass the PIN code for validation in private rooms
                    (success, message, GameRoom) = await _roomService.JoinAsSpectatorAsync(roomDetails.Id, userId, username, joinRoomDto.PinCode);
                }

                if (!success)
                {
                    await Clients.Caller.SendAsync("SpectatorJoinFailed", new
                    {
                        success = false,
                        message = message
                    });
                    return;
                }

                if (GameRoom != null)
                {
                    // Join the SignalR group
                    await Groups.AddToGroupAsync(Context.ConnectionId, GameRoom.Id);

                    var roomResponse = _mapper.Map<RoomResponseDto>(GameRoom);
                    RoomPinRedaction.RedactPrivatePinUnlessHost(roomResponse, userId, GameRoom);
                    var roomForGroup = RoomPinRedaction.CloneWithoutPin(_mapper, GameRoom);

                    // Get current phase for the GameRoom
                    var currentPhase = GetCurrentPhase(GameRoom.Id);
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Spectator joining GameRoom {GameRoom.Id} in phase: {currentPhase}");
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom status: {GameRoom.Status}, Timer type: {(_roomTimerTypes.ContainsKey(GameRoom.Id) ? _roomTimerTypes[GameRoom.Id] : "none")}");

                    // Send success response to the spectator who joined
                    await Clients.Caller.SendAsync("SpectatorJoined", new
                    {
                        success = true,
                        message = message,
                        data = new
                        {
                            GameRoom = roomResponse,
                            currentPhase = currentPhase
                        }
                    });

                    // Notify all players and spectators in the GameRoom about the new spectator
                    await Clients.Group(GameRoom.Id).SendAsync("SpectatorJoined", new
                    {
                        success = true,
                        message = $"{username} joined as spectator",
                        data = new
                        {
                            GameRoom = roomForGroup,
                            userId = userId,
                            username = username,
                            spectatorCount = GameRoom.Spectators.Count
                        }
                    });

                    // Broadcast comprehensive spectator update
                    await BroadcastSpectatorUpdate(GameRoom.Id, GameRoom);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error joining as spectator: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to join as spectator: {ex.Message}");
            }
        }

        public async Task LeaveAsSpectator(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: LeaveAsSpectator called by user {username} ({userId}) for GameRoom {roomId}");

                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var (success, message, GameRoom) = await _roomService.LeaveAsSpectatorAsync(roomId, userId);

                if (!success)
                {
                    await Clients.Caller.SendAsync("Error", message);
                    return;
                }

                // Remove from SignalR group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

                // Send response to the spectator who left
                await Clients.Caller.SendAsync("SpectatorLeft", new
                {
                    success = true,
                    message = message
                });

                // If GameRoom still exists, notify remaining players and spectators
                if (GameRoom != null)
                {
                    var roomResponse = RoomPinRedaction.CloneWithoutPin(_mapper, GameRoom);
                    await Clients.Group(roomId).SendAsync("SpectatorLeft", new
                    {
                        success = true,
                        message = $"{username} left as spectator",
                        data = new
                        {
                            GameRoom = roomResponse,
                            userId = userId,
                            username = username,
                            spectatorCount = GameRoom.Spectators.Count
                        }
                    });

                    // Broadcast updated GameRoom list to all clients
                    await BroadcastRoomList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error leaving as spectator: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to leave as spectator: {ex.Message}");
            }
        }

        // ==== GAME LIFECYCLE ====

        /// <summary>
        /// Start a game when GameRoom creator requests it and opponent is ready
        /// </summary>
        public async Task StartGame(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: StartGame called by user {username} ({userId}) for GameRoom {roomId}");

                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                // Check if user is the GameRoom creator
                if (GameRoom.CreatedBy != userId)
                {
                    await Clients.Caller.SendAsync("Error", "Only the GameRoom creator can start the game");
                    return;
                }

                // Check if both players are present
                if (GameRoom.Player1 == null || GameRoom.Player2 == null)
                {
                    await Clients.Caller.SendAsync("Error", "Both players must be present to start the game");
                    return;
                }

                // Check if opponent is ready
                var opponentReady = GameRoom.Player1.UserId == userId ? GameRoom.Player2.IsReady : GameRoom.Player1.IsReady;
                if (!opponentReady)
                {
                    await Clients.Caller.SendAsync("Error", "Opponent must be ready before starting the game");
                    return;
                }

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting game for GameRoom {roomId} - both players present and opponent ready");

                // Transition to betting phase
                await SendPhaseChangedAsync(roomId, "betting_phase");

                // Update GameRoom status to Playing when game starts (betting phase)
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} transitioning to Playing status (Game Started - Betting Phase)");
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Playing);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} successfully set to Playing status (Game Started - Betting Phase)");

                // Start betting phase timer (10 seconds)
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting betting_phase timer (10 seconds)");
                StartRoomTimerStatic(roomId, 10, "betting_phase");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error starting game: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to start game: {ex.Message}");
            }
        }


        /// <summary>
        /// End a game and determine winner
        /// </summary>
        public async Task EndGame(string roomId, string winnerId)
        {
            try
            {
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null) return;

                // Update GameRoom status to finished
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} transitioning to Finished status (Game Over)");
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Finished);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} successfully set to Finished status");

                // Process betting payouts
                var bettingResult = await _bettingService.ClaimWinnings(roomId, winnerId);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Betting winnings processed - Winner: {winnerId} (+{bettingResult.Winnings}), Total Claimed: {bettingResult.TotalClaimed}");

                // Send phase change to game_complete
                await SendPhaseChangedAsync(roomId, "game_complete");

                // Broadcast game ended to GameRoom
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Broadcasting game ended event");
                await Clients.Group(roomId).SendAsync("GameEnded", new
                {
                    success = true,
                    message = "Game finished!",
                    data = new
                    {
                        winnerId = winnerId,
                        winnerUsername = GameRoom.Player1!?.UserId == winnerId ? GameRoom.Player1!.Username : GameRoom.Player2!?.Username,
                        finalScores = GameRoom.PlayerScores,
                        bettingResult = bettingResult
                    }
                });

                // Start GameRoom deletion process after game ends
                _ = Task.Run(async () => await DeleteRoomAfterGameEnd(roomId, winnerId));
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to end game: {ex.Message}");
            }
        }

        // ==== PLAYER ACTIONS ====

        /// <summary>
        /// Set player ready state
        /// </summary>
        public async Task SetPlayerReady(string roomId, bool isReady)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: SetPlayerReady called by user {username} ({userId}) for GameRoom {roomId} with ready={isReady}");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                // Update player ready state
                if (GameRoom.Player1!?.UserId == userId)
                {
                    GameRoom.Player1!.IsReady = isReady;
                }
                else if (GameRoom.Player2!?.UserId == userId)
                {
                    GameRoom.Player2!.IsReady = isReady;
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "You are not a player in this GameRoom");
                    return;
                }

                // Broadcast ready state update to GameRoom
                await Clients.Group(roomId).SendAsync("PlayerReadyChanged", new
                {
                    success = true,
                    message = $"{username} is {(isReady ? "ready" : "not ready")}",
                    data = new
                    {
                        playerId = userId,
                        username = username,
                        isReady = isReady,
                        player1Ready = GameRoom.Player1!?.IsReady ?? false,
                        player2Ready = GameRoom.Player2!?.IsReady ?? false
                    }
                });

                // If both players are ready, notify they can start
                if (GameRoom.Player1!?.IsReady == true && GameRoom.Player2!?.IsReady == true)
                {
                    await Clients.Group(roomId).SendAsync("BothPlayersReady", new
                    {
                        success = true,
                        message = "Both players are ready! You can start the game.",
                        data = new { roomId = roomId }
                    });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to set ready state: {ex.Message}");
            }
        }

        /// <summary>
        /// Submit a move (rock, paper, scissors)
        /// </summary>
        public async Task SubmitMove(string roomId, string move)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: SubmitMove called by user {username} ({userId}) for GameRoom {roomId} with move {move}");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                if (GameRoom.PendingRevealResolution)
                {
                    await Clients.Caller.SendAsync("Error", "Đang chờ kết quả ván — không đổi nước đi lúc này.");
                    return;
                }

                // Validate move
                if (!IsValidMove(move))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid move. Must be rock, paper, or scissors");
                    return;
                }

                // Check if it's player's turn
                if (GameRoom.Player1!?.UserId != userId && GameRoom.Player2!?.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a player in this GameRoom");
                    return;
                }

                // Update player's current choice with logging
                string playerPosition = "";
                if (GameRoom.Player1!?.UserId == userId)
                {
                    GameRoom.Player1!.CurrentChoice = move;
                    playerPosition = "Player 1";
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m {username} (Player 1) submitted move: {move}");
                }
                else if (GameRoom.Player2!?.UserId == userId)
                {
                    GameRoom.Player2!.CurrentChoice = move;
                    playerPosition = "Player 2";
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m {username} (Player 2) submitted move: {move}");
                }

                // Log current GameRoom state
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} state after move submission:");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}): {GameRoom.Player1!?.CurrentChoice ?? "No move"}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}): {GameRoom.Player2!?.CurrentChoice ?? "No move"}");

                // Check if both players have made their moves
                if (GameRoom.Player1!?.CurrentChoice != null && GameRoom.Player2!?.CurrentChoice != null)
                {
                    GameRoom.PendingRevealResolution = true;
                    var revealEndsAt = DateTime.UtcNow.AddSeconds(MoveRevealCountdownSeconds);

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players submitted — {MoveRevealCountdownSeconds}s hidden reveal then process round");

                    await SendPhaseChangedAsync(roomId, "reveal_countdown");

                    await Clients.Group(roomId).SendAsync("MovesLocked", new
                    {
                        success = true,
                        message = "Moves locked — reveal countdown (no moves shown yet)",
                        data = new
                        {
                            player1Username = GameRoom.Player1!.Username,
                            player2Username = GameRoom.Player2!.Username,
                            revealEndsAt = revealEndsAt.ToString("o")
                        }
                    });

                    StartRoomTimerStatic(roomId, MoveRevealCountdownSeconds, "move_reveal");
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Waiting for opponent to submit move");
                    await Clients.Group(roomId).SendAsync("MoveSubmitted", new
                    {
                        success = true,
                        message = $"{username} has made their move",
                        data = new
                        {
                            playerId = userId,
                            username = username,
                            move = move,
                            playerPosition = playerPosition,
                            waitingForOpponent = true
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to submit move: {ex.Message}");
            }
        }

        /// <summary>
        /// Process a complete round (overload with roomId only)
        /// </summary>
        private async Task ProcessRound(string roomId)
        {
            try
            {
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} not found for round processing");
                    return;
                }

                await ProcessRound(roomId, GameRoom);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing round: {ex.Message}");
            }
        }

        /// <summary>
        /// Process a complete round
        /// </summary>
        private async Task ProcessRound(string roomId, GameRoom GameRoom)
        {
            try
            {
                GameRoom.PendingRevealResolution = false;

                var player1Move = GameRoom.Player1!?.CurrentChoice;
                var player2Move = GameRoom.Player2!?.CurrentChoice;
                var player1Id = GameRoom.Player1!?.UserId;
                var player2Id = GameRoom.Player2!?.UserId;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Processing round for GameRoom {roomId}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}): {player1Move}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}): {player2Move}");

                if (player1Move == null || player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Cannot process round - missing moves: P1={player1Move}, P2={player2Move}");
                    return;
                }

                // Determine round winner with actual player IDs
                var (result, winner) = DetermineRoundWinner(player1Move, player2Move, player1Id!, player2Id!);

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round result: {result}, Winner: {winner}");

                // Update scores based on actual player IDs
                if (winner == player1Id)
                {
                    GameRoom.PlayerScores[player1Id] = GameRoom.PlayerScores.GetValueOrDefault(player1Id, 0) + 1;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}) wins! New score: {GameRoom.PlayerScores[player1Id]}");
                }
                else if (winner == player2Id)
                {
                    GameRoom.PlayerScores[player2Id] = GameRoom.PlayerScores.GetValueOrDefault(player2Id, 0) + 1;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}) wins! New score: {GameRoom.PlayerScores[player2Id]}");
                }
                else if (winner == "tie")
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round is a tie! No score changes.");
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Unknown winner result: {winner}");
                }

                // Increment round after processing
                GameRoom.CurrentRound++;

                // Check if game is over
                var maxRounds = GameRoom.BestOfRounds;
                var player1Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom!.Player1!.UserId, 0);
                var player2Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom!.Player2!.UserId, 0);

                // Game is over when someone wins the majority of rounds
                // For best-of-3: need 2 wins, for best-of-5: need 3 wins, etc.
                var requiredWins = (maxRounds + 1) / 2; // This gives us 2 for best-of-3, 3 for best-of-5, etc.
                var gameOver = player1Score >= requiredWins || player2Score >= requiredWins;

                // Prepare round result data
                var roundData = new
                {
                    success = true,
                    message = "Round completed!",
                    data = new
                    {
                        roundNumber = GameRoom.CurrentRound,
                        player1Move,
                        player2Move,
                        player1Username = GameRoom.Player1!?.Username,
                        player2Username = GameRoom.Player2!?.Username,
                        result,
                        winner,
                        winnerId = winner,
                        player1Score,
                        player2Score,
                        gameOver,
                        isDraw = winner == "tie"
                    }
                };

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Broadcasting round result:");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round {GameRoom.CurrentRound}: {GameRoom.Player1!?.Username} ({player1Move}) vs {GameRoom.Player2!?.Username} ({player2Move})");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Result: {result}, Winner: {winner}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Scores: {GameRoom.Player1!?.Username}={player1Score}, {GameRoom.Player2!?.Username}={player2Score}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game Over: {gameOver}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m BestOfRounds: {maxRounds}, CurrentRound: {GameRoom.CurrentRound}");

                // Send phase change to round_result
                await SendPhaseChangedAsync(roomId, "round_result");

                // Broadcast round result
                await Clients.Group(roomId).SendAsync("RoundCompleted", roundData);

                if (gameOver)
                {
                    var winnerId = player1Score > player2Score ? GameRoom.Player1!.UserId : GameRoom.Player2!.UserId;
                    await EndGame(roomId, winnerId);
                }
                else
                {
                    // Reset choices for next round
                    GameRoom.Player1!.CurrentChoice = null;
                    GameRoom.Player2!.CurrentChoice = null;

                    // Notify next round
                    await Clients.Group(roomId).SendAsync("NextRound", new
                    {
                        success = true,
                        message = $"Round {GameRoom.CurrentRound} starting!",
                        data = new
                        {
                            roundNumber = GameRoom.CurrentRound,
                            currentRound = GameRoom.CurrentRound,
                            player1Score,
                            player2Score
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to process round: {ex.Message}");
            }
        }


        // ==== HELPER METHODS ====

        /// <summary>
        /// Check if the move is valid
        /// </summary>
        private bool IsValidMove(string move)
        {
            return move?.ToLower() switch
            {
                "rock" or "paper" or "scissors" => true,
                _ => false
            };
        }

        /// <summary>
        /// Determine the round winner
        /// </summary>
        private (string Result, string Winner) DetermineRoundWinner(string player1Move, string player2Move, string player1Id, string player2Id)
        {
            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Determining winner: {player1Move} vs {player2Move}");

            if (player1Move == player2Move)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Same moves - it's a tie!");
                return ("tie", "tie");
            }

            var result = (player1Move, player2Move) switch
            {
                ("rock", "scissors") => ("player1_wins", player1Id),
                ("paper", "rock") => ("player1_wins", player1Id),
                ("scissors", "paper") => ("player1_wins", player1Id),
                ("scissors", "rock") => ("player2_wins", player2Id),
                ("rock", "paper") => ("player2_wins", player2Id),
                ("paper", "scissors") => ("player2_wins", player2Id),
                _ => ("error", "error")
            };

            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner determined: {result.Item1} - Winner ID: {result.Item2}");
            return result;
        }

        /// <summary>
        /// Handle timeout scenarios when timer expires
        /// </summary>
        private async Task HandleMoveTimeout(string roomId)
        {
            try
            {
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} not found for timeout handling");
                    return;
                }

                var player1Move = GameRoom.Player1!?.CurrentChoice;
                var player2Move = GameRoom.Player2!?.CurrentChoice;
                var player1Id = GameRoom.Player1!?.UserId;
                var player2Id = GameRoom.Player2!?.UserId;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Handling move timeout for GameRoom {roomId}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}): {player1Move ?? "No move"}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}): {player2Move ?? "No move"}");

                // Case 1: Both players didn't choose - randomize both moves
                if (player1Move == null && player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players timed out - randomizing moves");
                    var randomMove1 = GetRandomMove();
                    var randomMove2 = GetRandomMove();

                    GameRoom.Player1!.CurrentChoice = randomMove1;
                    GameRoom.Player2!.CurrentChoice = randomMove2;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Randomized moves - Player 1: {randomMove1}, Player 2: {randomMove2}");

                    // Process the round with randomized moves
                    await ProcessRoundStatic(roomId, GameRoom);
                }
                // Case 2: Only player 1 didn't choose - player 1 loses
                else if (player1Move == null && player2Move != null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}) timed out - assigning loss");

                    // Assign a losing move to player 1 based on player 2's move
                    var losingMove = GetLosingMove(player2Move);
                    GameRoom.Player1!.CurrentChoice = losingMove;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Assigned losing move {losingMove} to Player 1");

                    // Process the round
                    await ProcessRoundStatic(roomId, GameRoom);
                }
                // Case 3: Only player 2 didn't choose - player 2 loses
                else if (player1Move != null && player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}) timed out - assigning loss");

                    // Assign a losing move to player 2 based on player 1's move
                    var losingMove = GetLosingMove(player1Move);
                    GameRoom.Player2!.CurrentChoice = losingMove;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Assigned losing move {losingMove} to Player 2");

                    // Process the round
                    await ProcessRoundStatic(roomId, GameRoom);
                }
                // Case 4: Both players already chose (shouldn't happen with timeout, but handle gracefully)
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players already chose - processing round normally");
                    await ProcessRound(roomId, GameRoom);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error handling move timeout: {ex.Message}");
                await Clients.Group(roomId).SendAsync("Error", $"Failed to handle move timeout: {ex.Message}");
            }
        }

        /// <summary>
        /// Process move timeout using static context only
        /// </summary>
        private static async Task ProcessMoveTimeoutStatic(string roomId)
        {
            try
            {
                if (_staticRoomService == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Static GameRoom service not available for timeout handling");
                    return;
                }

                var GameRoom = await _staticRoomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} not found for timeout handling");
                    return;
                }

                // Check if GameRoom is in correct state for processing
                if (GameRoom.Status != RoomStatus.Playing)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} is not in Playing state ({GameRoom.Status}), skipping timeout processing");
                    return;
                }

                var player1Move = GameRoom.Player1!?.CurrentChoice;
                var player2Move = GameRoom.Player2!?.CurrentChoice;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Processing move timeout for GameRoom {roomId}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}): {player1Move ?? "No move"}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}): {player2Move ?? "No move"}");

                // Case 1: Both players didn't choose - randomize both moves
                if (player1Move == null && player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players timed out - randomizing moves");
                    var randomMove1 = GetRandomMoveStatic();
                    var randomMove2 = GetRandomMoveStatic();

                    GameRoom.Player1!.CurrentChoice = randomMove1;
                    GameRoom.Player2!.CurrentChoice = randomMove2;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Randomized moves - Player 1: {randomMove1}, Player 2: {randomMove2}");
                }
                // Case 2: Only player 1 didn't choose - player 1 loses
                else if (player1Move == null && player2Move != null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}) timed out - assigning loss");

                    // Assign a losing move to player 1 based on player 2's move
                    var losingMove = GetLosingMoveStatic(player2Move);
                    GameRoom.Player1!.CurrentChoice = losingMove;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Assigned losing move {losingMove} to Player 1");
                }
                // Case 3: Only player 2 didn't choose - player 2 loses
                else if (player1Move != null && player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}) timed out - assigning loss");

                    // Assign a losing move to player 2 based on player 1's move
                    var losingMove = GetLosingMoveStatic(player1Move);
                    GameRoom.Player2!.CurrentChoice = losingMove;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Assigned losing move {losingMove} to Player 2");
                }
                // Case 4: Both players already chose (shouldn't happen with timeout, but handle gracefully)
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players already chose - processing round normally");
                }

                // Process the round immediately since moves are already assigned
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Processing round immediately after move timeout");
                await ProcessRoundStatic(roomId, GameRoom);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing move timeout statically: {ex.Message}");
            }
        }

        /// <summary>
        /// Hết đếm ngược reveal: gửi nước đi cho client rồi tạm dừng ngắn trước khi xử lý ván.
        /// </summary>
        private static async Task CompleteMoveRevealAndProcessRoundAsync(string roomId)
        {
            try
            {
                if (_staticRoomService == null || _hubContext == null)
                    return;

                var gr = await _staticRoomService.GetRoomAsync(roomId);
                if (gr == null)
                    return;

                if (!gr.PendingRevealResolution)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m move_reveal skip — PendingRevealResolution already cleared for {roomId}");
                    return;
                }

                var p1 = gr.Player1?.CurrentChoice;
                var p2 = gr.Player2?.CurrentChoice;
                if (p1 == null || p2 == null)
                {
                    gr.PendingRevealResolution = false;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m move_reveal: missing moves, falling back to timeout handling for {roomId}");
                    await ProcessMoveTimeoutStatic(roomId);
                    return;
                }

                await _hubContext.Clients.Group(roomId).SendAsync("MovesRevealed", new
                {
                    success = true,
                    message = "Moves revealed",
                    data = new
                    {
                        player1Move = p1,
                        player2Move = p2,
                        player1Username = gr.Player1!.Username,
                        player2Username = gr.Player2!.Username
                    }
                });

                await Task.Delay(MovesRevealedPauseBeforeRoundMs);
                await ProcessRoundStatic(roomId, gr);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in CompleteMoveRevealAndProcessRoundAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Process move timeout directly in timer callback
        /// </summary>
        private async Task ProcessMoveTimeout(string roomId)
        {
            try
            {
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} not found for timeout handling");
                    return;
                }

                var player1Move = GameRoom.Player1!?.CurrentChoice;
                var player2Move = GameRoom.Player2!?.CurrentChoice;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Processing move timeout for GameRoom {roomId}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}): {player1Move ?? "No move"}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}): {player2Move ?? "No move"}");

                // Case 1: Both players didn't choose - randomize both moves
                if (player1Move == null && player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players timed out - randomizing moves");
                    var randomMove1 = GetRandomMove();
                    var randomMove2 = GetRandomMove();

                    GameRoom.Player1!.CurrentChoice = randomMove1;
                    GameRoom.Player2!.CurrentChoice = randomMove2;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Randomized moves - Player 1: {randomMove1}, Player 2: {randomMove2}");
                }
                // Case 2: Only player 1 didn't choose - player 1 loses
                else if (player1Move == null && player2Move != null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}) timed out - assigning loss");

                    // Assign a losing move to player 1 based on player 2's move
                    var losingMove = GetLosingMove(player2Move);
                    GameRoom.Player1!.CurrentChoice = losingMove;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Assigned losing move {losingMove} to Player 1");
                }
                // Case 3: Only player 2 didn't choose - player 2 loses
                else if (player1Move != null && player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}) timed out - assigning loss");

                    // Assign a losing move to player 2 based on player 1's move
                    var losingMove = GetLosingMove(player1Move);
                    GameRoom.Player2!.CurrentChoice = losingMove;

                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Assigned losing move {losingMove} to Player 2");
                }
                // Case 4: Both players already chose (shouldn't happen with timeout, but handle gracefully)
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players already chose - processing round normally");
                }

                // Now process the round with the assigned moves
                await ProcessRoundStatic(roomId, GameRoom);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing move timeout: {ex.Message}");
            }
        }


        /// <summary>
        /// Generate a random move (rock, paper, or scissors)
        /// </summary>
        private string GetRandomMove()
        {
            var moves = new[] { "rock", "paper", "scissors" };
            var random = new Random();
            return moves[random.Next(moves.Length)];
        }

        /// <summary>
        /// Static version of GetRandomMove for use in static methods
        /// </summary>
        private static string GetRandomMoveStatic()
        {
            var moves = new[] { "rock", "paper", "scissors" };
            var random = new Random();
            return moves[random.Next(moves.Length)];
        }

        /// <summary>
        /// Get a move that loses against the given move
        /// </summary>
        private string GetLosingMove(string winningMove)
        {
            return winningMove.ToLower() switch
            {
                "rock" => "scissors",
                "paper" => "rock",
                "scissors" => "paper",
                _ => "scissors" // Default fallback
            };
        }

        /// <summary>
        /// Static version of GetLosingMove for use in static methods
        /// </summary>
        private static string GetLosingMoveStatic(string winningMove)
        {
            return winningMove.ToLower() switch
            {
                "rock" => "scissors",
                "paper" => "rock",
                "scissors" => "paper",
                _ => "scissors" // Default fallback
            };
        }

        /// <summary>
        /// Static version of ProcessRound for use in static methods
        /// </summary>
        private static async Task ProcessRoundStatic(string roomId, GameRoom GameRoom)
        {
            try
            {
                // Check if GameRoom is in correct state for processing
                if (GameRoom.Status != RoomStatus.Playing)
                {
                    GameRoom.PendingRevealResolution = false;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} is not in Playing state ({GameRoom.Status}), skipping round processing");
                    return;
                }

                GameRoom.PendingRevealResolution = false;

                var player1Move = GameRoom.Player1!?.CurrentChoice;
                var player2Move = GameRoom.Player2!?.CurrentChoice;
                var player1Id = GameRoom.Player1!?.UserId;
                var player2Id = GameRoom.Player2!?.UserId;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Processing round for GameRoom {roomId}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}): {player1Move}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}): {player2Move}");

                if (player1Move == null || player2Move == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Cannot process round - missing moves: P1={player1Move}, P2={player2Move}");
                    return;
                }

                // Determine round winner with actual player IDs
                var (result, winner) = DetermineRoundWinnerStatic(player1Move, player2Move, player1Id!, player2Id!);

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round result: {result}, Winner: {winner}");

                // Update scores based on actual player IDs
                if (winner == player1Id)
                {
                    GameRoom.PlayerScores[player1Id] = GameRoom.PlayerScores.GetValueOrDefault(player1Id, 0) + 1;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 1 ({GameRoom.Player1!?.Username}) wins! New score: {GameRoom.PlayerScores[player1Id]}");
                }
                else if (winner == player2Id)
                {
                    GameRoom.PlayerScores[player2Id] = GameRoom.PlayerScores.GetValueOrDefault(player2Id, 0) + 1;
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player 2 ({GameRoom.Player2!?.Username}) wins! New score: {GameRoom.PlayerScores[player2Id]}");
                }
                else if (winner == "tie")
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round is a tie! No score changes.");
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Unknown winner result: {winner}");
                }

                // Increment round after processing
                GameRoom.CurrentRound++;

                // Check if game is over
                var maxRounds = GameRoom.BestOfRounds;
                var player1Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player1!.UserId, 0);
                var player2Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player2!.UserId, 0);

                // Game is over when someone wins the majority of rounds
                // For best-of-3: need 2 wins, for best-of-5: need 3 wins, etc.
                var requiredWins = (maxRounds + 1) / 2; // This gives us 2 for best-of-3, 3 for best-of-5, etc.
                var gameOver = player1Score >= requiredWins || player2Score >= requiredWins;

                // Prepare round result data
                var roundData = new
                {
                    success = true,
                    message = "Round completed!",
                    data = new
                    {
                        roundNumber = GameRoom.CurrentRound,
                        player1Move = player1Move,
                        player2Move = player2Move,
                        player1Username = GameRoom.Player1!?.Username,
                        player2Username = GameRoom.Player2!?.Username,
                        result = result,
                        winner = winner,
                        winnerId = winner,
                        player1Score = player1Score,
                        player2Score = player2Score,
                        gameOver = gameOver,
                        isDraw = winner == "tie"
                    }
                };

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Broadcasting round result:");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round {GameRoom.CurrentRound}: {GameRoom.Player1!?.Username} ({player1Move}) vs {GameRoom.Player2!?.Username} ({player2Move})");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Result: {result}, Winner: {winner}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Scores: {GameRoom.Player1!?.Username}={player1Score}, {GameRoom.Player2!?.Username}={player2Score}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game Over: {gameOver}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m BestOfRounds: {maxRounds}, CurrentRound: {GameRoom.CurrentRound}");

                // Broadcast round result using static hub context
                if (_hubContext != null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Broadcasting round completed event");
                    await _hubContext.Clients.Group(roomId).SendAsync("RoundCompleted", roundData);

                    if (gameOver)
                    {
                        // In best-of games, there should never be a tie game
                        // The game continues until someone wins the majority
                        var winnerId = player1Score > player2Score ? GameRoom.Player1!.UserId : GameRoom.Player2!.UserId;

                        // Trigger final round animation sequence before ending game
                        _ = Task.Run(async () => {
                            try
                            {
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Starting final round animation sequence");

                                // Capture values we need to avoid disposed context issues
                                var capturedPlayer1Move = player1Move;
                                var capturedPlayer2Move = player2Move;
                                var capturedResult = result;
                                var capturedWinner = winner;
                                var capturedPlayer1Score = player1Score;
                                var capturedPlayer2Score = player2Score;
                                var capturedServiceScopeFactory = _staticServiceScopeFactory;

                                // Trigger animation sequence from backend after 1.5 second delay
                                await Task.Delay(1500); // 1.5 second delay
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Starting animation sequence");
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Trigger animation");
                                // Trigger swing animation
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "swing",
                                    roomId = roomId,
                                    data = new
                                    {
                                        player1Move = player1Move,
                                        player2Move = player2Move,
                                        result = result,
                                        winner = winner
                                    },
                                    timestamp = DateTime.UtcNow
                                });
                                await Task.Delay(1500); // 1.5 second delay
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Starting animation sequence");
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Trigger animation");
                                // Trigger swing animation
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "swing",
                                    roomId = roomId,
                                    data = new
                                    {
                                        player1Move = player1Move,
                                        player2Move = player2Move,
                                        result = result,
                                        winner = winner
                                    },
                                    timestamp = DateTime.UtcNow
                                });

                                // Trigger thrown animation after 2.8 seconds
                                await Task.Delay(2800);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering thrown animation");
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "thrown",
                                    roomId = roomId,
                                    data = new
                                    {
                                        player1Move = capturedPlayer1Move,
                                        player2Move = capturedPlayer2Move
                                    },
                                    timestamp = DateTime.UtcNow
                                });

                                // Trigger reveal animation after 0.8 seconds
                                await Task.Delay(800);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering reveal animation");
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "reveal",
                                    roomId = roomId,
                                    data = new
                                    {
                                        player1Move = player1Move,
                                        player2Move = player2Move,
                                        result = result,
                                        winner = winner
                                    },
                                    timestamp = DateTime.UtcNow
                                });

                                await Task.Delay(1000);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering idle_reveal state");
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "idle_reveal",
                                    roomId = roomId,
                                    data = new
                                    {
                                        message = "Hands staying in revealed position"
                                    },
                                    timestamp = DateTime.UtcNow
                                });

                                // Trigger winner announcement after 0.5 seconds
                                await Task.Delay(500);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering winner announcement");
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "winner_announcement",
                                    roomId = roomId,
                                    data = new
                                    {
                                        player1Move = player1Move,
                                        player2Move = player2Move,
                                        player1Username = GameRoom.Player1!?.Username,
                                        player2Username = GameRoom.Player2!?.Username,
                                        winner = winner,
                                        winnerId = winnerId,
                                        result = result,
                                        player1Score = capturedPlayer1Score,
                                        player2Score = capturedPlayer2Score,
                                        isDraw = winner == "tie"
                                    },
                                    timestamp = DateTime.UtcNow
                                });

                                // Trigger game completion announcement after 2 seconds
                                await Task.Delay(2000);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering game completion announcement");
                                await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                                {
                                    success = true,
                                    animationType = "game_complete",
                                    roomId = roomId,
                                    data = new
                                    {
                                        winner = winner,
                                        winnerId = winnerId,
                                        winnerUsername = winnerId == GameRoom.Player1!.UserId ? GameRoom.Player1!.Username : GameRoom.Player2!.Username,
                                        finalScores = GameRoom.PlayerScores,
                                        totalRounds = GameRoom.CurrentRound
                                    },
                                    timestamp = DateTime.UtcNow
                                });

                                // Wait 3 seconds for final announcement, then show result phase
                                await Task.Delay(3000);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Processing points and showing result phase");

                                // Process point transactions in database
                                // In best-of games, there should always be a winner
                                var loserId = winnerId == GameRoom.Player1!.UserId ? GameRoom.Player2!.UserId : GameRoom.Player1!.UserId;

                                // Validate winner/loser identification
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m VALIDATION - Winner: {winnerId} ({(GameRoom.Player1!.UserId == winnerId ? "Player1" : "Player2")}), Loser: {loserId} ({(GameRoom.Player1!.UserId == loserId ? "Player1" : "Player2")})");

                                if (winnerId == loserId)
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m ERROR: Winner and loser are the same! Skipping point processing.");
                                    return;
                                }

                                var pointResult = await ProcessGameResultStatic(winnerId, loserId, GameRoom.PointsPerWin, roomId, capturedServiceScopeFactory!, _hubContext);

                                if (!pointResult.Success)
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing points: {pointResult.Message}");
                                    // Even if point processing fails, we still need to show the correct winner
                                    // Get current points from database for display using fresh context
                                    using var errorScope = capturedServiceScopeFactory!.CreateScope();
                                    var errorUserRepository = errorScope.ServiceProvider.GetRequiredService<IUserRepository>();

                                    var player1 = await errorUserRepository.GetByIdAsync(Guid.Parse(GameRoom.Player1!.UserId));
                                    var player2 = await errorUserRepository.GetByIdAsync(Guid.Parse(GameRoom.Player2!.UserId));

                                    pointResult = new PointTransactionResult
                                    {
                                        Success = false,
                                        Message = pointResult.Message,
                                        WinnerPoints = player1?.Points ?? 0,
                                        LoserPoints = player2?.Points ?? 0
                                    };
                                }
                                else
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Points processed successfully - Winner: {pointResult.WinnerPoints}, Loser: {pointResult.LoserPoints}");
                                }

                                // Send profile update signals to both players
                                await _hubContext.Clients.User(GameRoom.Player1!.UserId).SendAsync("ProfileUpdated", new
                                {
                                    success = true,
                                    points = pointResult.WinnerPoints,
                                    message = "Your profile has been updated"
                                });

                                await _hubContext.Clients.User(GameRoom.Player2!.UserId).SendAsync("ProfileUpdated", new
                                {
                                    success = true,
                                    points = pointResult.LoserPoints,
                                    message = "Your profile has been updated"
                                });

                                // Calculate actual rounds played (current round - 1, since we increment after processing)
                                var actualRoundsPlayed = GameRoom.CurrentRound - 1;

                                // Send result phase data to frontend
                                await _hubContext.Clients.Group(roomId).SendAsync("ShowResultPhase", new
                                {
                                    success = true,
                                    roomId = roomId,
                                    data = new
                                    {
                                        winnerId = winnerId,
                                        winnerUsername = winnerId == GameRoom.Player1!.UserId ? GameRoom.Player1!.Username : GameRoom.Player2!.Username,
                                        finalScores = GameRoom.PlayerScores,
                                        totalRounds = actualRoundsPlayed,
                                        isDraw = false, // Best-of games never end in draws
                                        pointsEarned = GameRoom.PointsPerWin,
                                        pointsLost = GameRoom.PointsPerWin,
                                        netPoints = winnerId == GameRoom.Player1!.UserId ? GameRoom.PointsPerWin : -GameRoom.PointsPerWin,
                                        updatedPoints = new
                                        {
                                            player1Points = winnerId == GameRoom.Player1!.UserId ? pointResult.WinnerPoints : pointResult.LoserPoints,
                                            player2Points = winnerId == GameRoom.Player2!.UserId ? pointResult.WinnerPoints : pointResult.LoserPoints
                                        }
                                    }
                                });

                                // Transition to result phase
                                await SendPhaseChangedAsync(roomId, "result_phase");

                                // Wait 15 seconds before ending the game
                                await Task.Delay(15000);
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Ending game after result phase");
                                await EndGameStatic(roomId, winnerId, GameRoom);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in final round animation sequence: {ex.Message}");
                                // Fallback: end game immediately if animation fails
                                await EndGameStatic(roomId, winnerId, GameRoom);
                            }
                        });
                    }
                    else
                    {
                        // Reset choices for next round
                        GameRoom.Player1!.CurrentChoice = null;
                        GameRoom.Player2!.CurrentChoice = null;

                        // Trigger animation sequence from backend after 1.5 second delay
                        _ = Task.Run(async () =>
                        {
                            // Capture values we need to avoid disposed context issues
                            var capturedPlayer1Move = player1Move;
                            var capturedPlayer2Move = player2Move;
                            var capturedResult = result;
                            var capturedWinner = winner;
                            var capturedPlayer1Score = player1Score;
                            var capturedPlayer2Score = player2Score;

                            await Task.Delay(1500); // 1.5 second delay
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Starting animation sequence");
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Trigger animation");
                            // Trigger swing animation
                            await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                            {
                                success = true,
                                animationType = "swing",
                                roomId = roomId,
                                data = new
                                {
                                    player1Move = capturedPlayer1Move,
                                    player2Move = capturedPlayer2Move,
                                    result = capturedResult,
                                    winner = capturedWinner
                                },
                                timestamp = DateTime.UtcNow
                            });

                            // Trigger thrown animation after 2.8 seconds
                            await Task.Delay(2800);
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering thrown animation");
                            await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                            {
                                success = true,
                                animationType = "thrown",
                                roomId = roomId,
                                data = new
                                {
                                    player1Move = capturedPlayer1Move,
                                    player2Move = capturedPlayer2Move
                                },
                                timestamp = DateTime.UtcNow
                            });

                            // Trigger reveal animation after 0.8 seconds
                            await Task.Delay(800);
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering reveal animation");
                            await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                            {
                                success = true,
                                animationType = "reveal",
                                roomId = roomId,
                                data = new
                                {
                                    player1Move = player1Move,
                                    player2Move = player2Move,
                                    result = result,
                                    winner = winner
                                },
                                timestamp = DateTime.UtcNow
                            });

                            await Task.Delay(1000);
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering idle_reveal state");
                            await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                            {
                                success = true,
                                animationType = "idle_reveal",
                                roomId = roomId,
                                data = new
                                {
                                    message = "Hands staying in revealed position"
                                },
                                timestamp = DateTime.UtcNow
                            });

                            // Debug logging for winner announcement
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering winner announcement");
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner announcement data:");
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player1: {GameRoom.Player1!?.Username} (move: {player1Move})");
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player2: {GameRoom.Player2!?.Username} (move: {player2Move})");
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner: {winner}, Result: {result}");

                            await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                            {
                                success = true,
                                animationType = "announce_winner",
                                roomId = roomId,
                                data = new
                                {
                                    player1Move = player1Move,
                                    player2Move = player2Move,
                                    result = result,
                                    winner = winner,
                                    winnerId = winner,
                                    player1Username = GameRoom.Player1!?.Username ?? "Player 1",
                                    player2Username = GameRoom.Player2!?.Username ?? "Player 2",
                                    player1Score = player1Score,
                                    player2Score = player2Score,
                                    isDraw = winner == "tie"
                                },
                                timestamp = DateTime.UtcNow
                            });

                            // Trigger new round announcement after winner announcement (2 seconds)
                            await Task.Delay(2000);
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Triggering new round announcement");
                            await _hubContext.Clients.Group(roomId).SendAsync("AnimationTriggered", new
                            {
                                success = true,
                                animationType = "announce_new_round",
                                roomId = roomId,
                                data = new
                                {
                                    nextRoundNumber = GameRoom.CurrentRound + 1,
                                    player1Score = player1Score,
                                    player2Score = player2Score
                                },
                                timestamp = DateTime.UtcNow
                            });

                            // Wait 2 seconds after "Triggering new round announcement" before starting next round
                            // This ensures all animations complete before the new round begins
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Waiting 2 seconds after new round announcement before starting next round");
                            await Task.Delay(2000); // 2 second delay after new round announcement

                            // Notify next round and start the next round
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Starting next round after 2 second delay");
                            await _hubContext.Clients.Group(roomId).SendAsync("NextRound", new
                            {
                                success = true,
                                message = $"Round {GameRoom.CurrentRound + 1} ready to start!",
                                data = new
                                {
                                    roundNumber = GameRoom.CurrentRound + 1,
                                    player1Score = player1Score,
                                    player2Score = player2Score
                                }
                            });

                            // Send phase change to round_active
                            await SendPhaseChangedAsync(roomId, "round_active");

                            // Start the next round timer
                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} starting round_active timer ({RoundMoveSelectionSeconds} seconds)");
                            StartRoomTimerStatic(roomId, RoundMoveSelectionSeconds, "round_active");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing round statically: {ex.Message}");
            }
        }

        /// <summary>
        /// Static version of DetermineRoundWinner for use in static methods
        /// </summary>
        private static (string Result, string Winner) DetermineRoundWinnerStatic(string player1Move, string player2Move, string player1Id, string player2Id)
        {
            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Determining winner: {player1Move} vs {player2Move}");

            if (player1Move == player2Move)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Same moves - it's a tie!");
                return ("tie", "tie");
            }

            var result = (player1Move, player2Move) switch
            {
                ("rock", "scissors") => ("player1_wins", player1Id),
                ("paper", "rock") => ("player1_wins", player1Id),
                ("scissors", "paper") => ("player1_wins", player1Id),
                ("scissors", "rock") => ("player2_wins", player2Id),
                ("rock", "paper") => ("player2_wins", player2Id),
                ("paper", "scissors") => ("player2_wins", player2Id),
                _ => ("error", "error")
            };

            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner determined: {result.Item1} - Winner ID: {result.Item2}");
            return result;
        }

        /// <summary>
        /// Static version of EndGame for use in static methods
        /// </summary>
        private static async Task EndGameStatic(string roomId, string winnerId, GameRoom GameRoom)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game ended for GameRoom {roomId}, winner: {winnerId}");

                // Check if game has already been completed to prevent duplicate processing
                if (IsGameCompleted(roomId))
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game for GameRoom {roomId} has already been completed - skipping end game processing");
                    return;
                }

                // Mark game as completed to prevent duplicate processing
                MarkGameAsCompleted(roomId);

                // Process payouts if betting is enabled
                if (GameRoom.AllowBetting && _staticBettingService != null)
                {
                    try
                    {
                        var payoutResult = _staticBettingService.ClaimWinnings(roomId, winnerId);
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Payouts processed: {payoutResult}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing payouts: {ex.Message}");
                    }
                }

                // Stop all timers for this GameRoom
                if (_roomTimers.ContainsKey(roomId))
                {
                    _roomTimers[roomId]?.Dispose();
                    _roomTimers.Remove(roomId);
                }

                if (_roomTimerTypes.ContainsKey(roomId))
                {
                    _roomTimerTypes.Remove(roomId);
                }

                // Broadcast game end
                if (_hubContext != null)
                {
                    await _hubContext.Clients.Group(roomId).SendAsync("GameEnded", new
                    {
                        success = true,
                        message = "Game completed!",
                        data = new
                        {
                            winnerId = winnerId,
                            winnerUsername = winnerId == GameRoom.Player1!?.UserId ? GameRoom.Player1!?.Username : GameRoom.Player2!?.Username,
                            finalScores = GameRoom.PlayerScores
                        }
                    });

                    // Also notify that all timers have been stopped
                    await _hubContext.Clients.Group(roomId).SendAsync("AllTimersStopped", new
                    {
                        success = true,
                        message = "All timers stopped - game completed",
                        roomId = roomId
                    });
                }

                // Send immediate kickout message to all users in the GameRoom
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Sending immediate kickout message to GameRoom {roomId}");
                if (_hubContext != null)
                {
                    await _hubContext.Clients.Group(roomId).SendAsync("KickedFromRoom", new
                    {
                        success = true,
                        message = "Game completed. You will be redirected to the lobby in 15 seconds.",
                        roomId = roomId,
                        reason = "game_completed"
                    });
                }

                // Start GameRoom deletion process after game ends (static version)
                _ = Task.Run(async () => await DeleteRoomAfterGameEndStatic(roomId, winnerId));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error ending game statically: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete GameRoom after game ends with 5-second warning
        /// </summary>
        private async Task DeleteRoomAfterGameEnd(string roomId, string winnerId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting GameRoom deletion process for GameRoom {roomId} after game end");

                // Wait 10 seconds before deletion
                await Task.Delay(10000);

                // Warn all users that GameRoom deletion is happening soon
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Warning users about GameRoom deletion for GameRoom {roomId}");
                await Clients.Group(roomId).SendAsync("RoomDeletionWarning", new
                {
                    success = true,
                    message = "GameRoom will be deleted in 5 seconds. You will be redirected to the lobby.",
                    roomId = roomId,
                    countdown = 5
                });

                // Wait 5 more seconds for warning
                await Task.Delay(5000);

                // Note: Kickout message was already sent when game ended
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Proceeding with GameRoom deletion for GameRoom {roomId}");

                // Delete the GameRoom
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Attempting to delete GameRoom {roomId}");
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                var (success, message) = await _roomService.DeleteRoomAsync(roomId, GameRoom?.CreatedBy ?? winnerId);

                if (success)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} successfully deleted: {message}");

                    // Clear game completion status since GameRoom is deleted
                    ClearGameCompletionStatus(roomId);
                    ClearBetRefundStatus(roomId);

                    // Notify all users that GameRoom has been deleted (backup notification)
                    await Clients.Group(roomId).SendAsync("RoomDeleted", new
                    {
                        success = true,
                        message = "GameRoom has been deleted. You are being redirected to the lobby.",
                        roomId = roomId
                    });

                    // Broadcast updated GameRoom list to all clients
                    await BroadcastRoomList();
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Failed to delete GameRoom {roomId}: {message}");

                    // If deletion failed, notify users
                    await Clients.Group(roomId).SendAsync("RoomDeletionFailed", new
                    {
                        success = false,
                        message = $"Failed to delete GameRoom: {message}",
                        roomId = roomId
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in GameRoom deletion process: {ex.Message}");

                // Notify users about the error
                await Clients.Group(roomId).SendAsync("RoomDeletionError", new
                {
                    success = false,
                    message = $"An error occurred during GameRoom deletion: {ex.Message}",
                    roomId = roomId
                });
            }
        }

        /// <summary>
        /// Static version of DeleteRoomAfterGameEnd for use in static methods
        /// </summary>
        private static async Task DeleteRoomAfterGameEndStatic(string roomId, string winnerId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting GameRoom deletion process for GameRoom {roomId} after game end (static)");

                // Wait 10 seconds before deletion
                await Task.Delay(10000);

                // Warn all users that GameRoom deletion is happening soon
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Warning users about GameRoom deletion for GameRoom {roomId} (static)");
                if (_hubContext != null)
                {
                    await _hubContext.Clients.Group(roomId).SendAsync("RoomDeletionWarning", new
                    {
                        success = true,
                        message = "GameRoom will be deleted in 5 seconds. You will be redirected to the lobby.",
                        roomId = roomId,
                        countdown = 5
                    });
                }

                // Wait 5 more seconds for warning
                await Task.Delay(5000);

                // Note: Kickout message was already sent when game ended
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Proceeding with GameRoom deletion for GameRoom {roomId}");

                // Delete the GameRoom
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Attempting to delete GameRoom {roomId} (static)");
                if (_staticRoomService != null)
                {
                    var GameRoom = await _staticRoomService.GetRoomAsync(roomId);
                    var (success, message) = await _staticRoomService.DeleteRoomAsync(roomId, GameRoom?.CreatedBy ?? winnerId);

                    if (success)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} successfully deleted (static): {message}");

                        // Clear game completion status since GameRoom is deleted
                        ClearGameCompletionStatus(roomId);
                        ClearBetRefundStatus(roomId);

                        // Notify all users that GameRoom has been deleted (backup notification)
                        if (_hubContext != null)
                        {
                            await _hubContext.Clients.Group(roomId).SendAsync("RoomDeleted", new
                            {
                                success = true,
                                message = "GameRoom has been deleted. You are being redirected to the lobby.",
                                roomId = roomId
                            });

                            // Broadcast updated GameRoom list to all clients
                            await BroadcastRoomListStatic();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Failed to delete GameRoom {roomId} (static): {message}");

                        // If deletion failed, notify users
                        if (_hubContext != null)
                        {
                            await _hubContext.Clients.Group(roomId).SendAsync("RoomDeletionFailed", new
                            {
                                success = false,
                                message = $"Failed to delete GameRoom: {message}",
                                roomId = roomId
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in GameRoom deletion process (static): {ex.Message}");

                // Notify users about the error
                if (_hubContext != null)
                {
                    await _hubContext.Clients.Group(roomId).SendAsync("RoomDeletionError", new
                    {
                        success = false,
                        message = $"An error occurred during GameRoom deletion: {ex.Message}",
                        roomId = roomId
                    });
                }
            }
        }

        /// <summary>
        /// Static version of BroadcastRoomList for use in static methods
        /// </summary>
        private static async Task BroadcastRoomListStatic()
        {
            try
            {
                if (_staticRoomService == null || _staticMapper == null || _hubContext == null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Static services not available for GameRoom list broadcast");
                    return;
                }

                var rooms = await _staticRoomService.GetAllRoomsAsync();
                var roomResponses = _staticMapper.Map<List<RoomResponseDto>>(rooms);

                foreach (var roomResponse in roomResponses)
                    roomResponse.PinCode = null;

                await _hubContext.Clients.All.SendAsync("RoomListUpdated", new
                {
                    success = true,
                    data = new { rooms = roomResponses }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting GameRoom list (static): {ex.Message}");
            }
        }

        /// <summary>
        /// Delete GameRoom when creator leaves with 5-second warning
        /// </summary>
        private async Task DeleteRoomWhenPlayerLeavesAfterGameStart(string roomId, string leavingPlayerId)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting GameRoom deletion process for GameRoom {roomId} because player {leavingPlayerId} left after game started");

                // Wait 10 seconds before deletion
                await Task.Delay(10000);

                // Warn all users that GameRoom deletion is happening soon
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Warning users about GameRoom deletion for GameRoom {roomId} (player left after game started)");
                await Clients.Group(roomId).SendAsync("RoomDeletionWarning", new
                {
                    success = true,
                    message = "A player left after the game started. GameRoom will be deleted in 5 seconds. You will be redirected to the lobby.",
                    roomId = roomId,
                    countdown = 5,
                    reason = "player_left_after_game_start"
                });

                // Wait 5 more seconds for warning
                await Task.Delay(5000);

                // Send kickout message before deletion
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Kicking out all users from GameRoom {roomId} before deletion (player left after game started)");
                await Clients.Group(roomId).SendAsync("KickedFromRoom", new
                {
                    success = true,
                    message = "You have been kicked out of the GameRoom because a player left after the game started. Redirecting to lobby...",
                    roomId = roomId,
                    reason = "player_left_after_game_start"
                });

                // Wait 1 second for kickout message to be processed
                await Task.Delay(1000);

                // Delete the GameRoom using the GameRoom creator's ID (since only creator can delete)
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom != null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Attempting to delete GameRoom {roomId} (player left after game started)");
                    var (success, message) = await _roomService.DeleteRoomAsync(roomId, GameRoom.CreatedBy);

                    if (success)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} successfully deleted (player left after game started): {message}");

                        // Clear game completion status since GameRoom is deleted
                        ClearGameCompletionStatus(roomId);
                        ClearBetRefundStatus(roomId);

                        // Notify all users that GameRoom has been deleted (backup notification)
                        await Clients.Group(roomId).SendAsync("RoomDeleted", new
                        {
                            success = true,
                            message = "GameRoom has been deleted because a player left after the game started. You are being redirected to the lobby.",
                            roomId = roomId,
                            reason = "player_left_after_game_start"
                        });

                        // Broadcast updated GameRoom list to all clients
                        await BroadcastRoomList();
                    }
                    else
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Failed to delete GameRoom {roomId} (player left after game started): {message}");

                        // If deletion failed, notify users
                        await Clients.Group(roomId).SendAsync("RoomDeletionFailed", new
                        {
                            success = false,
                            message = $"Failed to delete GameRoom: {message}",
                            roomId = roomId,
                            reason = "player_left_after_game_start"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in GameRoom deletion process (player left after game started): {ex.Message}");

                // Notify users about the error
                await Clients.Group(roomId).SendAsync("RoomDeletionError", new
                {
                    success = false,
                    message = $"An error occurred during GameRoom deletion: {ex.Message}",
                    roomId = roomId,
                    reason = "player_left_after_game_start"
                });
            }
        }

        // ==== BETTING INTEGRATION ====

        /// <summary>
        /// Place a bet on a game
        /// </summary>
        public async Task PlaceBet(string roomId, decimal amount, string playerId, string? pinCode = null)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: PlaceBet called by user {username} ({userId}) for GameRoom {roomId} with amount {amount} on player {playerId}");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                if (!BettingService.AllowedBetStakes.Contains(amount))
                {
                    await Clients.Caller.SendAsync("Error", $"Mức cược không hợp lệ. Chọn: {string.Join(", ", BettingService.AllowedBetStakes.OrderBy(x => x))}");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                var betRequest = new BetRequestDto
                {
                    PlayerId = userId,
                    TargetPlayerId = playerId,
                    Amount = amount,
                    PinCode = pinCode
                };

                var betResponse = await _bettingService.PlaceBet(roomId, betRequest);

                // Broadcast bet placed to GameRoom
                await Clients.Group(roomId).SendAsync("BetPlaced", new
                {
                    success = true,
                    message = $"{username} placed a bet of {amount} on {playerId}",
                    data = new
                    {
                        betId = betResponse.BetId,
                        playerId = playerId,
                        amount = amount,
                        timestamp = betResponse.Timestamp,
                        bettorId = userId,
                        bettorUsername = username
                    }
                });

                // Update betting pool
                await UpdateBettingPool(roomId);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to place bet: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current betting pool for a GameRoom
        /// </summary>
        public async Task GetBettingPool(string roomId)
        {
            try
            {
                // Get GameRoom information to pass correct player IDs
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                var pool = _bettingService.GetPool(roomId, GameRoom.Player1?.UserId, GameRoom.Player2?.UserId);

                await Clients.Caller.SendAsync("BettingPool", new
                {
                    success = true,
                    data = pool
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to get betting pool: {ex.Message}");
            }
        }

        /// <summary>
        /// Update betting pool and broadcast to GameRoom
        /// </summary>
        private async Task UpdateBettingPool(string roomId)
        {
            try
            {
                // Get GameRoom information to pass correct player IDs
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    Console.WriteLine($"GameRoom {roomId} not found for betting pool update");
                    return;
                }

                var pool = _bettingService.GetPool(roomId, GameRoom.Player1?.UserId, GameRoom.Player2?.UserId);

                await Clients.Group(roomId).SendAsync("BettingPoolUpdated", new
                {
                    success = true,
                    data = pool
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating betting pool: {ex.Message}");
            }
        }

        /// <summary>
        /// Signal that frontend is ready for next round
        /// </summary>
        public async Task ReadyForNextRound(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: ReadyForNextRound called by user {username} ({userId}) for GameRoom {roomId}");
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                // Check if user is a player in this GameRoom
                if (GameRoom.Player1!?.UserId != userId && GameRoom.Player2!?.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a player in this GameRoom");
                    return;
                }

                // Check if game is in a valid state to continue
                // Allow ready signal if GameRoom is Playing, Waiting, or Finished
                // Only reject if GameRoom is in an invalid state (this should not happen in normal flow)
                if (GameRoom.Status != RoomStatus.Playing && GameRoom.Status != RoomStatus.Waiting && GameRoom.Status != RoomStatus.Finished)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom is in invalid state ({GameRoom.Status}), ignoring ready signal from {userId} in GameRoom {roomId}");
                    return;
                }

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom status: {GameRoom.Status}, accepting ready signal from {userId} in GameRoom {roomId}");

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Player {userId} ready for next round in GameRoom {roomId}");

                // Ensure GameRoom status is set to Playing for the next round
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} transitioning to Playing status (Ready Signal)");
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Playing);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} successfully set to Playing status (Ready Signal)");

                // Check if this is the first round (no moves have been made yet)
                var isFirstRound = GameRoom.CurrentRound == 1 &&
                                 (GameRoom.Player1!?.CurrentChoice == null || GameRoom.Player2!?.CurrentChoice == null);

                if (isFirstRound)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m First round - starting timer immediately");
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Subsequent round - starting timer after ready signal");
                }

                // Send phase change to round_active
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Broadcasting phase change to round_active");
                await SendPhaseChangedAsync(roomId, "round_active");

                // Start the next round timer
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} starting round_active timer ({RoundMoveSelectionSeconds} seconds)");
                await StartRoomTimer(roomId, RoundMoveSelectionSeconds, "round_active");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error handling ready for next round: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to signal ready for next round: {ex.Message}");
            }
        }

        /// <summary>
        /// Trigger animation sequence for a GameRoom
        /// </summary>
        public async Task TriggerAnimation(string roomId, string animationType)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                // Check if user is a player in this GameRoom
                if (GameRoom.Player1!?.UserId != userId && GameRoom.Player2!?.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a player in this GameRoom");
                    return;
                }

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Triggering animation '{animationType}' for GameRoom {roomId}");

                // Broadcast animation trigger to all clients in the GameRoom
                await Clients.Group(roomId).SendAsync("AnimationTriggered", new
                {
                    success = true,
                    animationType,
                    roomId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error triggering animation: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to trigger animation: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop all timers for a GameRoom (called when game completes)
        /// </summary>
        public async Task StopAllTimers(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SOCKET CALL: StopAllTimers called by user {username} ({userId}) for GameRoom {roomId}");
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    await Clients.Caller.SendAsync("Error", "GameRoom not found");
                    return;
                }

                // Check if user is a player in this GameRoom
                if (GameRoom.Player1!?.UserId != userId && GameRoom.Player2!?.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a player in this GameRoom");
                    return;
                }

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Stopping all timers for GameRoom {roomId}");

                // Stop all timers for this GameRoom
                if (_roomTimers.ContainsKey(roomId))
                {
                    _roomTimers[roomId]?.Dispose();
                    _roomTimers.Remove(roomId);
                }

                if (_roomTimerTypes.ContainsKey(roomId))
                {
                    _roomTimerTypes.Remove(roomId);
                }

                // Notify all clients that timers have been stopped
                await Clients.Group(roomId).SendAsync("AllTimersStopped", new
                {
                    success = true,
                    message = "All timers stopped",
                    roomId = roomId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error stopping timers: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"Failed to stop timers: {ex.Message}");
            }
        }

        /// <summary>
        /// Start a synchronized timer for a GameRoom
        /// </summary>
        public async Task StartRoomTimer(string roomId, int duration, string timerType)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting timer for GameRoom {roomId}: {duration} seconds, type: {timerType}");

                // Check if timer already exists for this GameRoom and type
                if (_roomTimers.ContainsKey(roomId) && _roomTimerTypes.ContainsKey(roomId) && _roomTimerTypes[roomId] == timerType)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer of type '{timerType}' already exists for GameRoom {roomId}, ignoring duplicate start request");
                    return;
                }

                // If different timer type, stop existing timer and start new one
                if (_roomTimers.ContainsKey(roomId))
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Different timer type '{timerType}' requested for GameRoom {roomId}, stopping existing timer of type '{_roomTimerTypes.GetValueOrDefault(roomId, "unknown")}'");
                    StopRoomTimer(roomId);
                }

                // Set initial timer value and type
                _roomTimerValues[roomId] = duration;
                _roomTimerTypes[roomId] = timerType;

                // Broadcast initial timer value to all clients in the GameRoom
                await Clients.Group(roomId).SendAsync("TimerStarted", new
                {
                    success = true,
                    roomId = roomId,
                    duration = duration,
                    timerType = timerType,
                    currentTime = duration
                });

                // Create and start the timer
                var timer = new Timer(async _ =>
                {
                    try
                    {
                        if (_roomTimerValues.ContainsKey(roomId))
                        {
                            _roomTimerValues[roomId]--;
                            var currentTime = _roomTimerValues[roomId];

                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer update for GameRoom {roomId}: {currentTime} seconds remaining");

                            // Broadcast timer update to all clients in the GameRoom using static context
                            if (_hubContext != null)
                            {
                                await _hubContext.Clients.Group(roomId).SendAsync("TimerUpdate", new
                                {
                                    success = true,
                                    roomId = roomId,
                                    currentTime = currentTime,
                                    timerType = timerType
                                });

                                // If timer reaches 0, stop it and notify clients
                                if (currentTime <= 0)
                                {
                                    StopRoomTimer(roomId);
                                    await _hubContext.Clients.Group(roomId).SendAsync("TimerCompleted", new
                                    {
                                        success = true,
                                        roomId = roomId,
                                        timerType = timerType
                                    });

                                    // Handle timer expiration based on type
                                    if (timerType == "round_active")
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round active timer expired for GameRoom {roomId}");
                                        try
                                        {
                                            // Check if this is move selection phase or animation phase
                                            var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                            if (GameRoom != null)
                                            {
                                                var player1Move = GameRoom.Player1!?.CurrentChoice;
                                                var player2Move = GameRoom.Player2!?.CurrentChoice;

                                                // If both players have moves, this is animation phase - process the round
                                                if (player1Move != null && player2Move != null)
                                                {
                                                    if (GameRoom.PendingRevealResolution)
                                                    {
                                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round timer expired but reveal countdown owns resolution — skipping for GameRoom {roomId}");
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Animation phase complete - processing round for GameRoom {roomId}");
                                                        await ProcessRoundStatic(roomId, GameRoom);
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Move selection phase complete - processing timeout for GameRoom {roomId}");
                                                    await ProcessMoveTimeoutStatic(roomId);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing round active timeout: {ex.Message}");
                                        }
                                    }
                                    else if (timerType == "move_reveal")
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m move_reveal expired for GameRoom {roomId}");
                                        await CompleteMoveRevealAndProcessRoundAsync(roomId);
                                    }
                                    // Removed waiting_opponent and waiting_spectators timers
                                    // These phases now wait for user actions instead of timers
                                    else if (timerType == "betting_phase")
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - transitioning to battle_start");
                                        try
                                        {
                                            // Check if GameRoom still exists before processing timer expiration
                                            var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                            if (GameRoom == null)
                                            {
                                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} no longer exists, skipping betting_phase timer expiration");
                                                return;
                                            }

                                            // Send phase change to battle_start
                                            await SendPhaseChangedAsync(roomId, "battle_start");

                                            // Start the battle phase timer (5 seconds)
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting battle_start timer (5 seconds)");
                                            StartRoomTimerStatic(roomId, 5, "battle_start");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing betting_phase timeout: {ex.Message}");
                                        }
                                    }
                                    else if (timerType == "battle_start")
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - starting the game");
                                        try
                                        {
                                            // Check if GameRoom still exists before processing timer expiration
                                            var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                            if (GameRoom == null)
                                            {
                                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} no longer exists, skipping battle_start timer expiration");
                                                return;
                                            }

                                            // Set GameRoom status to Playing
                                            await _staticRoomService!.UpdateRoomStatusAsync(roomId, RoomStatus.Playing);
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} status set to Playing");

                                            // Start the game
                                            var gameStarted = await _staticGameService!.StartGameAsync(roomId);

                                            // Send phase change to round_active
                                            await SendPhaseChangedAsync(roomId, "round_active");

                                            // Start the first round timer (7 seconds)
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting round_active timer ({RoundMoveSelectionSeconds} seconds)");
                                            StartRoomTimerStatic(roomId, RoundMoveSelectionSeconds, "round_active");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing battle_start timeout: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - no timeout handling needed");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in timer callback: {ex.Message}");
                    }
                }, null, 1000, 1000);

                _roomTimers[roomId] = timer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error starting GameRoom timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the timer for a GameRoom
        /// </summary>
        public void StopRoomTimer(string roomId)
        {
            try
            {
                if (_roomTimers.ContainsKey(roomId))
                {
                    _roomTimers[roomId]?.Dispose();
                    _roomTimers.Remove(roomId);
                }

                if (_roomTimerValues.ContainsKey(roomId))
                {
                    _roomTimerValues.Remove(roomId);
                }

                if (_roomTimerTypes.ContainsKey(roomId))
                {
                    _roomTimerTypes.Remove(roomId);
                }

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Stopped timer for GameRoom {roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error stopping GameRoom timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark a game as completed to prevent duplicate processing
        /// </summary>
        private static void MarkGameAsCompleted(string roomId)
        {
            try
            {
                _gameCompleted[roomId] = true;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game marked as completed for GameRoom {roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error marking game as completed for GameRoom {roomId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a game has already been completed
        /// </summary>
        private static bool IsGameCompleted(string roomId)
        {
            try
            {
                return _gameCompleted.ContainsKey(roomId) && _gameCompleted[roomId];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error checking game completion status for GameRoom {roomId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear game completion status (used when GameRoom is deleted)
        /// </summary>
        private static void ClearGameCompletionStatus(string roomId)
        {
            try
            {
                if (_gameCompleted.ContainsKey(roomId))
                {
                    _gameCompleted.Remove(roomId);
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Game completion status cleared for GameRoom {roomId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error clearing game completion status for GameRoom {roomId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark bets as refunded for a GameRoom to prevent duplicate refunds
        /// </summary>
        private static void MarkBetsAsRefunded(string roomId)
        {
            try
            {
                _betsRefunded[roomId] = true;
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Bets marked as refunded for GameRoom {roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error marking bets as refunded for GameRoom {roomId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if bets have already been refunded for a GameRoom
        /// </summary>
        private static bool AreBetsRefunded(string roomId)
        {
            try
            {
                return _betsRefunded.ContainsKey(roomId) && _betsRefunded[roomId];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error checking bet refund status for GameRoom {roomId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear bet refund status (used when GameRoom is deleted)
        /// </summary>
        private static void ClearBetRefundStatus(string roomId)
        {
            try
            {
                if (_betsRefunded.ContainsKey(roomId))
                {
                    _betsRefunded.Remove(roomId);
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Bet refund status cleared for GameRoom {roomId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error clearing bet refund status for GameRoom {roomId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current timer value for a GameRoom
        /// </summary>
        public async Task GetRoomTimer(string roomId)
        {
            try
            {
                if (_roomTimerValues.ContainsKey(roomId))
                {
                    var currentTime = _roomTimerValues[roomId];
                    await Clients.Caller.SendAsync("TimerSync", new
                    {
                        success = true,
                        roomId,
                        currentTime
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error getting GameRoom timer: {ex.Message}");
            }
        }

        private static async Task SendPhaseChangedAsync(string roomId, string phase)
        {
            try
            {
                if (_hubContext != null)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} - Broadcasting phase change to {phase}");
                    await _hubContext.Clients.Group(roomId).SendAsync("PhaseChanged", new
                    {
                        success = true,
                        data = new { phase }
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Hub context disposed, cannot send phase change for GameRoom {roomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error sending phase change: {ex.Message}");
            }
        }

        private static string GetCurrentPhase(string roomId)
        {
            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GetCurrentPhase called for GameRoom {roomId}");

            // Check if there's an active timer for this GameRoom
            if (_roomTimerTypes.ContainsKey(roomId))
            {
                var timerType = _roomTimerTypes[roomId];
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Found timer type: {timerType} for GameRoom {roomId}");

                // Special case: if GameRoom status is Playing but timer is waiting_spectators,
                // it means we're transitioning to betting_phase
                if (timerType == "waiting_spectators")
                {
                    // Try to get GameRoom status to determine if we're in betting phase
                    try
                    {
                        if (_staticRoomService != null)
                        {
                            var GameRoom = _staticRoomService.GetRoomAsync(roomId).Result;
                            if (GameRoom != null)
                            {
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} status: {GameRoom.Status}");
                                if (GameRoom.Status == RoomStatus.Playing)
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} is Playing with waiting_spectators timer - returning betting_phase");
                                    return "betting_phase";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error getting GameRoom status for phase detection: {ex.Message}");
                    }
                }

                var phase = timerType switch
                {
                    "waiting_spectators" => "waiting_spectators",
                    "betting_phase" => "betting_phase",
                    "battle_start" => "battle_start",
                    "round_active" => "round_active",
                    _ => "waiting_opponent"
                };
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Returning phase from timer: {phase}");
                return phase;
            }

            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m No timer found for GameRoom {roomId}, checking GameRoom status");

            // If no timer, check GameRoom status to determine phase
            try
            {
                if (_staticRoomService != null)
                {
                    var GameRoom = _staticRoomService.GetRoomAsync(roomId).Result;
                    if (GameRoom != null)
                    {
                        var phase = GameRoom.Status switch
                        {
                            RoomStatus.Playing => "betting_phase",
                            RoomStatus.InProgress => "round_active",
                            _ => "waiting_opponent"
                        };
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Returning phase from GameRoom status: {phase} (status: {GameRoom.Status})");
                        return phase;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error getting GameRoom status for phase detection: {ex.Message}");
            }

            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Returning default phase: waiting_opponent");
            return "waiting_opponent";
        }

        private static void StartRoomTimerStatic(string roomId, int duration, string timerType)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting timer for GameRoom {roomId}: {duration} seconds, type: {timerType}");

                // Stop existing timer if any
                if (_roomTimers.ContainsKey(roomId))
                {
                    _roomTimers[roomId].Dispose();
                    _roomTimers.Remove(roomId);
                }

                // Initialize timer value
                _roomTimerValues[roomId] = duration;

                if (_hubContext != null)
                {
                    _ = _hubContext.Clients.Group(roomId).SendAsync("TimerStarted", new
                    {
                        success = true,
                        roomId = roomId,
                        duration = duration,
                        timerType = timerType,
                        currentTime = duration
                    });
                }

                // Create timer that updates every second
                var timer = new Timer(async _ =>
                {
                    try
                    {
                        if (_roomTimerValues.ContainsKey(roomId))
                        {
                            var currentTime = _roomTimerValues[roomId];
                            if (currentTime > 0)
                            {
                                _roomTimerValues[roomId] = currentTime - 1;

                                // Send timer update to clients
                                try
                                {
                                    if (_hubContext != null)
                                    {
                                        await _hubContext.Clients.Group(roomId).SendAsync("TimerUpdate", new
                                        {
                                            success = true,
                                            roomId = roomId,
                                            currentTime = currentTime - 1,
                                            timerType = timerType
                                        });
                                    }
                                }
                                catch (ObjectDisposedException)
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Hub context disposed, cannot send timer update for GameRoom {roomId}");
                                }
                            }
                            else
                            {
                                // Timer expired
                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}'");

                                // Stop the timer
                                if (_roomTimers.ContainsKey(roomId))
                                {
                                    _roomTimers[roomId].Dispose();
                                    _roomTimers.Remove(roomId);
                                }
                                _roomTimerValues.Remove(roomId);

                                // Send timer completed event
                                try
                                {
                                    if (_hubContext != null)
                                    {
                                        await _hubContext.Clients.Group(roomId).SendAsync("TimerCompleted", new
                                        {
                                            success = true,
                                            roomId = roomId,
                                            timerType = timerType
                                        });
                                    }
                                }
                                catch (ObjectDisposedException)
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Hub context disposed, cannot send timer completed for GameRoom {roomId}");
                                }

                                // Handle timer expiration based on type
                                if (timerType == "round_active")
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round active timer expired for GameRoom {roomId}");
                                    try
                                    {
                                        // Check if this is move selection phase or animation phase
                                        var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                        if (GameRoom != null)
                                        {
                                            var player1Move = GameRoom.Player1!?.CurrentChoice;
                                            var player2Move = GameRoom.Player2!?.CurrentChoice;

                                            // If both players have moves, this is animation phase - process the round
                                            if (player1Move != null && player2Move != null)
                                            {
                                                if (GameRoom.PendingRevealResolution)
                                                {
                                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Round timer expired but reveal countdown owns resolution — skipping for GameRoom {roomId}");
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Animation phase complete - processing round for GameRoom {roomId}");
                                                    await ProcessRoundStatic(roomId, GameRoom);
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Move selection phase complete - processing timeout for GameRoom {roomId}");
                                                await ProcessMoveTimeoutStatic(roomId);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing round active timeout: {ex.Message}");
                                    }
                                }
                                else if (timerType == "move_reveal")
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m move_reveal expired for GameRoom {roomId}");
                                    await CompleteMoveRevealAndProcessRoundAsync(roomId);
                                }
                                else if (timerType == "waiting_opponent")
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - checking if both players are present");
                                    try
                                    {
                                        var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                        if (GameRoom != null && GameRoom.Player1! != null && GameRoom.Player2! != null)
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Both players present, transitioning to waiting_spectators phase");
                                            // Send phase change to waiting_spectators
                                            await SendPhaseChangedAsync(roomId, "waiting_spectators");

                                            // Start the next phase timer (waiting_spectators)
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting waiting_spectators timer (7 seconds)");
                                            StartRoomTimerStatic(roomId, 7, "waiting_spectators");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Not both players present, GameRoom will be cleaned up");
                                            // GameRoom will be cleaned up by the GameRoom cleanup service
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing waiting_opponent timeout: {ex.Message}");
                                    }
                                }
                                else if (timerType == "waiting_spectators")
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - transitioning to betting_phase");
                                    try
                                    {
                                        // Send phase change to betting_phase
                                        await SendPhaseChangedAsync(roomId, "betting_phase");

                                        // Update GameRoom status to Playing when game starts (betting phase)
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} transitioning to Playing status (Game Started - Betting Phase from Spectator Wait)");
                                        await _staticRoomService!.UpdateRoomStatusAsync(roomId, RoomStatus.Playing);
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m STATE TRANSITION: GameRoom {roomId} successfully set to Playing status (Game Started - Betting Phase from Spectator Wait)");

                                        // Start the betting phase timer (10 seconds)
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting betting_phase timer (10 seconds)");
                                        StartRoomTimerStatic(roomId, 10, "betting_phase");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing waiting_spectators timeout: {ex.Message}");
                                    }
                                }
                                else if (timerType == "betting_phase")
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - transitioning to battle_start");
                                    try
                                    {
                                        // Check if GameRoom still exists before processing timer expiration
                                        var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                        if (GameRoom == null)
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} no longer exists, skipping betting_phase timer expiration");
                                            return;
                                        }

                                        // Send phase change to battle_start
                                        await SendPhaseChangedAsync(roomId, "battle_start");

                                        // Start the battle phase timer (5 seconds)
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting battle_start timer (5 seconds)");
                                        StartRoomTimerStatic(roomId, 5, "battle_start");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing betting_phase timeout: {ex.Message}");
                                    }
                                }
                                else if (timerType == "battle_start")
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - starting the game");
                                    try
                                    {
                                        // Check if GameRoom still exists before processing timer expiration
                                        var GameRoom = await _staticRoomService!.GetRoomAsync(roomId);
                                        if (GameRoom == null)
                                        {
                                            Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} no longer exists, skipping battle_start timer expiration");
                                            return;
                                        }

                                        // Set GameRoom status to Playing
                                        await _staticRoomService!.UpdateRoomStatusAsync(roomId, RoomStatus.Playing);
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m GameRoom {roomId} status set to Playing");

                                        // Start the game
                                        var gameStarted = await _staticGameService!.StartGameAsync(roomId);

                                        // Send phase change to round_active
                                        await SendPhaseChangedAsync(roomId, "round_active");

                                        // Start the first round timer (7 seconds)
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Starting round_active timer ({RoundMoveSelectionSeconds} seconds)");
                                        StartRoomTimerStatic(roomId, RoundMoveSelectionSeconds, "round_active");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing battle_start timeout: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer expired for GameRoom {roomId} with type '{timerType}' - no timeout handling needed");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error in timer callback: {ex.Message}");
                    }
                }, null, 1000, 1000);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Timer ended for GameRoom {roomId}: {duration} seconds");

                _roomTimers[roomId] = timer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error starting GameRoom timer: {ex.Message}");
            }
        }

        // Static method to process game results with fresh DataContext
        private static async Task<PointTransactionResult> ProcessGameResultStatic(string winnerId, string loserId, int pointsPerWin, string gameId, IServiceScopeFactory serviceScopeFactory, IHubContext<GameHub>? hubContext = null)
        {
            try
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Processing game result (static) - Winner: {winnerId}, Loser: {loserId}, Points: {pointsPerWin}");

                if (serviceScopeFactory == null)
                {
                    return new PointTransactionResult
                    {
                        Success = false,
                        Message = "Service scope factory not available"
                    };
                }

                // Create a new DataContext and UserRepository for this operation
                using var scope = serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                // Get users from database
                var winner = await userRepository.GetByIdAsync(Guid.Parse(winnerId));
                var loser = await userRepository.GetByIdAsync(Guid.Parse(loserId));

                if (winner == null || loser == null)
                {
                    return new PointTransactionResult
                    {
                        Success = false,
                        Message = "Winner or loser not found in database"
                    };
                }

                // Create game history record
                var gameHistory = new History
                {
                    Id = Guid.NewGuid(),
                    Name = $"Game GameRoom {gameId[..8]}", // Short GameRoom ID for display
                    CreatorUserId = Guid.Parse(winnerId), // Use winner as creator for now
                    MaxRounds = 3, // Default to 3 rounds
                    Points = pointsPerWin,
                    Status = "Completed",
                    OpponentId = Guid.Parse(loserId), // Use loser as opponent
                    OpponentScore = 0, // Default score, could be enhanced later
                    StartedAt = DateTime.UtcNow.AddMinutes(-5), // Estimate start time
                    FinishedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                // Add game history to context
                context.Histories.Add(gameHistory);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Created game history record: {gameHistory.Id}");

                // Create point transactions
                var winnerTransaction = new PointTransaction
                {
                    Id = 0, // Will be set by database
                    UserId = winner.Id,
                    Delta = pointsPerWin,
                    Reason = "Game Win",
                    Type = "credit",
                    HistoryId = gameHistory.Id, // Link to game history
                    CreatedAt = DateTime.UtcNow
                };

                var loserTransaction = new PointTransaction
                {
                    Id = 0, // Will be set by database
                    UserId = loser.Id,
                    Delta = -pointsPerWin,
                    Reason = "Game Loss",
                    Type = "debit",
                    HistoryId = gameHistory.Id, // Link to game history
                    CreatedAt = DateTime.UtcNow
                };

                // Update user points
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Before calculation - Winner: {winner.Username} = {winner.Points}, Loser: {loser.Username} = {loser.Points}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Points per win: {pointsPerWin}");

                winner.Points += pointsPerWin;
                winner.Wins += 1;
                winner.TotalGames += 1;
                winner.CurrentWinStreak += 1;
                if (winner.CurrentWinStreak > winner.LongestWinStreak)
                {
                    winner.LongestWinStreak = winner.CurrentWinStreak;
                }
                winner.LastPlayedAt = DateTime.UtcNow;
                winner.UpdatedAt = DateTime.UtcNow;

                loser.Points = Math.Max(0, loser.Points - pointsPerWin); // Prevent negative points
                loser.Losses += 1;
                loser.TotalGames += 1;
                loser.CurrentWinStreak = 0; // Reset win streak
                loser.LastPlayedAt = DateTime.UtcNow;
                loser.UpdatedAt = DateTime.UtcNow;

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m After calculation - Winner: {winner.Username} = {winner.Points}, Loser: {loser.Username} = {loser.Points}");

                // Add transactions to context
                context.PointTransactions.Add(winnerTransaction);
                context.PointTransactions.Add(loserTransaction);

                // Update users in context
                context.Users.Update(winner);
                context.Users.Update(loser);

                // Check if entities are being tracked
                var winnerEntry = context.Entry(winner);
                var loserEntry = context.Entry(loser);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner entity state: {winnerEntry.State}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Loser entity state: {loserEntry.State}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner points before save: {winner.Points}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Loser points before save: {loser.Points}");

                // Save changes to database
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m About to save changes to database...");
                var changesSaved = await context.SaveChangesAsync();
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m SaveChangesAsync returned: {changesSaved}");

                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Points updated (static) - Winner: {winner.Username} (+{pointsPerWin} = {winner.Points}), Loser: {loser.Username} (-{pointsPerWin} = {loser.Points})");

                // Process betting winnings
                try
                {
                    var bettingService = scope.ServiceProvider.GetRequiredService<IBettingService>();
                    var bettingResult = await bettingService.ClaimWinnings(gameId, winnerId);
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Betting winnings processed - Winner: {winnerId} (+{bettingResult.Winnings}), Total Claimed: {bettingResult.TotalClaimed}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing betting winnings: {ex.Message}");
                }

                // Verify points after save by re-querying from database
                var winnerAfterSave = await userRepository.GetByIdAsync(winner.Id);
                var loserAfterSave = await userRepository.GetByIdAsync(loser.Id);
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Points after save - Winner: {winnerAfterSave?.Username} = {winnerAfterSave?.Points}, Loser: {loserAfterSave?.Username} = {loserAfterSave?.Points}");
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Winner ID: {winner.Id}, Loser ID: {loser.Id}");

                // Send ProfileUpdated events if hubContext is available
                if (hubContext != null)
                {
                    try
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Sending ProfileUpdated events to players");
                        await hubContext.Clients.User(winnerId).SendAsync("ProfileUpdated", new
                        {
                            success = true,
                            points = winner.Points,
                            message = "Your profile has been updated"
                        });

                        await hubContext.Clients.User(loserId).SendAsync("ProfileUpdated", new
                        {
                            success = true,
                            points = loser.Points,
                            message = "Your profile has been updated"
                        });
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m ProfileUpdated events sent successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error sending ProfileUpdated events: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m HubContext not available, skipping ProfileUpdated events");
                }

                return new PointTransactionResult
                {
                    Success = true,
                    Message = "Points updated successfully",
                    WinnerPoints = winner.Points,
                    LoserPoints = loser.Points,
                    WinnerTransaction = winnerTransaction,
                    LoserTransaction = loserTransaction
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[36m[GAME HUB]\u001b[0m Error processing game result (static): {ex.Message}");
                return new PointTransactionResult
                {
                    Success = false,
                    Message = $"Error processing points: {ex.Message}"
                };
            }
        }
    }
}