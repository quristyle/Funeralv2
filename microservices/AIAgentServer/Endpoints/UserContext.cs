using System.Reflection;

namespace AIAgentServer;

/// <summary>게이트웨이가 붙여 준 사용자 정보. <b>이 서비스가 사람을 아는 유일한 길이다.</b> 다른 서비스들이 쓰는 것과 같은 모양·같은 헤더다(AuthServer 의 것을 그대로 옮겼다).</summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>관리자 계열 판정 (시스템관리자 또는 관리자).</summary>
    public bool IsAdmin =>
        Roles.Contains("ADMINISTRATOR") || Roles.Contains("SYSTEM_ADMINISTRATOR") ||
        Role is "ADMINISTRATOR" or "SYSTEM_ADMINISTRATOR";

    /// <summary>Minimal API 매개변수 바인딩 로직</summary>
    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        var role = context.Request.Headers["X-User-Role"].ToString();
        var rolesHeader = context.Request.Headers["X-User-Roles"].ToString();
        var roles = string.IsNullOrWhiteSpace(rolesHeader)
            ? (string.IsNullOrEmpty(role) ? [] : new[] { role })
            : rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var companyId = context.Request.Headers["X-User-Company-Id"].ToString();

        // 사용자 ID가 없는 경우 null 반환 (엔드포인트에서 체크 가능)
        if (string.IsNullOrEmpty(userId))
        {
            return ValueTask.FromResult<UserContext?>(null);
        }

        var result = new UserContext
        {
            UserId = userId,
            Role = role,
            Roles = roles,
            CompanyId = companyId
        };

        return ValueTask.FromResult<UserContext?>(result);
    }
}
