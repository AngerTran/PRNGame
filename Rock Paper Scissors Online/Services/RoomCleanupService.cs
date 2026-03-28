
using Microsoft.AspNetCore.SignalR;
using Rock_Paper_Scissors_Online.DTOs;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class RoomCleanupService : IHostedService, IDisposable
    {
        //IHostedService: dịch vụ chạy nền trong ASP.NET Core khời động và tắt cùng lúc với ứng dụng web
        //IDisposable: giải phóng tài nguyên không quản lý như Timer
        private readonly ILogger<RoomCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory; // lấy các scope service mới
        private Timer? _timer;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30); // khoảng thời gian dọn dẹp (30s) có thể chuyển sang phút

        public RoomCleanupService(ILogger<RoomCleanupService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        // hàm sẽ chạy khi ứng dụng khởi động
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GameRoom Cleanup Service is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _cleanupInterval); // bắt đầu đặt đồng hồ
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                // bắt đầu lấy cái serivice cần thiết từ scope mới
                var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
                var mapper = scope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>();

                try
                {
                    _logger.LogDebug("Checking for timeout rooms...");
                    var timeoutRooms = await roomService.GetTimeoutRoomAsync(); // check phòng hết hạn

                    foreach (var GameRoom in timeoutRooms)
                    {
                        // giả sử player1 là người tạo phòng, rời đi để kích hoạt dọn dẹp
                        var (success, message, _) = await roomService.LeaveRoomAsync(GameRoom.Id, GameRoom.Player1?.UserId ?? string.Empty);
                        if (success)
                        {
                            _logger.LogInformation($"GameRoom {GameRoom.Name}, ID {GameRoom.Id} has been cleaned up due to timeout.");

                            // Thông báo cho tất cả client biết phòng đã bị xóa
                            await hubContext.Clients.All.SendAsync("RoomDeleted", new
                            {
                                success = true,
                                message = $"GameRoom '{GameRoom.Name}' has been deleted due to inactivity.",
                                RoomId = GameRoom.Id,
                            });
                            // Cập nhật danh sách phòng cho tất cả client
                            await BroadcastRoomList(hubContext, roomService, mapper);
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to clean up GameRoom {GameRoom.Name}, ID {GameRoom.Id}: {message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during GameRoom cleanup.");
                }
            }
        }

        private async Task BroadcastRoomList(IHubContext<GameHub> hubContext, IRoomService roomService, AutoMapper.IMapper mapper)
        {
            try
            {
                var rooms = await roomService.GetAllRoomsAsync();
                var roomResponses = mapper.Map<List<RoomResponseDto>>(rooms);

                foreach (var roomResponse in roomResponses)
                {
                    if (roomResponse.IsPrivate)
                    {
                        roomResponse.PinCode = null; // ẩn pincode nếu phòng riêng tư
                    }
                }
                await hubContext.Clients.All.SendAsync("UpdateListRoom", new
                {
                    success = true,
                    data = new { rooms = roomResponses }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting GameRoom list.");
            }
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GameRoom Cleanup Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            _timer?.Dispose(); // giải phóng tài nguyên timer khi không còn sử dụng
        }

    }
}
