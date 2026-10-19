using JSini.Web.Http;

namespace JSini.Web.Shell.Security;

/// <summary>로그인하기 전에 부르는 계정 경로(가입 신청·비밀번호 찾기). 토큰을 붙이지 않는 클라이언트로 부른다. 앞선 세션의 만료 토큰 하나로 로그인 화면에 튕기는 것을 막는다. 로그인 흐름의 일부라 셸에 둔다.</summary>
public sealed class AccountClient
{
    private readonly GatewayClient _anonymous;

    public AccountClient(IHttpClientFactory factory)
    {
        // 봉투 해석은 GatewayClient 와 같고, 토큰 핸들러가 붙지 않은 HttpClient 만 다르다.
        _anonymous = new GatewayClient(
            factory.CreateClient(ServiceCollectionExtensions.AnonymousClientName));
    }

    /// <summary>비밀번호 재설정 링크를 보내 달라고 한다. 아이디가 있든 없든 서버가 성공으로 답하므로 예외가 오지 않는다. 답이 갈리면 아이디를 확인해 주는 도구가 된다. 화면도 늘 같은 안내를 띄운다.</summary>
    public Task RequestPasswordResetAsync(string loginId, string email, CancellationToken ct = default)
        => _anonymous.PostAsync("auth/password/forgot", new { loginId, email }, ct);

    /// <summary>메일로 받은 링크로 비밀번호를 다시 정한다. 이유는 구분해 준다(시간 지남·이미 씀·같은 값). 실패하면 ApiException 에 서버 문구가 담긴다.</summary>
    public Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
        => _anonymous.PostAsync("auth/password/reset", new { token, newPassword }, ct);

    /// <summary>가입을 신청한다. 승인 전까지는 로그인할 수 없다.</summary>
    public Task SignupAsync(SignupInput input, CancellationToken ct = default)
        => _anonymous.PostAsync("auth/signup", new
        {
            loginId = input.LoginId,
            userName = input.UserName,
            email = input.Email,
            phone = input.Phone,
            password = input.Password,
            note = input.Note,
        }, ct);
}

/// <summary>가입 신청서에 사람이 적는 것. 부서·역할·상태는 없다. 신청자가 그 칸을 채우면 스스로 역할을 고르게 되므로 승인하는 사람이 붙인다.</summary>
public sealed class SignupInput
{
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Password { get; set; } = string.Empty;

    /// <summary>확인 칸. <b>서버로 보내지 않는다</b> — 오타를 여기서만 잡을 수 있다.</summary>
    public string Confirm { get; set; } = string.Empty;

    public string? Note { get; set; }
}
