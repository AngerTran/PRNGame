using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Models;
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

        private static bool IsCompleted(History h) =>
            string.Equals(h.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.Status, "Finished", StringComparison.OrdinalIgnoreCase);

        /// <summary>Bản ghi từ hub: CreatorUserId = người thắng, OpponentId = người thua, Points = điểm ăn thua trận.</summary>
        private static void MapResult(History h, Guid userId, out string result, out long earned, out long lost)
        {
            result = h.Status;
            earned = 0;
            lost = 0;

            if (!IsCompleted(h))
            {
                result = h.Status;
                return;
            }

            var won = h.CreatorUserId == userId;
            var lostGame = h.OpponentId == userId;
            if (won)
            {
                result = "win";
                earned = h.Points;
            }
            else if (lostGame)
            {
                result = "loss";
                lost = h.Points;
            }
            else
            {
                result = "other";
            }
        }

        public async Task<MatchHistoryResponse?> GetMatchHistoryAsync(Guid userId, int page, int pageSize)
        {
            var totalCount = await _historyRepository.CountForUserAsync(userId);
            var skip = (page - 1) * pageSize;
            var list = await _historyRepository.GetPagedForUserAsync(userId, skip, pageSize);

            var matches = list.Select(h =>
            {
                MapResult(h, userId, out var result, out var earned, out var lost);
                return new MatchHistoryDto
                {
                    Id = h.Id.ToString(),
                    OpponentUsername = h.CreatorUserId == userId
                        ? (h.Opponent != null ? h.Opponent.Username : "Unknown")
                        : (h.CreatorUser != null ? h.CreatorUser.Username : "Unknown"),
                    Result = result,
                    PointsEarned = earned,
                    PointsLost = lost,
                    RoundsPlayed = h.MaxRounds,
                    PlayedAt = h.FinishedAt != default ? h.FinishedAt.UtcDateTime : h.CreatedAt.UtcDateTime,
                    Duration = 0,
                    RoomName = h.Name
                };
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
