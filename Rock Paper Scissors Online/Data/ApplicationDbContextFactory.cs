using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Rock_Paper_Scissors_Online.Configuration;
using Rock_Paper_Scissors_Online.Models;

namespace Rock_Paper_Scissors_Online.Data;

/// <summary>
/// Cho <c>dotnet ef migrations</c> / <c>database update</c>. Mặc định đọc <c>Database:Provider</c> trong appsettings.
/// Ghi đè tạm: <c>DOTNET_EF_DATABASE_PROVIDER=Postgres</c> hoặc <c>SqlServer</c>.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var provider = Environment.GetEnvironmentVariable("DOTNET_EF_DATABASE_PROVIDER")
                       ?? config["Database:Provider"]
                       ?? "SqlServer";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var cs = PostgresConnectionResolver.Resolve(config)
                     ?? throw new InvalidOperationException(PostgresConnectionResolver.BuildMissingConnectionExceptionMessage());
            optionsBuilder.UseNpgsql(cs);
        }
        else
        {
            var cs = config.GetConnectionString("DefaultConnection")
                     ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");
            optionsBuilder.UseSqlServer(cs);
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
