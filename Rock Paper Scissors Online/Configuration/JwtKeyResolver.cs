using Microsoft.Extensions.Configuration;

namespace Rock_Paper_Scissors_Online.Configuration;

/// <summary>
/// Lấy khóa ký JWT: <c>Jwt:Key</c> từ cấu hình, hoặc từ biến môi trường (nhiều tên hay gặp trên Render/Railway).
/// </summary>
public static class JwtKeyResolver
{
    /// <summary>Thứ tự ưu tiên tên biến khi không có trong JSON / User Secrets.</summary>
    private static readonly string[] EnvironmentVariableNames =
    [
        "Jwt__Key",   // ASP.NET: Jwt:Key (hai dấu gạch dưới)
        "JWT__KEY",
        "JWT_KEY",    // hay gõ nhầm một gạch
        "Jwt_Key",
        "JWT_SECRET",
    ];

    public static string? Resolve(IConfiguration configuration)
    {
        var fromConfig = configuration["Jwt:Key"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        foreach (var name in EnvironmentVariableNames)
        {
            var v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    public static string BuildMissingKeyExceptionMessage() =>
        "Jwt:Key chưa cấu hình hoặc đang rỗng. "
        + "Trên Render: Environment → thêm Key chính xác Jwt__Key (hai dấu __), Value ≥32 ký tự, Save rồi Deploy lại. "
        + "Các tên được hỗ trợ: Jwt__Key, JWT__KEY, JWT_KEY, Jwt_Key, JWT_SECRET. "
        + "Development: appsettings.Development.json hoặc User Secrets.";
}
