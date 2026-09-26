using Microsoft.AspNetCore.Components;
using System.Text;

namespace JSini.Web.LifeEnv.Components.Shared;

public partial class WarningSection
{
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>본문. 비어 있으면 이 토막을 아예 그리지 않는다.</summary>
    [Parameter] public string? Body { get; set; }

    /// <summary>칠할 구역명. 길이 내림차순으로 들어온다.</summary>
    [Parameter] public IReadOnlyList<string> Keywords { get; set; } = [];

    private MarkupString Highlighted => Mark(Body ?? string.Empty, Keywords);

    private static MarkupString Mark(string body, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return new MarkupString(Escape(body));
        }

        // 이미 칠한 자리. 겹쳐 칠하면 태그가 태그 안에 들어가 깨진다.
        var taken = new bool[body.Length];

        foreach (var keyword in keywords)
        {
            var from = 0;

            while (from < body.Length)
            {
                var at = body.IndexOf(keyword, from, StringComparison.Ordinal);
                if (at < 0) break;

                var free = true;
                for (var i = at; i < at + keyword.Length; i++)
                {
                    if (taken[i]) { free = false; break; }
                }

                if (free)
                {
                    for (var i = at; i < at + keyword.Length; i++)
                    {
                        taken[i] = true;
                    }
                }

                from = at + keyword.Length;
            }
        }

        var builder = new StringBuilder(body.Length + 64);
        var inMark = false;

        for (var i = 0; i < body.Length; i++)
        {
            if (taken[i] && !inMark)
            {
                builder.Append("<mark class=\"le-warn__mark\">");
                inMark = true;
            }
            else if (!taken[i] && inMark)
            {
                builder.Append("</mark>");
                inMark = false;
            }

            builder.Append(Escape(body[i]));
        }

        if (inMark)
        {
            builder.Append("</mark>");
        }

        return new MarkupString(builder.ToString());
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Escape(char ch) => ch switch
    {
        '&' => "&amp;",
        '<' => "&lt;",
        '>' => "&gt;",
        '\n' => "<br />",
        '\r' => string.Empty,
        _ => ch.ToString(),
    };
}
