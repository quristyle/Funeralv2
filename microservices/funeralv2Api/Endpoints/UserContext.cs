using System.Reflection;

namespace funeralv2Api;

/// <summary>
/// 게이트웨이로부터 전달받은 사용자 인증 정보 컨텍스트
/// </summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Minimal API 매개변수 바인딩 로직
    /// </summary>
    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        var role = context.Request.Headers["X-User-Role"].ToString();

        // 사용자 ID가 없는 경우 null 반환 (엔드포인트에서 체크 가능)
        if (string.IsNullOrEmpty(userId))
        {
            return ValueTask.FromResult<UserContext?>(null);
        }

        var result = new UserContext
        {
            UserId = userId,
            Role = role
        };

        return ValueTask.FromResult<UserContext?>(result);
    }
}
