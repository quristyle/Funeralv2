using System.ComponentModel.DataAnnotations;

namespace JSini.Web.Shell.Components.Pages;

/// <summary>로그인 폼에 담기는 값.</summary>
public sealed class LoginInput
{
    [Required(ErrorMessage = "아이디를 입력하세요.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "비밀번호를 입력하세요.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 브라우저를 닫아도 로그인을 유지할 것인가 (30일).
    ///
    /// <para>
    /// <b>「아이디 기억하기」와 다른 값이다.</b> 그쪽은 아이디만 적어 두는
    /// 브라우저 안의 일이고(theme.js), 이것은 <b>서버가 굽는 인증 쿠키의
    /// 수명</b>을 정한다 — 그래서 이 값은 폼에 실려 서버로 간다.
    /// </para>
    ///
    /// <para>
    /// 홈 화면 앱(PWA)으로 쓰는 사람에게 특히 뜻이 있다. 끄면 세션 쿠키라
    /// 브라우저가 앱을 내렸다 올리는 것만으로 로그인이 풀린다.
    /// </para>
    /// </summary>
    public bool KeepSignedIn { get; set; }
}

/// <summary>
/// 패스키(지문·얼굴) 로그인 폼에 담기는 값.
/// </summary>
/// <remarks>
/// <para>
/// <b>아이디·비밀번호 폼과 갈라 둔다.</b> 한 폼에 넣으면 그 폼의
/// <c>[Required]</c> 두 개가 패스키로 들어올 때도 걸려서, 아이디를 치지 않고
/// 들어오는 길이 막힌다. 검증을 조건부로 만드는 방법도 있지만 그쪽은
/// <b>비밀번호 로그인의 검증까지 건드리는 일</b>이라 위험 대비 이득이 없다.
/// </para>
///
/// <para>
/// 정적 SSR 화면이라 폼 둘을 이름으로 가른다(<c>FormName</c>).
/// </para>
/// </remarks>
public sealed class PasskeyLoginInput
{
    /// <summary>
    /// 기기가 만든 서명 묶음(JSON). <c>passkey.js</c> 가 채우고 폼을 제출한다.
    /// 사람이 손으로 채울 수 있는 값이 아니라 감춰 둔다.
    /// </summary>
    public string Assertion { get; set; } = string.Empty;

    /// <summary>
    /// 로그인 유지. 옆 폼의 체크 상태를 <c>passkey.js</c> 가 옮겨 담는다 —
    /// 사용자가 보는 체크는 하나여야 하기 때문이다.
    /// </summary>
    public bool KeepSignedIn { get; set; }
}
