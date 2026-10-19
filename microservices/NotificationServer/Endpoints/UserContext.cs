using System.Reflection;

namespace NotificationServer.Endpoints;

/// <summary>게이트웨이가 붙여 준 신원(X-User-* 헤더). 외부에서 온 같은 이름의 헤더는 게이트웨이가 먼저 지운다.</summary>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }

    /// <summary>최소 엔드포인트의 매개변수로 바로 받을 수 있게 하는 바인더.</summary>
    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return ValueTask.FromResult<UserContext?>(null);
        }

        // 이름은 한글이라 게이트웨이가 URL 인코딩해서 보낸다. 되돌린다.
        var name = context.Request.Headers["X-User-Name"].ToString();
        if (!string.IsNullOrWhiteSpace(name))
        {
            try { name = Uri.UnescapeDataString(name); }
            catch (UriFormatException) { /* 인코딩이 아니면 그대로 쓴다 */ }
        }

        return ValueTask.FromResult<UserContext?>(new UserContext
        {
            UserId = userId,
            Role = context.Request.Headers["X-User-Role"].ToString(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            Email = context.Request.Headers["X-User-Email"].ToString()
        });
    }
}
