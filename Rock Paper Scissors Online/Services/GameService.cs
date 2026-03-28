using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class GameService : IGameService
    {
        private readonly Dictionary<string, GameStateDto> _games = new();
        private readonly Dictionary<string, PlayerReadyStateDto> _readyStates = new();
        private readonly Dictionary<string, List<GameHistoryDto>> _gameHistory = new();

        public GameService()
        {
            // Initialize with some mock data for testing
            InitializeMockData();
        }

        private void InitializeMockData()
        {
            // Mock game state
            var mockGame = new GameStateDto
            {
                Id = "game-123",
                RoomId = "room-123",
                Players = new List<string> { "user-123", "user-456" },
                Status = "playing",
                CurrentRound = 1,
                BestOfRounds = 5,
                Scores = new Dictionary<string, int>
                {
                    { "user-123", 2 },
                    { "user-456", 1 }
                },
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Rounds = new List<RoundDto>
                {
                    new RoundDto
                    {
                        RoundNumber = 1,
                        Moves = new Dictionary<string, string>
                        {
                            { "user-123", "rock" },
                            { "user-456", "scissors" }
                        },
                        Results = new Dictionary<string, string>
                        {
                            { "user-123", "win" },
                            { "user-456", "lose" }
                        },
                        Winner = "user-123",
                        CompletedAt = DateTime.UtcNow.AddMinutes(-8)
                    }
                }
            };

            _games["game-123"] = mockGame;

            // Mock ready states
            _readyStates["game-123"] = new PlayerReadyStateDto
            {
                Player1Ready = true,
                Player2Ready = true
            };

            // Mock game history
            _gameHistory["user-123"] = new List<GameHistoryDto>
            {
                new GameHistoryDto
                {
                    Id = "game-123",
                    Opponent = "PlayerTwo",
                    Result = "win",
                    Rounds = 5,
                    PointsEarned = 100,
                    Timestamp = DateTime.UtcNow.AddMinutes(-30)
                },
                new GameHistoryDto
                {
                    Id = "game-122",
                    Opponent = "PlayerThree",
                    Result = "loss",
                    Rounds = 3,
                    PointsEarned = 0,
                    Timestamp = DateTime.UtcNow.AddMinutes(-60)
                }
            };
        }

        public async Task<GameStateDto> GetGameStateAsync(string gameId)
        {
            await Task.Delay(1); // Simulate async operation

            if (_games.TryGetValue(gameId, out var game))
            {
                return game;
            }

            throw new KeyNotFoundException($"Game with ID {gameId} not found");
        }

        public async Task<MoveSubmissionResponseDto> SubmitMoveAsync(string gameId, string playerId, string move)
        {
            await Task.Delay(1); // Simulate async operation

            if (!_games.TryGetValue(gameId, out var game))
            {
                throw new KeyNotFoundException($"Game with ID {gameId} not found");
            }

            // Simulate game logic
            var opponentId = game.Players.FirstOrDefault(p => p != playerId);
            if (opponentId == null)
            {
                throw new InvalidOperationException("No opponent found");
            }

            // Mock opponent move
            var opponentMoves = new[] { "rock", "paper", "scissors" };
            var random = new Random();
            var opponentMove = opponentMoves[random.Next(opponentMoves.Length)];

            // Determine result
            var result = DetermineWinner(move, opponentMove);

            return new MoveSubmissionResponseDto
            {
                RoundResult = result,
                OpponentMove = opponentMove
            };
        }

        public async Task<bool> SetPlayerReadyAsync(string gameId, string playerId, bool ready)
        {
            await Task.Delay(1); // Simulate async operation

            if (!_readyStates.TryGetValue(gameId, out var readyState))
            {
                _readyStates[gameId] = new PlayerReadyStateDto();
                readyState = _readyStates[gameId];
            }

            // Update ready state based on player position
            if (_games.TryGetValue(gameId, out var game))
            {
                var playerIndex = game.Players.IndexOf(playerId);
                if (playerIndex == 0)
                {
                    readyState.Player1Ready = ready;
                }
                else if (playerIndex == 1)
                {
                    readyState.Player2Ready = ready;
                }
            }

            return true;
        }

        public async Task<bool> StartGameAsync(string gameId)
        {
            await Task.Delay(1); // Simulate async operation

            if (_readyStates.TryGetValue(gameId, out var readyState))
            {
                return readyState.Player1Ready && readyState.Player2Ready;
            }

            return false;
        }

        public async Task<bool> UpdateProgressAsync(string gameId, string playerId, int progress)
        {
            await Task.Delay(1); // Simulate async operation

            // In a real implementation, this would update the game state
            // For now, just return success
            return true;
        }

        public async Task<GameHistoryResponseDto> GetGameHistoryAsync(string userId, int limit = 20, int offset = 0)
        {
            await Task.Delay(1); // Simulate async operation

            if (!_gameHistory.TryGetValue(userId, out var history))
            {
                return new GameHistoryResponseDto
                {
                    RecentGames = new List<GameHistoryDto>(),
                    TotalGames = 0,
                    WinRate = 0.0
                };
            }

            var totalGames = history.Count;
            var wins = history.Count(h => h.Result == "win");
            var winRate = totalGames > 0 ? (double)wins / totalGames * 100 : 0.0;

            var pagedHistory = history
                .Skip(offset)
                .Take(limit)
                .ToList();

            return new GameHistoryResponseDto
            {
                RecentGames = pagedHistory,
                TotalGames = totalGames,
                WinRate = winRate
            };
        }

        public async Task<PlayerReadyStateDto> GetPlayerReadyStatesAsync(string gameId)
        {
            await Task.Delay(1); // Simulate async operation

            if (_readyStates.TryGetValue(gameId, out var readyState))
            {
                return readyState;
            }

            return new PlayerReadyStateDto
            {
                Player1Ready = false,
                Player2Ready = false
            };
        }

        private string DetermineWinner(string playerMove, string opponentMove)
        {
            if (playerMove == opponentMove)
                return "tie";

            return (playerMove, opponentMove) switch
            {
                ("rock", "scissors") => "win",
                ("paper", "rock") => "win",
                ("scissors", "paper") => "win",
                _ => "lose"
            };
        }
    }
}
