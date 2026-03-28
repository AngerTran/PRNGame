using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rock_Paper_Scissors_Online.Components;
using Rock_Paper_Scissors_Online.Hubs;
using Rock_Paper_Scissors_Online.Mapper;
using Rock_Paper_Scissors_Online.Models;
using Rock_Paper_Scissors_Online.Repository;
using Rock_Paper_Scissors_Online.Repository.Interfaces;
using Rock_Paper_Scissors_Online.Services;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            var jwtKey = builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RockPaperScissorsOnline";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RockPaperScissorsClients";

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddAutoMapper(typeof(MappingProfile));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                (path.StartsWithSegments("/gameHub") || path.StartsWithSegments("/chatHub")))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
            builder.Services.AddAuthorization();

            builder.Services.AddSingleton<IRoomService, RoomService>();
            builder.Services.AddSingleton<IGameService, GameService>();
            builder.Services.AddSingleton<ISessionManagementService, SessionManagementService>();
            builder.Services.AddSingleton<IUserTrackerService, UserTrackerService>();
            builder.Services.AddSingleton<IGlobalChatService, GlobalChatService>();
            builder.Services.AddSingleton<IRoomChatService, RoomChatService>();
            builder.Services.AddSingleton<IJwtService, JwtService>();

            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();
            builder.Services.AddScoped<IPointTransactionRepository, PointTransactionRepository>();
            builder.Services.AddScoped<IApplicationMetricsRepository, ApplicationMetricsRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            builder.Services.AddScoped<IHealthMetricsService, HealthMetricsService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
            builder.Services.AddScoped<IMatchHistoryService, MatchHistoryService>();
            builder.Services.AddScoped<IOnlinePlayersService, OnlinePlayersService>();
            builder.Services.AddScoped<IPointTransactionService, PointTransactionService>();
            builder.Services.AddScoped<IBettingService, BettingService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IProfileService, ProfileService>();
            builder.Services.AddScoped<IPlayerService, PlayerService>();
            builder.Services.AddScoped<IEnhancedRoomManagementService, EnhancedRoomManagementService>();

            builder.Services.AddHostedService<RoomCleanupBackgroundService>();
            builder.Services.AddHostedService<RoomCleanupService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapControllers();
            app.MapHub<GameHub>("/gameHub");
            app.MapHub<ChatHub>("/chatHub");
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
