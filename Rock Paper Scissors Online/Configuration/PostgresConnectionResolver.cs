using Microsoft.Extensions.Configuration;

namespace Rock_Paper_Scissors_Online.Configuration;

/// <summary>
/// Chuỗi kết nối Postgres: <c>ConnectionStrings:PostgresConnection</c> / <c>ConnectionStrings__PostgresConnection</c>,
/// hoặc URI từ <c>DATABASE_URL</c> (Render), <c>POSTGRES_URL</c> (Railway).
/// </summary>
public static class PostgresConnectionResolver
{
    private static readonly string[] EnvUriKeys = ["DATABASE_URL", "POSTGRES_URL"];

    public static string? Resolve(IConfiguration config)
    {
        var cs = config.GetConnectionString("PostgresConnection");
        if (!string.IsNullOrWhiteSpace(cs))
            return Normalize(cs.Trim());

        foreach (var envKey in EnvUriKeys)
        {
            var url = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(url))
                return Normalize(url.Trim());
        }

        return null;
    }

    /// <summary>Chuẩn hóa <c>postgres://</c> thành <c>postgresql://</c> cho Npgsql.</summary>
    public static string Normalize(string value)
    {
        const string legacy = "postgres://";
        if (value.StartsWith(legacy, StringComparison.OrdinalIgnoreCase))
            return "postgresql://" + value[legacy.Length..];
        return value;
    }
}
