using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Controllers
{
    [ApiController]
    [Route("api/v1/match-history")]
    [Authorize]
    public class MatchHistoryController : ControllerBase
    {
        private readonly IMatchHistoryService _matchHistoryService;

        public MatchHistoryController(IMatchHistoryService matchHistoryService)
        {
            _matchHistoryService = matchHistoryService;
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<ApiResponse<MatchHistoryResponse>>> GetMatchHistory(
            string userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(new ApiResponse<MatchHistoryResponse>
                    {
                        Success = false,
                        Message = "Invalid user ID format"
                    });
                }

                var data = await _matchHistoryService.GetMatchHistoryAsync(userGuid, page, pageSize);
                if (data == null)
                {
                    return BadRequest(new ApiResponse<MatchHistoryResponse>
                    {
                        Success = false,
                        Message = "Unable to load match history"
                    });
                }

                return Ok(new ApiResponse<MatchHistoryResponse>
                {
                    Success = true,
                    Message = "Match history retrieved successfully",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<MatchHistoryResponse>
                {
                    Success = false,
                    Message = $"Failed to retrieve match history: {ex.Message}"
                });
            }
        }

        [HttpGet("{userId}/transactions")]
        public async Task<ActionResult<ApiResponse<PointTransactionHistoryResponse>>> GetPointTransactions(
            string userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(new ApiResponse<PointTransactionHistoryResponse>
                    {
                        Success = false,
                        Message = "Invalid user ID format"
                    });
                }

                var data = await _matchHistoryService.GetPointTransactionsAsync(userGuid, page, pageSize);
                if (data == null)
                {
                    return BadRequest(new ApiResponse<PointTransactionHistoryResponse>
                    {
                        Success = false,
                        Message = "Unable to load transactions"
                    });
                }

                return Ok(new ApiResponse<PointTransactionHistoryResponse>
                {
                    Success = true,
                    Message = "Point transactions retrieved successfully",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PointTransactionHistoryResponse>
                {
                    Success = false,
                    Message = $"Failed to retrieve point transactions: {ex.Message}"
                });
            }
        }
    }
}
