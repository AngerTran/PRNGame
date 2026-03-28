using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class RoomCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RoomCleanupBackgroundService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // Run every 5 minutes

        public RoomCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<RoomCleanupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Room Cleanup Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var roomManagementService = scope.ServiceProvider.GetRequiredService<IEnhancedRoomManagementService>();
                        
                        // Clean up inactive rooms
                        var cleanupResult = await roomManagementService.CleanupInactiveRoomsAsync();
                        
                        if (cleanupResult)
                        {
                            _logger.LogInformation("Room cleanup completed successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Room cleanup completed with issues");
                        }

                        // Get room management stats for monitoring
                        var stats = await roomManagementService.GetRoomManagementStatsAsync();
                        _logger.LogInformation("Room Management Stats - Total: {TotalRooms}, Active: {ActiveRooms}, Waiting: {WaitingRooms}", 
                            stats.TotalRooms, stats.ActiveRooms, stats.WaitingRooms);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during room cleanup");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }

            _logger.LogInformation("Room Cleanup Background Service stopped");
        }
    }
}
