using System.Reflection;

namespace SiteServer.Endpoints;

/// <summary>
/// 게이트웨이가 붙여 준 사용자 정보. 헤더가 없으면 <c>null</c> 로 바인딩된다.
/// </summary>
/// <remarks>
/// 게이트웨이는 외부에서 보낸 같은 이름의 헤더를 무조건 지우고 자기가 검증한 값으로 다시 넣는다
/// (ApiGateway/Program.cs). 그래서 이 헤더가 있다는 것은 게이트웨이를 통과한 인증된 요청이라는 뜻이다.
/// 다른 서비스(FileServer · AuthServer)와 같은 모양을 유지한다.
/// </remarks>
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
