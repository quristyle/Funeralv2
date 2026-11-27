namespace JSini.Web.Shell.Security;

/// <summary>
/// 로그인 뒤 돌아갈 주소를 이 사이트 안의 경로로만 추린다(오픈 리다이렉트 방지).
/// 「/ 로 시작하고 // 가 아니다」만 보면 <c>/\evil.com</c>·<c>/%09/evil.com</c> 이 지나간다 — 브라우저가 역슬래시를 / 로,
/// 탭·줄바꿈을 지워 읽어 <c>//evil.com</c> 이 된다. 그래서 역슬래시와 제어·공백 문자를 통째로 거절한다.
/// </summary>
public static class LocalReturnUrl
{
    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)
            || returnUrl[0] != '/'
            || (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')))
        {
            return "/";
        }

        foreach (var c in returnUrl)
        {
            if (c == '\\' || char.IsControl(c) || char.IsWhiteSpace(c))
            {
                return "/";
            }
        }

        return returnUrl;
    }
}
