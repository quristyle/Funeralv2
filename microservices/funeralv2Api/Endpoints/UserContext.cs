using System.Reflection;

namespace funeralv2Api;

/// <summary>
/// 게이트웨이로부터 전달받은 사용자 인증 정보 컨텍스트
/// </summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>첫 번째 역할 (X-User-Role). 역할이 여럿이면 <see cref="Roles"/> 를 본다.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>전체 역할 목록 (X-User-Roles, 쉼표 구분)</summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// 장비 제어(전원·재시작)와 출상 취소가 허용되는 역할 (47번 문서 D-RS4).
    /// 옛 시스템의 SUPER_ADMIN · PARTER_ADMIN 검사를 포털 역할로 옮긴 것이다.
    /// </summary>
    private static readonly string[] ControlRoles =
    [
        "ADMINISTRATOR",
        "SYSTEM_ADMINISTRATOR",
        "PARTNER_ADMINISTRATOR",
    ];

    /// <summary>장비 제어·출상 취소를 할 수 있는 계정인지</summary>
    public bool CanControlDevices => Roles.Any(r => ControlRoles.Contains(r));

    /// <summary>
    /// Minimal API 매개변수 바인딩 로직
    /// </summary>
    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        var role = context.Request.Headers["X-User-Role"].ToString();
        var roles = context.Request.Headers["X-User-Roles"].ToString();

        // 사용자 ID가 없는 경우 null 반환 (엔드포인트에서 체크 가능)
        if (string.IsNullOrEmpty(userId))
        {
            return ValueTask.FromResult<UserContext?>(null);
        }

        var result = new UserContext
        {
            UserId = userId,
            Role = role,
            Roles = string.IsNullOrWhiteSpace(roles)
                ? (string.IsNullOrWhiteSpace(role) ? [] : [role])
                : roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        };

        return ValueTask.FromResult<UserContext?>(result);
    }
}
