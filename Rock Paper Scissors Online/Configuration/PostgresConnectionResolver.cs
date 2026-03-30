using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rock_Paper_Scissors_Online.Configuration;

/// <summary>
/// Gom chuỗi Postgres từ env / config. URI <c>postgresql://</c> luôn parse thủ công (không dùng ctor Npgsql với URI — tránh lỗi “index 0”).
/// </summary>
public static class PostgresConnectionResolver
{
    private static readonly string[] UriEnvKeys =
    [
        "DATABASE_URL",
        "POSTGRES_URL",
        "POSTGRES_PRISMA_URL",
    ];

    private static readonly string[] ConnEnvKeys =
    [
        "ConnectionStrings__PostgresConnection",
        "ConnectionStrings_PostgresConnection",
    ];

    public static string? Resolve(IConfiguration config)
    {
        var candidates = CollectCandidates(config);
        if (candidates.Count == 0)
            return null;

        Exception? last = null;
        foreach (var raw in candidates)
        {
            var cleaned = Clean(raw);
            if (cleaned.Length == 0)
                continue;
            try
            {
                return ToNpgsqlConnectionString(cleaned);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        if (last != null)
        {
            throw new InvalidOperationException(
                "Không đọc được chuỗi PostgreSQL. Chỉ giữ DATABASE_URL = Internal URL (Copy một dòng), hoặc chuỗi Npgsql Host=...;Username=...;Password=...;Database=... "
                + "Lỗi: " + last.Message,
                last);
        }

        throw new InvalidOperationException(
            "Có biến DATABASE_URL / ConnectionStrings__PostgresConnection nhưng sau khi làm sạch không còn nội dung hợp lệ.");
    }

    private static List<string> CollectCandidates(IConfiguration config)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void TryAdd(string? v)
        {
            if (string.IsNullOrWhiteSpace(v))
                return;
            var t = v.Trim();
            if (seen.Add(t))
                list.Add(t);
        }

        foreach (var k in UriEnvKeys)
            TryAdd(Environment.GetEnvironmentVariable(k));
        foreach (var k in ConnEnvKeys)
            TryAdd(Environment.GetEnvironmentVariable(k));
        TryAdd(config.GetConnectionString("PostgresConnection"));

        return list;
    }

    private static string Clean(string value)
    {
        var s = value.Trim();
        while (s.Length > 0 && (s[0] == '\uFEFF' || char.IsControl(s[0])))
            s = s[1..].TrimStart();

        if (s.Length >= 2)
        {
            var f = s[0];
            var l = s[^1];
            if ((f == '"' && l == '"') || (f == '\'' && l == '\''))
                s = s[1..^1].Trim();
        }

        var nl = s.IndexOfAny(['\r', '\n']);
        if (nl >= 0)
            s = s[..nl].Trim();

        foreach (var marker in new[] { "postgresql://", "postgres://", "Host=", "host=" })
        {
            var i = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i > 0)
                s = s[i..].TrimStart();
        }

        return s;
    }

    private static string ToNpgsqlConnectionString(string cleaned)
    {
        cleaned = NormalizeScheme(cleaned);
        if (cleaned.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return FromPostgresUri(cleaned);
        return FromKeywordFormat(cleaned);
    }

    private static string FromPostgresUri(string uriString)
    {
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Không parse được URI PostgreSQL.");

        if (string.IsNullOrEmpty(uri.Host))
            throw new InvalidOperationException("URI thiếu host.");

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        var path = uri.AbsolutePath.TrimStart('/');
        var dbEnd = path.IndexOf('?');
        var database = dbEnd >= 0 ? path[..dbEnd] : path;
        if (string.IsNullOrEmpty(database))
            throw new InvalidOperationException("URI thiếu tên database trong path.");

        var port = uri.Port;
        if (port <= 0)
            port = 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = user,
            Password = pass,
        };

        ApplySslModeFromQuery(builder, uri.Query);
        ApplyHostSslDefaults(builder);

        return builder.ConnectionString;
    }

    private static string FromKeywordFormat(string keywordString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(keywordString);
            if (builder.Port <= 0)
                builder.Port = 5432;
            ApplyHostSslDefaults(builder);
            return builder.ConnectionString;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Chuỗi dạng Host=... không hợp lệ với Npgsql.", ex);
        }
    }

    private static void ApplySslModeFromQuery(NpgsqlConnectionStringBuilder builder, string query)
    {
        if (string.IsNullOrEmpty(query))
            return;
        var q = query.TrimStart('?');
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;
            if (!kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                continue;
            var v = Uri.UnescapeDataString(kv[1]).ToLowerInvariant();
            builder.SslMode = v switch
            {
                "require" => SslMode.Require,
                "disable" => SslMode.Disable,
                "prefer" => SslMode.Prefer,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                _ => SslMode.Prefer
            };
            return;
        }
    }

    /// <summary>Host nội bộ Render (dpg-… không có dấu chấm): Prefer để tránh Require + chứng chỉ.</summary>
    private static void ApplyHostSslDefaults(NpgsqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrEmpty(builder.Host))
            return;
        if (!builder.Host.Contains('.', StringComparison.Ordinal) && builder.SslMode == SslMode.Require)
            builder.SslMode = SslMode.Prefer;
    }

    public static string BuildMissingConnectionExceptionMessage() =>
        "Thiếu chuỗi kết nối PostgreSQL. Render: PostgreSQL → Connect → Internal URL → Web Service → DATABASE_URL (một dòng). Save → deploy lại.";

    private static string NormalizeScheme(string value)
    {
        const string legacy = "postgres://";
        if (value.StartsWith(legacy, StringComparison.OrdinalIgnoreCase))
            return "postgresql://" + value[legacy.Length..];
        return value;
    }
}
