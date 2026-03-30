using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Rock_Paper_Scissors_Online.Client;
using Rock_Paper_Scissors_Online.Configuration;
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
            // Render/Docker: tránh hết inotify (IOException 1024 / exit 139) khi reload appsettings.
            AppContext.SetSwitch("System.IO.UsePollingFileWatcher", true);
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

            var builder = WebApplication.CreateBuilder(args);

            // Đưa secret từ biến môi trường (Jwt__Key, JWT_KEY, …) vào Jwt:Key để mọi IConfiguration đều thấy.
            var mergedJwt = JwtKeyResolver.Resolve(builder.Configuration);
            if (!string.IsNullOrWhiteSpace(mergedJwt))
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = mergedJwt });

            // Render / Fly / Railway: biến PORT
            var port = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrEmpty(port))
                builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            var dbProvider = builder.Configuration["Database:Provider"]?.Trim() ?? "SqlServer";
            var jwtKey = JwtKeyResolver.Resolve(builder.Configuration)
                ?? throw new InvalidOperationException(JwtKeyResolver.BuildMissingKeyExceptionMessage());
            EnsureJwtKeyIsAcceptable(builder.Environment, jwtKey);
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RockPaperScissorsOnline";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RockPaperScissorsClients";

            if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
                || dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                || dbProvider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                var pg = PostgresConnectionResolver.Resolve(builder.Configuration)
                    ?? throw new InvalidOperationException(PostgresConnectionResolver.BuildMissingConnectionExceptionMessage());
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PostgresConnection"] = pg
                });
                builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(pg));
            }
            else if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                || dbProvider.Equals("Sql", StringComparison.OrdinalIgnoreCase))
            {
                var sql = builder.Configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found when Database:Provider is SqlServer.");
                builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(sql));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Database:Provider '{dbProvider}' is not supported. Use 'SqlServer' or 'Postgres'.");
            }

            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddRazorPages();
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddScoped<ProtectedLocalStorage>();
            builder.Services.AddScoped<AuthTokenStore>();
            builder.Services.AddScoped<GameApiService>();

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

            app.UseForwardedHeaders();

            // Áp migration Postgres khi deploy (Render + PostgreSQL). SQL Server: tự quản schema/migration.
            if (IsPostgresProvider(dbProvider))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.Migrate();
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // Chỉ bật redirect HTTPS khi có cổng HTTPS (tránh cảnh báo khi dùng launch profile chỉ HTTP)
            var httpsPorts = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS");
            if (!app.Environment.IsDevelopment() || !string.IsNullOrEmpty(httpsPorts))
                app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorPages();
            app.MapControllers();
            app.MapHub<GameHub>("/gameHub");
            app.MapHub<ChatHub>("/chatHub");
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }

        private static bool IsPostgresProvider(string? p) =>
            p != null && (p.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
                || p.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                || p.Equals("Npgsql", StringComparison.OrdinalIgnoreCase));

        private static void EnsureJwtKeyIsAcceptable(IHostEnvironment env, string jwtKey)
        {
            if (jwtKey.Length < 32)
                throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 ký tự (ký tự ký hiệu HS256).");

            if (env.IsDevelopment())
                return;

            if (jwtKey.Contains("RockPaperScissorsDevSecretKey", StringComparison.Ordinal)
                || jwtKey.Contains("DevSecret", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Production: không được dùng Jwt:Key dành cho Development. Đặt Jwt__Key ngẫu nhiên, ≥32 ký tự, trong Environment Variables.");
            }
        }
    }
}
