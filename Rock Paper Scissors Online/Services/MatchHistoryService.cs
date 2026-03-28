using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class MatchHistoryService : IMatchHistoryService
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IPointTransactionRepository _pointTransactionRepository;

        public MatchHistoryService(
            IHistoryRepository historyRepository,
            IPointTransactionRepository pointTransactionRepository)
        {
            _historyRepository = historyRepository;
            _pointTransactionRepository = pointTransactionRepository;
        }

        public async Task<MatchHistoryResponse?> GetMatchHistoryAsync(Guid userId, int page, int pageSize)
        {
            var totalCount = await _historyRepository.CountForUserAsync(userId);
            var skip = (page - 1) * pageSize;
            var list = await _historyRepository.GetPagedForUserAsync(userId, skip, pageSize);

            var matches = list.Select(h => new MatchHistoryDto
            {
                Id = h.Id.ToString(),
                OpponentUsername = h.CreatorUserId == userId
                    ? (h.Opponent != null ? h.Opponent.Username : "Unknown")
                    : (h.CreatorUser != null ? h.CreatorUser.Username : "Unknown"),
                Result = h.Status == "completed"
                    ? (h.CreatorUserId == userId ? "win" : "loss")
                    : "draw",
                PointsEarned = h.CreatorUserId == userId ? h.Points : 0,
                PointsLost = h.CreatorUserId == userId ? 0 : h.Points,
                RoundsPlayed = h.MaxRounds,
                PlayedAt = h.CreatedAt.UtcDateTime,
                Duration = 120,
                RoomName = $"Game Room {h.Id.ToString().Substring(0, Math.Min(8, h.Id.ToString().Length))}"
            }).ToList();

            return new MatchHistoryResponse
            {
                Matches = matches,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PointTransactionHistoryResponse?> GetPointTransactionsAsync(Guid userId, int page, int pageSize)
        {
            var totalCount = await _pointTransactionRepository.CountForUserAsync(userId);
            var skip = (page - 1) * pageSize;
            var rows = await _pointTransactionRepository.GetPagedForUserAsync(userId, skip, pageSize);

            var transactions = rows.Select(pt => new PointTransactionDto
            {
                Id = (int)pt.Id,
                Delta = pt.Delta,
                Description = pt.Reason,
                CreatedAt = pt.CreatedAt.UtcDateTime
            }).ToList();

            return new PointTransactionHistoryResponse
            {
                Transactions = transactions,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
