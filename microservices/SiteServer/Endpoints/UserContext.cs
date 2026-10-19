using System.Reflection;

namespace SiteServer.Endpoints;

/// <summary>게이트웨이가 붙여 준 사용자 정보. 헤더가 없으면 null 로 바인딩된다. 게이트웨이가 외부의 같은 이름 헤더를 지우고 검증한 값으로 다시 넣으므로, 헤더가 있으면 통과한 인증 요청이다.</summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];

    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return ValueTask.FromResult<UserContext?>(null);
        }

        var roles = context.Request.Headers["X-User-Roles"].ToString();

        return ValueTask.FromResult<UserContext?>(new UserContext
        {
            UserId = userId,
            Role = context.Request.Headers["X-User-Role"].ToString(),
            Roles = string.IsNullOrEmpty(roles) ? [] : roles.Split(','),
        });
    }
}
