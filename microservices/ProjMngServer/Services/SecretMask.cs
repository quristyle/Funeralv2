using System.Text.RegularExpressions;

namespace ProjMngServer.Services;

/// <summary>
/// 로그와 결과문에서 비밀값을 가린다.
/// </summary>
/// <remarks>
/// <para>
/// 설계 9.6. AI 는 운영 서버에서 <b>권한을 다 열고</b> 돌고, 그 장비에는
/// 서비스 열 곳의 접속 정보가 평문으로 있다. 담장(systemd)이 그 폴더를
/// 막지만, <b>대상 폴더 안에 비밀값이 들어 있는 경우</b>는 담장이 못 막는다 —
/// 저장소 안의 <c>secrets.env</c> 나 누가 떨어뜨려 둔 설정 파일이 그렇다.
/// </para>
/// <para>
/// <b>가리는 자리는 적재 직전이다.</b> DB 에 들어간 뒤에 가리면 이미 늦다 —
/// 화면에도 메일에도 나가고, 지워도 로그 백업에 남는다.
/// </para>
/// <para>
/// <b>완벽하지 않다.</b> 패턴으로 잡는 일이라 모양이 다르면 지나간다.
/// 이것은 <i>마지막 그물</i>이지 울타리가 아니다 — 울타리는 담장이고,
/// 그 사실을 잊으면 마스킹을 믿고 담장을 느슨하게 하게 된다.
/// </para>
/// </remarks>
public static partial class SecretMask
{
    private const string Hidden = "***가림***";

    /// <summary>
    /// <c>이름=값</c> · <c>이름: 값</c> 꼴에서 <b>이름이 위험한 것</b>을 잡는다.
    /// </summary>
    /// <remarks>
    /// 값의 모양(길이·글자 구성)으로 잡으려 하면 멀쩡한 해시·경로·SHA 까지
    /// 가려져서 로그를 못 읽게 된다. <b>이름으로 잡는 편이 헛가림이 적다.</b>
    /// </remarks>
    [GeneratedRegex(
        // 칸 사이의 `_`·`-` 는 **여러 개일 수 있다** — 이 저장소의 환경변수가
        // `Jwt__Key` 처럼 이중 밑줄을 쓴다. `[_-]?` 로 두면 그게 안 잡힌다(실제로 밟음).
        @"(?i)\b([A-Za-z0-9_.\-]*(?:password|passwd|pwd|secret|token|api[_-]*key|apikey|access[_-]*key|private[_-]*key|jwt[_-]*key|signing[_-]*key|vapid)[A-Za-z0-9_.\-]*)(\s*[=:]\s*)(""?)([^\s""',;]{4,})",
        RegexOptions.CultureInvariant)]
    private static partial Regex NamedSecret();

    /// <summary>
    /// 접속 문자열의 비밀번호 칸. <c>Host=…;Password=…;</c> 모양이다.
    /// </summary>
    [GeneratedRegex(@"(?i)(Password\s*=\s*)([^;\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ConnPassword();

    /// <summary>
    /// 주소 안에 박힌 자격 — <c>https://사용자:비밀번호@호스트</c>.
    /// </summary>
    /// <remarks>
    /// <c>git remote -v</c> 나 <c>~/.git-credentials</c> 를 찍으면 이 모양으로 나온다.
    /// </remarks>
    [GeneratedRegex(@"(?i)\b([a-z][a-z0-9+.\-]*://)([^/\s:@]+):([^/\s@]+)@", RegexOptions.CultureInvariant)]
    private static partial Regex UrlCredential();

    /// <summary>
    /// 알려진 열쇠 덩어리 — PEM 본문 · GitHub 토큰.
    /// </summary>
    [GeneratedRegex(
        @"(-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----)|(\bgh[pousr]_[A-Za-z0-9]{16,}\b)",
        RegexOptions.CultureInvariant)]
    private static partial Regex KnownBlob();

    /// <summary>
    /// 가린다. 빈 값이면 그대로 돌려준다.
    /// </summary>
    public static string? Apply(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // 덩어리부터. 먼저 지우지 않으면 아래 규칙들이 그 안을 헤집는다.
        text = KnownBlob().Replace(text, Hidden);

        text = UrlCredential().Replace(text, m => $"{m.Groups[1].Value}{m.Groups[2].Value}:{Hidden}@");

        text = ConnPassword().Replace(text, m => m.Groups[1].Value + Hidden);

        // 이름과 구분자는 **원문 그대로** 되살린다. 공백까지 살려야 원래 줄이
        // 어떤 모양이었는지 읽힌다.
        text = NamedSecret().Replace(text, m =>
            $"{m.Groups[1].Value}{m.Groups[2].Value}{m.Groups[3].Value}{Hidden}");

        return text;
    }
}
