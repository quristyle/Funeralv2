namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 카드가 어느 AI 에게 간 건인지 말하는 한 글자 배지(반 폭 카드라 이름 대신 머리글자). claude 와 copilot 은 같은 C 라 색으로 가른다.
/// 색은 projmng.css 의 .pm-ai--* 가 쥔다. 모르는 값도 배지는 뜬다.
/// </summary>
internal static class AiRunnerMark
{
    /// <summary>배지에 적을 한 글자.</summary>
    public static string Letter(string? kind) => Key(kind) switch
    {
        "claude" => "C",
        "antigravity" => "A",
        "copilot" => "C",

        // 모르는 값은 첫 자를 대문자로 쓴다. 그마저 없으면 ? 로 AI 를 모른다는 사실을 보인다.
        var other => other.Length > 0 ? char.ToUpperInvariant(other[0]).ToString() : "?",
    };

    /// <summary>배지 색 수식어(pm-ai--*). 모르는 값은 흐림으로 간다.</summary>
    public static string Tone(string? kind) => Key(kind) switch
    {
        "claude" or "antigravity" or "copilot" => Key(kind),
        _ => "etc",
    };

    /// <summary>견주기 좋게 다듬은 값. 공통코드의 코드값이 그대로 온다.</summary>
    private static string Key(string? kind) => (kind ?? string.Empty).Trim().ToLowerInvariant();
}
