using System.Text;
using System.Text.Json;

namespace JSini.Web.Components.Security;

/// <summary>게이트웨이 토큰(JWT)에 실린 역할을 꺼낸다. 서명은 보지 않는다 — 검증은 게이트웨이가 하고, 여기서는 「DB 의 역할과 같은가」만 견준다.</summary>
public static class TokenRoles
{
    /// <summary>JwtSecurityTokenHandler 는 ClaimTypes.Role 을 "role" 로 줄여 쓴다. 줄이지 않은 모양도 함께 받는다.</summary>
    private static readonly string[] RoleKeys =
    [
        "role",
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
    ];

    /// <summary>토큰의 역할들. 토큰을 못 읽으면 null — 「역할이 없다」와 가른다.</summary>
    public static IReadOnlySet<string>? Read(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(DecodeSegment(parts[1]));
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in RoleKeys)
            {
                if (!payload.RootElement.TryGetProperty(key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } one)
                {
                    roles.Add(one);
                }
                else if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } many)
                        {
                            roles.Add(many);
                        }
                    }
                }
            }

            return roles;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>두 역할 묶음이 같은가(대소문자 무시).</summary>
    public static bool SameAs(IReadOnlySet<string> tokenRoles, IEnumerable<string> dbRoles) =>
        tokenRoles.SetEquals(dbRoles.Where(r => !string.IsNullOrWhiteSpace(r)));

    private static byte[] DecodeSegment(string segment)
    {
        var base64 = segment.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
