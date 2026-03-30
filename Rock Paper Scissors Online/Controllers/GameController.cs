using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using Rock_Paper_Scissors_Online.Utilities;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/game")]
    [Authorize]
    public class GameController : ControllerBase
    {
        private readonly IGameService _gameService;
        private readonly IRoomService _roomService;
        private readonly IHubContext<GameHub> _gameHubContext;

        public GameController(IGameService gameService, IRoomService roomService, IHubContext<GameHub> gameHubContext)
        {
            _gameService = gameService;
            _roomService = roomService;
            _gameHubContext = gameHubContext;
        }

        [HttpPost("rooms/{roomId}/start")]
        public async Task<IActionResult> StartGame(string roomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Starting game for GameRoom {roomId} by user {userId}");

                // Get GameRoom details
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    return NotFound(new { success = false, message = "GameRoom not found" });
                }

                // Check if user is in the GameRoom
                if (GameRoom.Player1?.UserId != userId && GameRoom.Player2?.UserId != userId)
                {
                    return Forbid("You are not a member of this GameRoom");
                }

                // Check if both players are ready
                if (!(GameRoom.Player1?.IsReady == true) || !(GameRoom.Player2?.IsReady == true))
                {
                    return BadRequest(new { success = false, message = "Both players must be ready to start" });
                }

                // Start the game via service
                var gameStarted = await _gameService.StartGameAsync(roomId);
                if (!gameStarted)
                {
                    return BadRequest(new { success = false, message = "Failed to start game" });
                }

                // Update GameRoom status
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Playing);

                // Broadcast game started event via SignalR
                var gameStartData = new
                {
                    success = true,
                    message = "Game started!",
                    data = new
                    {
                        gameId = roomId,
                        roomId = roomId,
                        bestOfRounds = GameRoom.BestOfRounds,
                        pointsPerWin = GameRoom.PointsPerWin,
                        currentRound = 1
                    }
                };

                await _gameHubContext.Clients.Group(roomId).SendAsync("GameStarted", gameStartData);
                Console.WriteLine($"[GAME API] Game started and broadcasted to GameRoom {roomId}");

                return Ok(new { success = true, message = "Game started successfully", data = gameStartData.data });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error starting game: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("rooms/{roomId}/ready")]
        public async Task<IActionResult> SetPlayerReady(string roomId, [FromBody] SetReadyRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Setting ready state for GameRoom {roomId}, user {userId}, ready: {request.IsReady}");

                // Set player ready via GameRoom service
                var success = await _roomService.SetPlayerReadyAsync(roomId, userId, request.IsReady);
                if (!success)
                {
                    return BadRequest(new { success = false, message = "Failed to set ready state" });
                }

                // Get updated GameRoom state
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    return NotFound(new { success = false, message = "GameRoom not found" });
                }

                // Broadcast ready state change via SignalR
                var readyData = new
                {
                    success = true,
                    message = $"Player ready state updated",
                    data = new
                    {
                        roomId = roomId,
                        playerId = userId,
                        isReady = request.IsReady,
                        player1Ready = GameRoom.Player1?.IsReady ?? false,
                        player2Ready = GameRoom.Player2?.IsReady ?? false,
                        bothReady = (GameRoom.Player1?.IsReady == true) && (GameRoom.Player2?.IsReady == true)
                    }
                };

                await _gameHubContext.Clients.Group(roomId).SendAsync("PlayerReadyChanged", readyData);
                Console.WriteLine($"[GAME API] Ready state broadcasted to GameRoom {roomId}");

                // If both players are ready, broadcast that too
                if ((GameRoom.Player1?.IsReady == true) && (GameRoom.Player2?.IsReady == true))
                {
                    await _gameHubContext.Clients.Group(roomId).SendAsync("BothPlayersReady", new
                    {
                        success = true,
                        message = "Both players are ready!",
                        data = new { roomId = roomId }
                    });
                    Console.WriteLine($"[GAME API] Both players ready broadcasted to GameRoom {roomId}");
                }

                return Ok(new { success = true, message = "Ready state updated successfully", data = readyData.data });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error setting ready state: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("rooms/{roomId}/move")]
        public async Task<IActionResult> SubmitMove(string roomId, [FromBody] SubmitMoveRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Submitting move for GameRoom {roomId}, user {userId}, move: {request.Move}");

                // Validate move
                if (!IsValidMove(request.Move))
                {
                    return BadRequest(new { success = false, message = "Invalid move. Must be rock, paper, or scissors" });
                }

                // Submit move via service
                var response = await _gameService.SubmitMoveAsync(roomId, userId, request.Move);
                // Note: MoveSubmissionResponseDto doesn't have IsValid property, so we'll assume success if no exception

                // Get updated GameRoom state
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    return NotFound(new { success = false, message = "GameRoom not found" });
                }

                // Broadcast move submitted via SignalR
                var moveData = new
                {
                    success = true,
                    message = "Move submitted successfully",
                    data = new
                    {
                        roomId = roomId,
                        playerId = userId,
                        move = request.Move,
                        player1Move = GameRoom.Player1?.CurrentChoice,
                        player2Move = GameRoom.Player2?.CurrentChoice,
                        bothMovesSubmitted = !string.IsNullOrEmpty(GameRoom.Player1?.CurrentChoice) && !string.IsNullOrEmpty(GameRoom.Player2?.CurrentChoice)
                    }
                };

                await _gameHubContext.Clients.Group(roomId).SendAsync("MoveSubmitted", moveData);
                Console.WriteLine($"[GAME API] Move submitted and broadcasted to GameRoom {roomId}");

                // If both moves are submitted, process the round
                if (!string.IsNullOrEmpty(GameRoom.Player1?.CurrentChoice) && !string.IsNullOrEmpty(GameRoom.Player2?.CurrentChoice))
                {
                    await ProcessRound(roomId, GameRoom);
                }

                return Ok(new { success = true, message = "Move submitted successfully", data = moveData.data });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error submitting move: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("rooms/{roomId}/end")]
        public async Task<IActionResult> EndGame(string roomId, [FromBody] EndGameRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Ending game for GameRoom {roomId} by user {userId}");

                // Get GameRoom details
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    return NotFound(new { success = false, message = "GameRoom not found" });
                }

                // Check if user is in the GameRoom
                if (GameRoom.Player1?.UserId != userId && GameRoom.Player2?.UserId != userId)
                {
                    return Forbid("You are not a member of this GameRoom");
                }

                // Determine winner
                string winnerId = "";
                if (request.WinnerId != null)
                {
                    winnerId = request.WinnerId;
                }
                else
                {
                    // Auto-determine winner based on scores
                    if (GameRoom.PlayerScores.ContainsKey(GameRoom.Player1!.UserId) && GameRoom.PlayerScores.ContainsKey(GameRoom.Player2!.UserId))
                    {
                        var player1Score = GameRoom.PlayerScores[GameRoom.Player1.UserId];
                        var player2Score = GameRoom.PlayerScores[GameRoom.Player2.UserId];

                        if (player1Score > player2Score)
                            winnerId = GameRoom.Player1.UserId;
                        else if (player2Score > player1Score)
                            winnerId = GameRoom.Player2.UserId;
                        else
                        {
                            // In best-of games, there should never be equal scores when game ends
                            // This should not happen, but if it does, determine winner by other criteria
                            Console.WriteLine($"[GAME API] WARNING: Equal scores detected ({player1Score}-{player2Score}) in best-of game. This should not happen!");

                            // Fallback: Use player who made the last move or first player as winner
                            winnerId = GameRoom.Player1.UserId; // Default to player 1
                            Console.WriteLine($"[GAME API] Using fallback winner: {winnerId}");
                        }
                    }
                    else
                    {
                        // No scores available - this is an error condition
                        Console.WriteLine($"[GAME API] ERROR: No player scores available for game end determination");
                        return BadRequest(new { success = false, message = "Cannot determine winner - no scores available" });
                    }
                }

                // Update GameRoom status
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Waiting);

                // Broadcast game ended via SignalR
                var endGameData = new
                {
                    success = true,
                    message = "Game ended",
                    data = new
                    {
                        gameId = roomId,
                        roomId = roomId,
                        winnerId = winnerId,
                        winnerName = winnerId == GameRoom.Player1?.UserId ? GameRoom.Player1.Username :
                                   winnerId == GameRoom.Player2?.UserId ? GameRoom.Player2.Username : null,
                        finalScores = GameRoom.PlayerScores,
                        totalRounds = GameRoom.CurrentRound
                    }
                };

                await _gameHubContext.Clients.Group(roomId).SendAsync("GameEnded", endGameData);
                Console.WriteLine($"[GAME API] Game ended and broadcasted to GameRoom {roomId}");

                // Reset GameRoom for new game
                await ResetRoomForNewGame(roomId);

                return Ok(new { success = true, message = "Game ended successfully", data = endGameData.data });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error ending game: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("rooms/{roomId}/state")]
        public async Task<IActionResult> GetGameState(string roomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Getting game state for GameRoom {roomId}");

                var gameState = await _gameService.GetGameStateAsync(roomId);
                if (gameState == null)
                {
                    return NotFound(new { success = false, message = "Game state not found" });
                }

                return Ok(new { success = true, data = gameState });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error getting game state: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("rooms/{roomId}/ready-states")]
        public async Task<IActionResult> GetPlayerReadyStates(string roomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Getting ready states for GameRoom {roomId}");

                var readyStates = await _gameService.GetPlayerReadyStatesAsync(roomId);
                if (readyStates == null)
                {
                    return NotFound(new { success = false, message = "Ready states not found" });
                }

                return Ok(new { success = true, data = readyStates });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error getting ready states: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        // Helper methods
        private bool IsValidMove(string move)
        {
            return move?.ToLower() switch
            {
                "rock" or "paper" or "scissors" => true,
                _ => false
            };
        }

        private async Task ProcessRound(string roomId, Models.GameRoom GameRoom)
        {
            try
            {
                Console.WriteLine($"[GAME API] Processing round for GameRoom {roomId}");

                var player1Move = GameRoom.Player1?.CurrentChoice;
                var player2Move = GameRoom.Player2?.CurrentChoice;

                if (string.IsNullOrEmpty(player1Move) || string.IsNullOrEmpty(player2Move))
                {
                    Console.WriteLine($"[GAME API] Cannot process round - missing moves");
                    return;
                }

                // Determine round winner
                var (winnerId, reason) = DetermineRoundWinner(player1Move, player2Move, GameRoom.Player1!.UserId, GameRoom.Player2!.UserId);

                // Update scores
                if (!string.IsNullOrEmpty(winnerId))
                {
                    if (!GameRoom.PlayerScores.ContainsKey(winnerId))
                        GameRoom.PlayerScores[winnerId] = 0;
                    GameRoom.PlayerScores[winnerId]++;
                }

                GameRoom.CurrentRound++;

                // Broadcast round completed
                var roundData = new
                {
                    success = true,
                    message = "Round completed",
                    data = new
                    {
                        roomId = roomId,
                        round = GameRoom.CurrentRound,
                        player1Move = player1Move,
                        player2Move = player2Move,
                        winnerId = winnerId,
                        winnerName = winnerId == GameRoom.Player1?.UserId ? GameRoom.Player1!.Username :
                                   winnerId == GameRoom.Player2?.UserId ? GameRoom.Player2!.Username : null,
                        scores = GameRoom.PlayerScores,
                        isDraw = string.IsNullOrEmpty(winnerId)
                    }
                };

                await _gameHubContext.Clients.Group(roomId).SendAsync("RoundCompleted", roundData);
                Console.WriteLine($"[GAME API] Round completed and broadcasted to GameRoom {roomId}");

                // Check if game is over
                var maxRounds = GameRoom.BestOfRounds;
                var player1Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player1!.UserId, 0);
                var player2Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player2!.UserId, 0);

                var lastW = string.IsNullOrEmpty(winnerId) ? "tie" : winnerId;
                var gameOver = RpsMatchRules.IsMatchOver(maxRounds, GameRoom.CurrentRound, player1Score, player2Score);

                if (gameOver)
                {
                    var gameWinnerId = RpsMatchRules.ResolveWinnerUserId(
                        GameRoom.Player1.UserId, GameRoom.Player2.UserId, player1Score, player2Score, lastW);

                    await EndGame(roomId, new EndGameRequest { WinnerId = gameWinnerId });
                }
                else
                {
                    GameRoom.Player1.CurrentChoice = null;
                    GameRoom.Player2.CurrentChoice = null;

                    var nextRoundData = new
                    {
                        success = true,
                        message = "Next round starting",
                        data = new
                        {
                            roomId = roomId,
                            currentRound = GameRoom.CurrentRound,
                            scores = GameRoom.PlayerScores
                        }
                    };

                    await _gameHubContext.Clients.Group(roomId).SendAsync("NextRound", nextRoundData);
                    Console.WriteLine($"[GAME API] Next round started and broadcasted to GameRoom {roomId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error processing round: {ex.Message}");
            }
        }

        private static readonly Dictionary<string, string> _winningMoves = new()
{
    { "rock", "scissors" },
    { "scissors", "paper" },
    { "paper", "rock" }
};

        private (string WinnerId, string Reason) DetermineRoundWinner(string player1Move, string player2Move, string player1Id, string player2Id)
        {
            if (player1Move == player2Move)
                return ("", Reason: "Draw");

            if (_winningMoves.TryGetValue(player1Move, out var beats) && beats == player2Move)
                return (player1Id, $"{Capitalize(player1Move)} beats {player2Move}");

            if (_winningMoves.TryGetValue(player2Move, out beats) && beats == player1Move)
                return (player2Id, $"{Capitalize(player2Move)} beats {player1Move}");

            return ("", Reason: "Invalid moves");
        }

        private static string Capitalize(string move) =>
            string.IsNullOrEmpty(move) ? move : char.ToUpper(move[0]) + move[1..];


        [HttpPost("rooms/{roomId}/round/result")]
        public async Task<IActionResult> ProcessRoundResult(string roomId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Processing round result for GameRoom {roomId} by user {userId}");

                // Get GameRoom details
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    return NotFound(new { success = false, message = "GameRoom not found" });
                }

                // Check if user is in the GameRoom
                if (GameRoom.Player1?.UserId != userId && GameRoom.Player2?.UserId != userId)
                {
                    return Forbid("You are not a player in this GameRoom");
                }

                // Check if both players have made their moves
                if (string.IsNullOrEmpty(GameRoom.Player1?.CurrentChoice) || string.IsNullOrEmpty(GameRoom.Player2?.CurrentChoice))
                {
                    return BadRequest(new { success = false, message = "Both players must submit moves before processing round result" });
                }

                // Process the round
                var player1Move = GameRoom.Player1.CurrentChoice;
                var player2Move = GameRoom.Player2.CurrentChoice;

                // Determine round winner
                var (winnerId, reason) = DetermineRoundWinner(player1Move, player2Move, GameRoom.Player1.UserId, GameRoom.Player2.UserId);

                // Update scores
                if (!string.IsNullOrEmpty(winnerId) && winnerId != "tie")
                {
                    if (!GameRoom.PlayerScores.ContainsKey(winnerId))
                        GameRoom.PlayerScores[winnerId] = 0;
                    GameRoom.PlayerScores[winnerId]++;
                }

                GameRoom.CurrentRound++;

                // Check if game is over
                var maxRounds = GameRoom.BestOfRounds;
                var player1Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player1.UserId, 0);
                var player2Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player2.UserId, 0);

                var gameOver = RpsMatchRules.IsMatchOver(maxRounds, GameRoom.CurrentRound, player1Score, player2Score);

                // Prepare round result data
                var roundResult = new
                {
                    success = true,
                    message = "Round result processed successfully",
                    data = new
                    {
                        roundNumber = GameRoom.CurrentRound - 1,
                        player1Move = player1Move,
                        player2Move = player2Move,
                        player1Username = GameRoom.Player1?.Username,
                        player2Username = GameRoom.Player2?.Username,
                        result = reason,
                        winner = winnerId,
                        winnerId = winnerId,
                        player1Score = player1Score,
                        player2Score = player2Score,
                        gameOver = gameOver,
                        isDraw = winnerId == "tie"
                    }
                };

                // Broadcast round result via SignalR
                await _gameHubContext.Clients.Group(roomId).SendAsync("RoundCompleted", roundResult);
                Console.WriteLine($"[GAME API] Round result broadcasted to GameRoom {roomId}");

                return Ok(roundResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error processing round result: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("rooms/{roomId}/result")]
        public async Task<IActionResult> ProcessGameResult(string roomId, [FromBody] GameResultRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                Console.WriteLine($"[GAME API] Processing game result for GameRoom {roomId} by user {userId}");

                // Get GameRoom details
                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null)
                {
                    return NotFound(new { success = false, message = "GameRoom not found" });
                }

                // Check if user is in the GameRoom
                if (GameRoom.Player1?.UserId != userId && GameRoom.Player2?.UserId != userId)
                {
                    return Forbid("You are not a player in this GameRoom");
                }

                // Determine final winner if not provided
                var finalWinnerId = request.WinnerId;
                if (string.IsNullOrEmpty(finalWinnerId))
                {
                    var player1Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player1!.UserId, 0);
                    var player2Score = GameRoom.PlayerScores.GetValueOrDefault(GameRoom.Player2!.UserId, 0);

                    if (player1Score > player2Score)
                        finalWinnerId = GameRoom.Player1.UserId;
                    else if (player2Score > player1Score)
                        finalWinnerId = GameRoom.Player2.UserId;
                    else
                        finalWinnerId = "tie";
                }

                // Prepare game result data
                var gameResult = new
                {
                    success = true,
                    message = "Game completed successfully",
                    data = new
                    {
                        roomId = roomId,
                        winnerId = finalWinnerId,
                        finalScores = GameRoom.PlayerScores,
                        totalRounds = GameRoom.CurrentRound - 1,
                        gameDuration = DateTime.UtcNow - GameRoom.CreatedAt
                    }
                };

                // Broadcast game result via SignalR
                await _gameHubContext.Clients.Group(roomId).SendAsync("GameCompleted", gameResult);
                Console.WriteLine($"[GAME API] Game result broadcasted to GameRoom {roomId}");

                // Reset GameRoom for new game
                await ResetRoomForNewGame(roomId);

                return Ok(gameResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error processing game result: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        private async Task ResetRoomForNewGame(string roomId)
        {
            try
            {
                Console.WriteLine($"[GAME API] Resetting GameRoom {roomId} for new game");

                var GameRoom = await _roomService.GetRoomAsync(roomId);
                if (GameRoom == null) return;

                // Reset game state
                GameRoom.CurrentRound = 1;
                GameRoom.PlayerScores.Clear();
                GameRoom.Player1!.IsReady = false;
                GameRoom.Player2!.IsReady = false;
                GameRoom.Player1.CurrentChoice = null;
                GameRoom.Player2.CurrentChoice = null;

                // Update GameRoom status
                await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Waiting);

                Console.WriteLine($"[GAME API] GameRoom {roomId} reset successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME API] Error resetting GameRoom: {ex.Message}");
            }
        }
    }

    // DTOs for the API
    public class SetReadyRequest
    {
        public bool IsReady { get; set; }
    }

    public class SubmitMoveRequest
    {
        public string Move { get; set; } = string.Empty;
    }

    public class EndGameRequest
    {
        public string? WinnerId { get; set; }
    }

    public class GameResultRequest
    {
        public string? WinnerId { get; set; }
    }
}
