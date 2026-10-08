using System.Reflection;

namespace AIAgentServer;

/// <summary>
/// 게이트웨이가 붙여 준 사용자 정보. <b>이 서비스가 사람을 아는 유일한 길이다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 다른 서비스들이 쓰는 것과 <b>같은 모양·같은 헤더</b>다(AuthServer 의 것을
/// 그대로 옮겼다). 공용으로 올리지 않은 까닭도 그쪽들과 같다 — 서비스마다
/// 필요한 칸이 달라지는데 한 벌로 묶으면 한 곳을 고칠 때 전부 따라 움직인다.
/// </para>
/// <para>
/// <b>바깥에서 보낸 같은 이름의 헤더는 믿어도 된다.</b> 게이트웨이가 들어오는
/// <c>X-User-*</c> 를 전부 지우고 검증한 토큰으로 다시 만들기 때문이다
/// (ApiGateway/Program.cs). 다만 이 서비스는 게이트웨이를 거치지 않는 직접
/// 호출도 받으므로(사용량 조회 같은 자리) <b>그 자리마다 역할을 한 번 더 본다.</b>
/// </para>
/// </remarks>
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>
    /// Minimal API 매개변수 바인딩 로직
    /// </summary>
    public static ValueTask<UserContext?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        var role = context.Request.Headers["X-User-Role"].ToString();
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
            CompanyId = companyId
        };

        return ValueTask.FromResult<UserContext?>(result);
    }
}
