using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Enums;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class BettingService : IBettingService
    {
        /// <summary>Mức cược cố định (xu) — FE chỉ hiển thị các giá trị này.</summary>
        public static readonly HashSet<decimal> AllowedBetStakes = new[] { 25m, 50m, 100m, 150m }.ToHashSet();

        private static List<Bet> _bets = new();
        private static List<GameRoom> _rooms = new();
        private static decimal _totalClaimed = 0;
        private readonly IPointTransactionService _pointTransactionService;

        public BettingService(IPointTransactionService pointTransactionService)
        {
            _pointTransactionService = pointTransactionService;
        }

        // Method to add room to the collection (called from RoomService)
        public static void AddRoom(GameRoom room)
        {
            _rooms.Add(room);
        }

        // Method to remove room from the collection
        public static void RemoveRoom(string roomId)
        {
            _rooms.RemoveAll(r => r.Id == roomId);
        }

    public async Task<BetResponseDto> PlaceBet(string gameId, BetRequestDto request)
    {
        Console.WriteLine($"[BETTING AUDIT] PlaceBet called - GameId: {gameId}, PlayerId: {request.PlayerId}, TargetPlayerId: {request.TargetPlayerId}");
        
        // Get room details to use PointsPerWin as fixed bet amount
        var room = _rooms.FirstOrDefault(r => r.Id == gameId);
        if (room == null)
        {
            Console.WriteLine($"[BETTING AUDIT] PlaceBet failed - Room not found: {gameId}");
            throw new ArgumentException("Room not found");
        }
        
        Console.WriteLine($"[BETTING AUDIT] Room found - PointsPerWin: {room.PointsPerWin}, Player1: {room.Player1?.UserId}, Player2: {room.Player2?.UserId}");

        if (!room.AllowBetting)
            throw new InvalidOperationException("Phòng này không bật cược.");

        var bothReady = room.Player1?.IsReady == true && room.Player2?.IsReady == true;

        // Chỉ cho phép đặt cược TRƯỚC khi trận bắt đầu:
        // - Trạng thái waiting
        // - Đủ 2 người chơi và cả hai đã sẵn sàng
        // Khi trạng thái đã chuyển sang Playing / InProgress thì khóa cược.
        if (room.Status == RoomStatus.Playing || room.Status == RoomStatus.InProgress)
            throw new InvalidOperationException("Trận đấu đã bắt đầu, không thể đặt cược nữa.");

        if (room.Status != RoomStatus.Waiting || !bothReady)
            throw new InvalidOperationException("Chỉ được cược khi phòng có đủ 2 người chơi và cả hai đã sẵn sàng.");

        if (!AllowedBetStakes.Contains(request.Amount))
            throw new ArgumentException($"Mức cược không hợp lệ. Chọn một trong: {string.Join(", ", AllowedBetStakes.OrderBy(x => x))}.");

        if (room.IsPrivate)
        {
            var proof = (request.PinCode ?? "").Trim();
            if (string.IsNullOrEmpty(room.PinCode) ||
                !string.Equals(proof, room.PinCode.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Phòng riêng: cần mã PIN đúng mới được cược.");
        }

        // Validate request parameters
        if (string.IsNullOrEmpty(request.PlayerId))
        {
            throw new ArgumentException("Player ID is required");
        }

        if (string.IsNullOrEmpty(request.TargetPlayerId))
        {
            throw new ArgumentException("Target player ID is required");
        }

        // Validate that the target player is actually a player in the game
        if (room.Player1?.UserId != request.TargetPlayerId && room.Player2?.UserId != request.TargetPlayerId)
        {
            throw new ArgumentException("Target player is not a valid player in this game");
        }

        // Validate that the spectator is not trying to bet on themselves
        if (request.PlayerId == request.TargetPlayerId)
        {
            throw new ArgumentException("You cannot bet on yourself");
        }

        // Validate that the spectator is not one of the game players
        if (room.Player1?.UserId == request.PlayerId || room.Player2?.UserId == request.PlayerId)
        {
            throw new ArgumentException("Game players cannot place bets on their own game");
        }

        // Check if spectator has already placed a bet in this game
        var existingBet = _bets.FirstOrDefault(b => b.GameId == gameId && b.PlayerId == request.PlayerId);
        if (existingBet != null)
        {
            Console.WriteLine($"[BETTING AUDIT] PlaceBet failed - Duplicate bet attempt: Player {request.PlayerId} already has bet {existingBet.Id} in game {gameId}");
            throw new InvalidOperationException("You have already placed a bet in this game. Only one bet per spectator is allowed.");
        }
        
        Console.WriteLine($"[BETTING AUDIT] Validation passed - No existing bet found for player {request.PlayerId} in game {gameId}");

        var betAmount = request.Amount;
        if (betAmount <= 0)
            throw new ArgumentException("Invalid bet amount.");

        // Check if user has sufficient points before placing bet
        try
        {
            var userPoints = await _pointTransactionService.GetUserPointsAsync(Guid.Parse(request.PlayerId));
            Console.WriteLine($"[BETTING AUDIT] User points check - Player {request.PlayerId} has {userPoints} points, needs {betAmount} points");
            
            if (userPoints < betAmount)
            {
                Console.WriteLine($"[BETTING AUDIT] PlaceBet failed - Insufficient points: Player {request.PlayerId} has {userPoints} points but needs {betAmount} points");
                throw new InvalidOperationException($"Insufficient points. You have {userPoints} points but need {betAmount} points to place this bet.");
            }
            
            Console.WriteLine($"[BETTING AUDIT] Sufficient points confirmed for player {request.PlayerId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BETTING AUDIT] PlaceBet failed - Point check error: {ex.Message}");
            throw new Exception($"Failed to check user points: {ex.Message}");
        }

        // Subtract points immediately when placing bet
        try
        {
            Console.WriteLine($"[BETTING SERVICE] Subtracting {betAmount} points from spectator {request.PlayerId} for bet on {request.TargetPlayerId}");
            await _pointTransactionService.SubtractPointsAsync(
                Guid.Parse(request.PlayerId),
                (int)betAmount,
                "Bet Placed"
            );
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to place bet: {ex.Message}");
        }

        var bet = new Bet
        {
            GameId = gameId,
            PlayerId = request.PlayerId, // Spectator who placed the bet
            TargetPlayerId = request.TargetPlayerId, // Game player they bet on
            Amount = betAmount, // Use fixed amount from room
            Timestamp = DateTime.UtcNow,
            Status = "pending"
        };
        _bets.Add(bet);

        Console.WriteLine($"[BETTING AUDIT] Bet created successfully - BetId: {bet.Id}, GameId: {bet.GameId}, PlayerId: {bet.PlayerId}, TargetPlayerId: {bet.TargetPlayerId}, Amount: {bet.Amount}, Status: {bet.Status}");
        Console.WriteLine($"[BETTING AUDIT] Total bets in system: {_bets.Count}, Bets for this game: {_bets.Count(b => b.GameId == gameId)}");

        return new BetResponseDto
        {
            BetId = bet.Id,
                GameId = bet.GameId,
                PlayerId = bet.PlayerId,
                Amount = bet.Amount,
                Timestamp = bet.Timestamp,
                Status = bet.Status
            };
        }

        public object GetPool(string gameId, string? player1Id = null, string? player2Id = null)
        {
            var bets = _bets.Where(b => b.GameId == gameId).ToList();

            // Use provided player IDs if available, otherwise fall back to bet-based detection
            if (player1Id == null || player2Id == null)
            {
                var targetPlayerIds = bets.Select(b => b.TargetPlayerId).Distinct().ToList();
                if (targetPlayerIds.Count == 0)
                {
                    return new
                    {
                        player1Bets = new List<object>(),
                        player2Bets = new List<object>(),
                        totalPool = 0,
                        player1Odds = 1.0m, // Allow betting with 1.0 odds
                        player2Odds = 1.0m, // Allow betting with 1.0 odds
                        player1Total = 0m,
                        player2Total = 0m,
                        hasBets = false
                    };
                }
                
                // Sort target player IDs to ensure consistent ordering
                var sortedTargetPlayerIds = targetPlayerIds.OrderBy(id => id).ToList();
                player1Id = sortedTargetPlayerIds.ElementAtOrDefault(0);
                player2Id = sortedTargetPlayerIds.ElementAtOrDefault(1);
            }

            var player1Bets = bets
                .Where(b => b.TargetPlayerId == player1Id)
                .Select(b => new { id = b.Id, userId = b.PlayerId, amount = b.Amount, timestamp = b.Timestamp })
                .ToList();

            var player2Bets = bets
                .Where(b => b.TargetPlayerId == player2Id)
                .Select(b => new { id = b.Id, userId = b.PlayerId, amount = b.Amount, timestamp = b.Timestamp })
                .ToList();

            var totalPool = bets.Sum(b => b.Amount);
            var player1Total = player1Bets.Sum(b => (decimal)b.amount);
            var player2Total = player2Bets.Sum(b => (decimal)b.amount);

            // Calculate dynamic odds based on betting distribution
            // This is a parimutuel betting system where:
            // - Odds = Total Pool / Player's Total Bets
            // - Higher odds = less popular choice = higher potential profit
            // - Lower odds = more popular choice = lower potential profit
            // - 1.0 odds = no profit potential (break-even)
            // - Both players always allow betting (minimum 1.0 odds)
            decimal player1Odds = 0m;
            decimal player2Odds = 0m;

            if (player1Total > 0 && player2Total > 0)
            {
                // Both players have bets - calculate fair parimutuel odds
                player1Odds = totalPool / player1Total;
                player2Odds = totalPool / player2Total;
            }
            else if (player1Total > 0 && player2Total == 0)
            {
                // Only player 1 has bets - no profit potential (odds = 1.0)
                player1Odds = 1.0m;
                player2Odds = 1.0m; // Allow betting on player 2
            }
            else if (player1Total == 0 && player2Total > 0)
            {
                // Only player 2 has bets - no profit potential (odds = 1.0)
                player1Odds = 1.0m; // Allow betting on player 1
                player2Odds = 1.0m;
            }
            else
            {
                // No bets yet - allow betting on both players with 1.0 odds
                player1Odds = 1.0m;
                player2Odds = 1.0m;
            }

            // Calculate betting statistics
            var totalBets = bets.Count;
            var player1BetCount = player1Bets.Count;
            var player2BetCount = player2Bets.Count;
            
            // Calculate potential profit for each player
            var player1PotentialProfit = player1Odds > 0 ? (player1Odds - 1) * 100 : 0; // As percentage
            var player2PotentialProfit = player2Odds > 0 ? (player2Odds - 1) * 100 : 0; // As percentage

            return new
            {
                player1Bets,
                player2Bets,
                totalPool,
                player1Odds,
                player2Odds,
                player1Total,
                player2Total,
                totalBets,
                player1BetCount,
                player2BetCount,
                player1PotentialProfit,
                player2PotentialProfit,
                hasBets = true,
                bettingDistribution = new
                {
                    player1Percentage = totalPool > 0 ? (player1Total / totalPool) * 100 : 0,
                    player2Percentage = totalPool > 0 ? (player2Total / totalPool) * 100 : 0
                }
            };
        }

        public object GetUserBets(string userId)
        {
            var activeBets = _bets
                .Where(b => b.PlayerId == userId && b.Status == "pending")
                .Select(b => new BetResponseDto
                {
                    BetId = b.Id,
                    GameId = b.GameId,
                    PlayerId = b.PlayerId,
                    Amount = b.Amount,
                    Timestamp = b.Timestamp,
                    Status = b.Status
                }).ToList();

            var completedBets = _bets
                .Where(b => b.PlayerId == userId && b.Status != "pending")
                .Select(b => new BetResponseDto
                {
                    BetId = b.Id,
                    GameId = b.GameId,
                    PlayerId = b.PlayerId,
                    Amount = b.Amount,
                    Timestamp = b.Timestamp,
                    Status = b.Status,
                    Payout = b.Payout
                }).ToList();

            var totalWinnings = completedBets.Sum(b => b.Payout ?? 0);

            return new
            {
                activeBets,
                completedBets,
                totalWinnings
            };
        }

        public async Task<ClaimResponseDto> ClaimWinnings(string gameId, string winnerId)
        {
            Console.WriteLine($"[BETTING AUDIT] ClaimWinnings called - GameId: {gameId}, WinnerId: {winnerId}");
            
            var bets = _bets.Where(b => b.GameId == gameId).ToList();
            Console.WriteLine($"[BETTING AUDIT] Found {bets.Count} bets for game {gameId}");
            
            if (!bets.Any())
            {
                Console.WriteLine($"[BETTING AUDIT] No bets found for game {gameId} - returning zero winnings");
                return new ClaimResponseDto { Winnings = 0, TotalClaimed = _totalClaimed };
            }

            // Thay đổi luật: nếu cược đúng, người thắng luôn được nhận x2 số xu đã cược
            // (đã trừ xu khi đặt cược). Điều này đơn giản, dễ hiểu hơn cho người chơi
            // so với chia pool theo tỉ lệ (parimutuel).
            // Ví dụ: cược 50 xu, thắng nhận 100 xu → lãi ròng +50 xu.

            // Settle all pending bets theo luật x2
            var pendingBets = bets.Where(b => b.Status == "pending").ToList();
            Console.WriteLine($"[BETTING AUDIT] Settling {pendingBets.Count} pending bets");
            
            foreach (var bet in pendingBets)
            {
                if (bet.TargetPlayerId == winnerId)
                {
                    // Người cược đúng: nhận x2 số xu đã cược
                    bet.Payout = bet.Amount * 2;
                    bet.Status = "won";
                    Console.WriteLine($"[BETTING AUDIT] Bet {bet.Id} WON - Player {bet.PlayerId} bet {bet.Amount} on winner, payout (x2): {bet.Payout}");
                }
                else
                {
                    // Loser loses all their bet amount (payout = 0)
                    bet.Payout = 0;
                    bet.Status = "lost";
                    Console.WriteLine($"[BETTING AUDIT] Bet {bet.Id} LOST - Player {bet.PlayerId} bet {bet.Amount} on loser, payout: 0");
                }
            }

            // Process point transactions cho tất cả khán giả
            var winningBets = bets.Where(b => b.TargetPlayerId == winnerId && b.Status == "won").ToList();
            var losingBets = bets.Where(b => b.TargetPlayerId != winnerId && b.Status == "lost").ToList();
            var totalWinnings = winningBets.Sum(b => b.Payout ?? 0);

            try
            {
                // Process winnings for spectators who bet on the winning player
                foreach (var bet in winningBets)
                {
                    if (bet.Payout > 0)
                    {
                        Console.WriteLine($"[BETTING SERVICE] Adding {bet.Payout} points to spectator {bet.PlayerId} (bet amount: {bet.Amount})");
                        await _pointTransactionService.AddPointsAsync(
                            Guid.Parse(bet.PlayerId), // Spectator who won
                            (int)bet.Payout.Value, 
                            "Betting Winnings"
                        );
                    }
                    bet.Status = "claimed";
                }

                // Process losses for spectators who bet on the losing player
                foreach (var bet in losingBets)
                {
                    // No point transaction needed - they already lost their points when placing the bet
                    bet.Status = "claimed";
                }

                _totalClaimed += totalWinnings;

                Console.WriteLine($"[BETTING SERVICE] Processed betting transactions - Winners: {winningBets.Count} spectators (+{totalWinnings}), Losers: {losingBets.Count} spectators");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BETTING SERVICE] Error processing betting transactions: {ex.Message}");
            }
            Console.WriteLine($"[BETTING AUDIT] ClaimWinnings completed - Total winnings: {totalWinnings}, Total claimed: {_totalClaimed}");
            
            return new ClaimResponseDto
            {
                Winnings = totalWinnings,
                TotalClaimed = _totalClaimed
            };
        }

        public object GetBettingStatistics(string gameId)
        {
            var bets = _bets.Where(b => b.GameId == gameId).ToList();
            
            if (!bets.Any())
            {
                return new
                {
                    totalBets = 0,
                    totalPool = 0,
                    averageBet = 0m,
                    largestBet = 0m,
                    smallestBet = 0m,
                    hasBets = false
                };
            }

            var totalPool = bets.Sum(b => b.Amount);
            var betAmounts = bets.Select(b => b.Amount).ToList();
            
            return new
            {
                totalBets = bets.Count,
                totalPool = totalPool,
                averageBet = betAmounts.Average(),
                largestBet = betAmounts.Max(),
                smallestBet = betAmounts.Min(),
                hasBets = true,
                recentBets = bets
                    .OrderByDescending(b => b.Timestamp)
                    .Take(10)
                    .Select(b => new 
                    { 
                        id = b.Id, 
                        playerId = b.PlayerId, 
                        amount = b.Amount, 
                        timestamp = b.Timestamp,
                        status = b.Status
                    })
                    .ToList()
            };
        }

        public async Task<List<Bet>> GetBetsForRoomAsync(string roomId)
        {
            try
            {
                await Task.CompletedTask; // Make it async
                var bets = _bets.Where(b => b.GameId == roomId).ToList();

                Console.WriteLine($"[BETTING SERVICE] Retrieved {bets.Count} bets for room {roomId}");
                return bets;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BETTING SERVICE] Error getting bets for room {roomId}: {ex.Message}");
                return new List<Bet>();
            }
        }
    }
}

