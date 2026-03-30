using Microsoft.Extensions.Configuration;
using Npgsql;

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
        string? raw = null;
        var cs = config.GetConnectionString("PostgresConnection");
        if (!string.IsNullOrWhiteSpace(cs))
            raw = cs;
        else
        {
            foreach (var envKey in EnvironmentOnlyKeys)
            {
                var v = Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrWhiteSpace(v))
                {
                    raw = v;
                    break;
                }
            }
        }

        if (raw == null)
            return null;

        raw = SanitizeRaw(raw);
        if (raw.Length == 0)
            return null;

        raw = NormalizePostgresUriScheme(raw);

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(raw);
            return builder.ConnectionString;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "Chuỗi PostgreSQL không hợp lệ với Npgsql (thường do copy thừa dấu ngoặc, ký tự ẩn, hoặc URL sai). "
                + "Trên Render: dán nguyên Internal Database URL vào DATABASE_URL (một dòng, không bọc \"). "
                + "Hoặc dùng dạng Npgsql: Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require "
                + "Lỗi chi tiết: " + ex.Message,
                ex);
        }
    }

    public static string BuildMissingConnectionExceptionMessage() =>
        "Thiếu chuỗi kết nối PostgreSQL. "
        + "Trên Render: mở dịch vụ PostgreSQL → tab Info / Connect → copy Internal Database URL (dạng postgresql://...). "
        + "Mở Web Service (app) → Environment → thêm biến Key = DATABASE_URL hoặc ConnectionStrings__PostgresConnection, Value = URL vừa copy (hai dấu __ trong ConnectionStrings__PostgresConnection). "
        + "Save → Manual Deploy. Không dùng Host=localhost trên cloud. "
        + "Nếu chưa có PostgreSQL trên Render: New → PostgreSQL → tạo DB rồi làm các bước trên.";

    private static string SanitizeRaw(string value)
    {
        var s = value.Trim();
        if (s.Length > 0 && s[0] == '\uFEFF')
            s = s.TrimStart('\uFEFF').Trim();

        if (s.Length >= 2)
        {
            var f = s[0];
            var l = s[^1];
            if ((f == '"' && l == '"') || (f == '\'' && l == '\''))
                s = s[1..^1].Trim();
        }

        return s;
    }

    private static string NormalizePostgresUriScheme(string value)
    {
        const string legacy = "postgres://";
        if (value.StartsWith(legacy, StringComparison.OrdinalIgnoreCase))
            return "postgresql://" + value[legacy.Length..];
        return value;
    }
}
