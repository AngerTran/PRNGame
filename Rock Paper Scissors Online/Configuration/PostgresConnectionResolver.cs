using Microsoft.Extensions.Configuration;

namespace Rock_Paper_Scissors_Online.Configuration;

/// <summary>
/// Chuỗi kết nối Postgres: <c>ConnectionStrings:PostgresConnection</c> / <c>ConnectionStrings__PostgresConnection</c>,
/// hoặc URI từ <c>DATABASE_URL</c> (Render), <c>POSTGRES_URL</c> (Railway).
/// </summary>
public static class PostgresConnectionResolver
{
    /// <summary>Đọc trực tiếp từ OS (một số host map vào IConfiguration không đều).</summary>
    private static readonly string[] EnvironmentOnlyKeys =
    [
        "ConnectionStrings__PostgresConnection",
        "DATABASE_URL",
        "POSTGRES_URL",
        "POSTGRES_PRISMA_URL",
        "ConnectionStrings_PostgresConnection", // hay nhầm một dấu _
    ];

    public static string? Resolve(IConfiguration config)
    {
        var cs = config.GetConnectionString("PostgresConnection");
        if (!string.IsNullOrWhiteSpace(cs))
            return Normalize(cs.Trim());

        foreach (var envKey in EnvironmentOnlyKeys)
        {
            var v = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(v))
                return Normalize(v.Trim());
        }

        return null;
    }

    public static string BuildMissingConnectionExceptionMessage() =>
        "Thiếu chuỗi kết nối PostgreSQL. "
        + "Trên Render: mở dịch vụ PostgreSQL → tab Info / Connect → copy Internal Database URL (dạng postgresql://...). "
        + "Mở Web Service (app) → Environment → thêm biến Key = DATABASE_URL hoặc ConnectionStrings__PostgresConnection, Value = URL vừa copy (hai dấu __ trong ConnectionStrings__PostgresConnection). "
        + "Save → Manual Deploy. Không dùng Host=localhost trên cloud. "
        + "Nếu chưa có PostgreSQL trên Render: New → PostgreSQL → tạo DB rồi làm các bước trên.";

    /// <summary>Chuẩn hóa <c>postgres://</c> thành <c>postgresql://</c> cho Npgsql.</summary>
    public static string Normalize(string value)
    {
        const string legacy = "postgres://";
        if (value.StartsWith(legacy, StringComparison.OrdinalIgnoreCase))
            return "postgresql://" + value[legacy.Length..];
        return value;
    }
}
