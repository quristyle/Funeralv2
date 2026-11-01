using System.Text.RegularExpressions;

namespace ProjMngServer.Services;

/// <summary>
/// 일정표(<c>dev_wbs</c>)의 휴가 줄을 가린다. 휴가는 따로 적는 칸이 없고 제목에만 있다 —
/// 「정일인 휴가」「김원욱 여름휴가」「순열월차」처럼. <b>담당 칸(<c>dev_user</c>)은 휴가 간 사람이
/// 아니라 적어 넣은 사람</b>이라(정일인의 휴가를 이순열이 적었다) 사람도 제목에서 뽑는다.
/// </summary>
public static partial class WbsLeave
{
    /// <summary>「휴가반납」은 휴가를 물리고 일한 날이라 휴가가 아니다.</summary>
    [GeneratedRegex("휴가\\s*반납")]
    private static partial Regex Returned();

    [GeneratedRegex("대체휴일|대체휴무|리프레시|경조사?|연차|월차|반차|병가|휴가|휴무")]
    private static partial Regex Word();

    [GeneratedRegex("\\[[^\\]]*\\]|\\([^)]*\\)")]
    private static partial Regex Bracket();

    private static readonly string[] Modifiers =
        ["여름", "겨울", "하계", "동계", "오전", "오후", "개인", "정기", "특별", "출산", "연말", "명절", "포상"];

    /// <summary>SQL 이 먼저 거르는 무늬. <see cref="Parse"/> 의 낱말과 같아야 한다.</summary>
    public const string SqlPattern = "대체휴일|대체휴무|리프레시|경조|연차|월차|반차|병가|휴가|휴무";

    public sealed record Leave(string Kind, string? Person);

    /// <summary>휴가 제목이면 종류와 사람을 돌려주고, 아니면 null. 사람을 못 뽑으면 Person 이 null 이다.</summary>
    public static Leave? Parse(string? title)
    {
        if (string.IsNullOrWhiteSpace(title) || Returned().IsMatch(title)) return null;

        var text = Bracket().Replace(title, " ").Trim();
        var m = Word().Match(text);
        if (!m.Success) return null;

        var person = LastName(text[..m.Index]) ?? FirstName(text[(m.Index + m.Length)..]);
        return new Leave(m.Value, person);
    }

    /// <summary>
    /// 물은 이름이 이 휴가의 사람인가. 제목에는 성을 떼고 적기도 해서(「순열월차」) 이름이 그
    /// 글자로 끝나도 같은 사람으로 본다 — 두 글자 이상일 때만.
    /// </summary>
    public static bool IsFor(Leave leave, string? title, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var n = name.Trim();

        if (leave.Person is { } p
            && (p.Equals(n, StringComparison.OrdinalIgnoreCase) || (p.Length >= 2 && n.EndsWith(p, StringComparison.Ordinal))))
            return true;

        return title?.Contains(n, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? LastName(string before)
    {
        var tokens = Split(before);
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var t = StripModifier(tokens[i]);
            if (t.Length > 0) return t;
        }
        return null;
    }

    private static string? FirstName(string after)
    {
        foreach (var token in Split(after))
        {
            var t = StripModifier(token);
            if (t.Length > 0) return t;
        }
        return null;
    }

    private static List<string> Split(string s) =>
        [.. s.Split([' ', '\t', '-', '/', ',', ':', '·', '_'], StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>「여름」처럼 꾸밈말만인 토막은 빈 글자로, 이름 뒤에 붙은 꾸밈말(「김원욱여름」)은 떼어 낸다.</summary>
    private static string StripModifier(string token)
    {
        var t = token.Trim();
        foreach (var mod in Modifiers)
        {
            if (t == mod) return string.Empty;
            if (t.Length > mod.Length && t.EndsWith(mod, StringComparison.Ordinal)) t = t[..^mod.Length];
        }
        return t;
    }
}
