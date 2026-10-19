using System.Reflection;

namespace CargoTrustServer.Endpoints;

/// <summary>게이트웨이가 붙여 주는 X-User-* 헤더를 읽는다. 헤더가 없으면 null(직접 호출). 다른 서비스와 같은 복사본이다.</summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];

    /// <summary>X-User-Name 은 게이트웨이가 URL 인코딩해 보낸다(한글).</summary>
    public string UserName { get; set; } = string.Empty;

    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(userId))
            return ValueTask.FromResult<UserContext?>(null);

        var roles = context.Request.Headers["X-User-Roles"].ToString();
        var name = context.Request.Headers["X-User-Name"].ToString();
        return ValueTask.FromResult<UserContext?>(new UserContext
        {
            UserId = userId,
            Role = context.Request.Headers["X-User-Role"].ToString(),
            Roles = string.IsNullOrEmpty(roles) ? [] : roles.Split(','),
            UserName = string.IsNullOrEmpty(name) ? "" : Uri.UnescapeDataString(name),
        });
    }
}
