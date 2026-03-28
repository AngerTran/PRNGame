using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IPointTransactionService
    {
        Task<PointTransactionResult> ProcessGameResultAsync(string winnerId, string loserId, int pointsPerWin, string gameId);
        Task<PointTransactionResult> AddPointsAsync(Guid userId, int points, string reason, string type = "credit");
        Task<PointTransactionResult> SubtractPointsAsync(Guid userId, int points, string reason, string type = "debit");
        Task<long> GetUserPointsAsync(Guid userId);
    }

    public class PointTransactionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long WinnerPoints { get; set; }
        public long LoserPoints { get; set; }
        public PointTransaction? WinnerTransaction { get; set; }
        public PointTransaction? LoserTransaction { get; set; }
    }
}
