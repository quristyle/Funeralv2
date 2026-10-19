using System.ComponentModel.DataAnnotations;

namespace JSini.Web.Shell.Components.Pages;

/// <summary>로그인 폼에 담기는 값.</summary>
public sealed class LoginInput
{
    [Required(ErrorMessage = "아이디를 입력하세요.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "비밀번호를 입력하세요.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>브라우저를 닫아도 로그인을 유지할 것인가(30일). 「아이디 기억하기」와 달리 서버가 굽는 인증 쿠키의 수명을 정하므로 폼에 실려 서버로 간다. 홈 화면 앱으로 쓰는 사람에게 뜻이 있다.</summary>
    public bool KeepSignedIn { get; set; }
}

/// <summary>패스키(지문·얼굴) 로그인 폼에 담기는 값. 아이디·비밀번호 폼과 갈라 둔다. 한 폼에 넣으면 그 폼의 [Required] 가 패스키로 들어올 때도 걸려 아이디를 치지 않고 들어오는 길이 막힌다. 정적 SSR 이라 폼 둘을 FormName 으로 가른다.</summary>
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
