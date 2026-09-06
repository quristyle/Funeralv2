using JSini.Web.Http;

namespace JSini.Web.Shell.Security;

/// <summary>
/// 로그인하기 <b>전에</b> 부르는 계정 경로들 — 가입 신청 · 비밀번호 찾기.
/// </summary>
/// <remarks>
/// <para>
/// [토큰을 붙이지 않는 클라이언트로 부른다]
/// </para>
///
/// <para>
/// 이 세 경로를 쓰는 사람은 로그인하지 않은 상태다. 토큰을 붙이는
/// <see cref="GatewayClient"/> 로 부르면 앞선 세션의 만료된 토큰 하나 때문에
/// 401 → 갱신 시도 → 로그인 화면으로 튕기는 길이 열린다. 가입 신청을 하려던
/// 사람이 로그인 화면으로 끌려가는 셈이다. <c>NoticeClient</c> 가 공개 공지를
/// 읽을 때와 같은 이유이고 같은 방법이다.
/// </para>
///
/// <para>
/// [셸에 두는 이유]
/// </para>
///
/// <para>
/// 로그인 흐름의 일부라 업무 모듈에 속하지 않는다. 게다가 셸은 업무 모듈을
/// 이름으로 알지 못하므로 모듈에 두면 로그인 화면 옆의 이 화면들이 못 쓴다.
/// </para>
/// </remarks>
public sealed class AccountClient
{
    private readonly GatewayClient _anonymous;

    public AccountClient(IHttpClientFactory factory)
    {
        // 봉투를 벗기는 일은 같으므로 GatewayClient 를 그대로 쓴다.
        // 다른 것은 토큰 핸들러가 걸려 있지 않은 HttpClient 라는 점뿐이다.
        _anonymous = new GatewayClient(
            factory.CreateClient(ServiceCollectionExtensions.AnonymousClientName));
    }

    /// <summary>
    /// 비밀번호 재설정 링크를 보내 달라고 한다.
    ///
    /// <para>
    /// <b>실패해도 예외가 오지 않는다.</b> 서버가 아이디가 있든 없든 언제나
    /// 성공으로 답하기 때문이다 — 답이 갈리면 이 경로가 아이디를 확인해 주는
    /// 도구가 된다. 그래서 화면도 늘 같은 안내를 띄운다.
    /// </para>
    /// </summary>
    public Task RequestPasswordResetAsync(string loginId, string email, CancellationToken ct = default)
        => _anonymous.PostAsync("auth/password/forgot", new { loginId, email }, ct);

    /// <summary>메일로 받은 링크로 비밀번호를 다시 정한다.</summary>
    /// <remarks>
    /// 이쪽은 이유를 구분해 준다(시간 지남 · 이미 씀 · 같은 값 …).
    /// 실패하면 <see cref="ApiException"/> 에 서버가 준 문구가 담겨 온다.
    /// </remarks>
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

/// <summary>
/// 가입 신청서에 사람이 적는 것.
/// </summary>
/// <remarks>
/// 부서·역할·상태가 없다. 신청자가 그 칸을 채우면 <b>스스로 역할을 고르는</b>
/// 셈이라, 그것들은 승인하는 사람이 계정 관리에서 붙인다.
/// </remarks>
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
