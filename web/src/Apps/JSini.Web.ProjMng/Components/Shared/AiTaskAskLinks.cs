using System.Text.RegularExpressions;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>지시문 한 토막. <see cref="Href"/> 가 있으면 그 글자가 곧 화면 주소다.</summary>
public sealed record AiTaskAskPiece(string Text, string? Href = null);

/// <summary>
/// 지시문에 적힌 <b>상대 경로</b>(<c>/helpdesk/monitor/maintenance</c>)를 골라내
/// 화면으로 가는 링크로 가른다. 글자는 적힌 그대로 둔다.
/// </summary>
/// <remarks>
/// <b>실려 있는 화면의 주소만 링크가 된다</b>(<c>isScreen</c>). 지시문에는
/// <c>/srv/jsini/.env</c> · <c>/api/…</c> 같은 파일·API 경로도 흔해서, 모양만 보고
/// 걸면 눌렀을 때 「준비 중」이나 빈 화면으로 간다.
/// </remarks>
public static partial class AiTaskAskLinks
{
    // 앞이 글자·슬래시·점·콜론이면 경로의 시작이 아니다 — `https://host/a` 의 `/a`,
    // `./a` · `~/a` · `a/b` 를 거른다. 한글은 경로 글자가 아니라 「…maintenance화면」도 끊긴다.
    [GeneratedRegex(@"(?<![\w/.:~\-])(?<path>/[A-Za-z0-9_\-]+(?:/[A-Za-z0-9_\-.]+)*)(?<query>\?[A-Za-z0-9_\-=&%.]*)?")]
    private static partial Regex PathPattern();

    public static IReadOnlyList<AiTaskAskPiece> Split(string? text, Func<string, bool> isScreen)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var pieces = new List<AiTaskAskPiece>();
        var at = 0;

        foreach (Match m in PathPattern().Matches(text))
        {
            // 문장 끝의 마침표는 경로가 아니다(「… /admin/system/menu.」).
            var path = m.Groups["path"].Value.TrimEnd('.');
            var query = m.Groups["query"].Success ? m.Groups["query"].Value.TrimEnd('.') : "";

            if (!isScreen(path.TrimEnd('/')))
            {
                continue;
            }

            var href = query.Length > 1 ? path + query : path;

            if (m.Index > at)
            {
                pieces.Add(new AiTaskAskPiece(text[at..m.Index]));
            }

            pieces.Add(new AiTaskAskPiece(href, href));
            at = m.Index + href.Length;
        }

        if (at < text.Length)
        {
            pieces.Add(new AiTaskAskPiece(text[at..]));
        }

        return pieces;
    }

    /// <summary>
    /// 주소가 <c>@page</c> 틀 하나와 맞는가. 매개변수 자리(<c>{id}</c> · <c>{id:int}</c>)는
    /// 아무 마디나 받는다 — 지시문에는 <c>/projmng/ai/task/89</c> 처럼 채운 주소가 적힌다.
    /// </summary>
    public static bool Matches(string path, IEnumerable<string> templates)
    {
        var parts = path.Trim('/').Split('/');

        foreach (var template in templates)
        {
            var slots = template.Trim('/').Split('/');

            if (slots.Length != parts.Length)
            {
                continue;
            }

            var ok = true;

            for (var i = 0; i < slots.Length && ok; i++)
            {
                var slot = slots[i];
                ok = slot.StartsWith('{')
                    ? !IsNumericSlot(slot) || long.TryParse(parts[i], out _)
                    : string.Equals(slot, parts[i], StringComparison.OrdinalIgnoreCase);
            }

            if (ok)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumericSlot(string slot) =>
        slot.Contains(":int", StringComparison.OrdinalIgnoreCase)
        || slot.Contains(":long", StringComparison.OrdinalIgnoreCase);
}
