using Microsoft.EntityFrameworkCore;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Services.Interfaces;
using Rock_Paper_Scissors_Online.Repository.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class PointTransactionService : IPointTransactionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;

        public PointTransactionService(ApplicationDbContext context, IUserRepository userRepository)
        {
            _context = context;
            _userRepository = userRepository;
        }

        public async Task<PointTransactionResult> ProcessGameResultAsync(string winnerId, string loserId, int pointsPerWin, string gameId)
        {
            try
            {
                Console.WriteLine($"[POINT SERVICE] Processing game result - Winner: {winnerId}, Loser: {loserId}, Points: {pointsPerWin}");

                // Get users from database
                var winner = await _userRepository.GetByIdAsync(Guid.Parse(winnerId));
                var loser = await _userRepository.GetByIdAsync(Guid.Parse(loserId));

                if (winner == null || loser == null)
                {
                    return new PointTransactionResult
                    {
                        Success = false,
                        Message = "Winner or loser not found in database"
                    };
                }

                // Create point transactions
                var winnerTransaction = new PointTransaction
                {
                    Id = 0, // Will be set by database
                    UserId = winner.Id,
                    Delta = pointsPerWin,
                    Reason = "Game Win",
                    Type = "credit",
                    HistoryId = Guid.Parse(gameId),
                    CreatedAt = DateTime.UtcNow
                };

                var loserTransaction = new PointTransaction
                {
                    Id = 0, // Will be set by database
                    UserId = loser.Id,
                    Delta = -pointsPerWin,
                    Reason = "Game Loss",
                    Type = "debit",
                    HistoryId = Guid.Parse(gameId),
                    CreatedAt = DateTime.UtcNow
                };

                // Update user points
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

                // Add transactions to context
                _context.PointTransactions.Add(winnerTransaction);
                _context.PointTransactions.Add(loserTransaction);

                // Update users in context
                _context.Users.Update(winner);
                _context.Users.Update(loser);

                // Save changes to database
                await _context.SaveChangesAsync();

                Console.WriteLine($"[POINT SERVICE] Points updated - Winner: {winner.Username} (+{pointsPerWin} = {winner.Points}), Loser: {loser.Username} (-{pointsPerWin} = {loser.Points})");

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
                Console.WriteLine($"[POINT SERVICE] Error processing game result: {ex.Message}");
                return new PointTransactionResult
                {
                    Success = false,
                    Message = $"Error processing points: {ex.Message}"
                };
            }
        }

        public async Task<PointTransactionResult> AddPointsAsync(Guid userId, int points, string reason, string type = "credit")
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return new PointTransactionResult
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                var transaction = new PointTransaction
                {
                    Id = 0,
                    UserId = userId,
                    Delta = points,
                    Reason = reason,
                    Type = type,
                    CreatedAt = DateTime.UtcNow
                };

                user.Points += points;
                user.UpdatedAt = DateTime.UtcNow;

                _context.PointTransactions.Add(transaction);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return new PointTransactionResult
                {
                    Success = true,
                    Message = "Points added successfully",
                    WinnerPoints = user.Points
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[POINT SERVICE] Error adding points: {ex.Message}");
                return new PointTransactionResult
                {
                    Success = false,
                    Message = $"Error adding points: {ex.Message}"
                };
            }
        }

        public async Task<PointTransactionResult> SubtractPointsAsync(Guid userId, int points, string reason, string type = "debit")
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return new PointTransactionResult
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                var transaction = new PointTransaction
                {
                    Id = 0,
                    UserId = userId,
                    Delta = -points,
                    Reason = reason,
                    Type = type,
                    CreatedAt = DateTime.UtcNow
                };

                user.Points = Math.Max(0, user.Points - points); // Prevent negative points
                user.UpdatedAt = DateTime.UtcNow;

                _context.PointTransactions.Add(transaction);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return new PointTransactionResult
                {
                    Success = true,
                    Message = "Points subtracted successfully",
                    WinnerPoints = user.Points
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[POINT SERVICE] Error subtracting points: {ex.Message}");
                return new PointTransactionResult
                {
                    Success = false,
                    Message = $"Error subtracting points: {ex.Message}"
                };
            }
        }

        public async Task<long> GetUserPointsAsync(Guid userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new ArgumentException("User not found");
                }

                return user.Points;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[POINT SERVICE] Error getting user points: {ex.Message}");
                throw;
            }
        }
    }
}