using System.Text;
using System.Text.RegularExpressions;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>요청 글을 돕는 두 가지 — 제목 짓기와 요약 얹기. 고르는 칸은 없다: 포털 AI 쳇(AiChatClient)과 같은 끝점을 쓰고 서버가 공급자를 정한다.
/// 두 가지 다 모여야 쓸 수 있는 답이라 스트림을 끝까지 받아 한 덩이로 잇는다.</summary>
/// <summary>지어 온 제목 후보와, 그것을 만든 AI(Model — 없을 수 있다).</summary>
public sealed record AiTitles(IReadOnlyList<string> Titles, string? Model);

/// <summary>간추린 조각(HTML)과 그것을 만든 AI.</summary>
public sealed record AiSummary(string? Html, string? Model);

public sealed class RequestAi(AiChatClient ai)
{
    /// <summary>AI 에게 넘기는 본문의 상한(글자). 넘기기 전에 자른다 — 캡처가 든 본문을 그대로 보내면 토큰 한도를 넘어 429·502 가 된다.</summary>
    private const int MaxChars = 4000;

    /// <summary>받아 쓸 제목 후보의 최소 개수.</summary>
    public const int MinTitles = 3;

    /// <summary>태그와 실체 참조. 본문을 AI 에게 글자로 넘기려고 걷어낸다.</summary>
    private static readonly Regex TagOrEntity = new(
        """<[^>]*>|&[a-z]+;|&#\d+;""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>앞에 붙은 번호·글머리 기호·따옴표. 모델이 시키지 않아도 붙인다.</summary>
    private static readonly Regex Bullet = new(
        """^\s*(?:[-*•]|\d+\s*[.)]|제?\s*\d+\s*[.)])\s*""",
        RegexOptions.Compiled);

    /// <summary>본문을 보고 제목 후보를 짓는다. 못 지으면 빈 목록이라 부르는 쪽이 까닭을 말해야 한다.</summary>
    public async Task<AiTitles> SuggestTitlesAsync(
        string? contentHtml, CancellationToken cancellationToken = default)
    {
        var text = ToPlainText(contentHtml);

        if (text.Length == 0)
        {
            return new AiTitles([], null);
        }

        // 개수를 넉넉히 부른다 — 중복·누락을 걸러 낸 뒤에도 최소 개수가 남아야 한다.
        var (answer, model) = await AskAsync(
            $"""
             너는 사내 헬프데스크에 올라온 요청 글에 제목을 다는 사람이다.
             아래 본문을 읽고 제목 후보를 다섯 개 지어라.

             규칙:
             - 한국어로 쓴다.
             - 한 줄에 하나씩, 다섯 줄만 쓴다.
             - 번호·글머리 기호·따옴표를 붙이지 않는다.
             - 한 줄은 마흔 글자를 넘기지 않는다.
             - 무엇이 어디서 어떻게 됐는지가 드러나게 쓴다.
             - 설명이나 인사말을 덧붙이지 않는다. 제목 다섯 줄만 쓴다.

             본문:
             {text}
             """,
            cancellationToken);

        if (answer is null)
        {
            return new AiTitles([], model);
        }

        var titles = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Bullet.Replace(line, string.Empty).Trim().Trim('"', '\'', '「', '」'))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();

        return new AiTitles(titles, model);
    }

    /// <summary>본문을 간추린다. 돌려주는 것은 HTML 조각이라 본문 맨 위에 그대로 꽂는다. 못 간추리면 null.</summary>
    public async Task<AiSummary> SummarizeAsync(
        string? contentHtml, CancellationToken cancellationToken = default)
    {
        var text = ToPlainText(contentHtml);

        if (text.Length == 0)
        {
            return new AiSummary(null, null);
        }

        var (answer, model) = await AskAsync(
            $"""
             너는 사내 헬프데스크에 올라온 요청 글을 읽고 세 줄로 간추리는 사람이다.
             아래 본문을 읽고 요약을 써라.

             규칙:
             - 한국어로 쓴다.
             - 한 줄에 하나씩, 세 줄에서 다섯 줄 사이로 쓴다.
             - 번호·글머리 기호를 붙이지 않는다. 줄바꿈으로만 가른다.
             - 본문에 없는 말을 지어내지 않는다.
             - 설명이나 인사말을 덧붙이지 않는다. 요약 줄만 쓴다.

             본문:
             {text}
             """,
            cancellationToken);

        if (answer is null)
        {
            return new AiSummary(null, model);
        }

        var lines = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Bullet.Replace(line, string.Empty).Trim())
            .Where(line => line.Length > 0)
            .Take(5)
            .ToList();

        return new AiSummary(lines.Count == 0 ? null : ToHtml(lines), model);
    }

    /// <summary>요약 한 덩이의 모양 — 테두리를 두른 상자. blockquote 로 짓는다: 편집기가 다시 읽을 때 모르는 모양은 통째로 버리고, 살아남는 블록은 blockquote 하나뿐이다.
    /// 서식은 글 안에 인라인으로 박는다 — 저장된 글은 우리 CSS 없이 다시 그려지므로 클래스에 모양을 맡기면 상자가 사라진다. 바탕색과 글자색은 칠하지 않는다(밝은·어두운 화면 양쪽).</summary>
    private static string ToHtml(IReadOnlyList<string> lines)
    {
        const string edge = "#94a3b8";

        // 모든 줄이 함께 쓰는 것 — 좌우 테두리와 안쪽 여백.
        var side = $"margin:0;border-left:1px solid {edge};border-right:1px solid {edge};padding:2px 14px;";

        var html = new StringBuilder();

        html.Append("<blockquote class=\"hd-summary\" style=\"").Append(side)
            .Append($"border-top:1px solid {edge};border-radius:6px 6px 0 0;padding-top:10px;")
            .Append("\"><strong>요약</strong></blockquote>");

        for (var i = 0; i < lines.Count; i++)
        {
            var last = i == lines.Count - 1;

            html.Append("<blockquote class=\"hd-summary\" style=\"").Append(side);

            if (last)
            {
                html.Append($"border-bottom:1px solid {edge};border-radius:0 0 6px 6px;padding-bottom:10px;");
            }

            html.Append("\">· ").Append(Escape(lines[i])).Append("</blockquote>");
        }

        // 상자와 본문 사이의 빈 줄 — 없으면 상자 바로 아래 첫 문단이 한 덩이로 읽힌다.
        html.Append("<p><br></p>");

        return html.ToString();
    }

    /// <summary>AI 가 내놓은 글자를 HTML 에 넣기 전에 막는다 — 모델이 쓴 글이라 믿을 것이 없다. 그대로 꽂으면 태그가 살아난다.</summary>
    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>한 번 묻고 답을 한 덩이로 받는다. 어느 AI 가 답했는지는 보내기 전에는 모른다 — 스트림의 used 표시가 실제로 답한 AI 다.</summary>
    private async Task<(string? Text, string? Model)> AskAsync(
        string prompt, CancellationToken cancellationToken)
    {
        var answer = new StringBuilder();
        string? model = null;

        // 대화로 담지 않는다(sessionId 없음) — 한 번짜리 부탁이라 담으면 AI쳇 목록이 사람이 연 적 없는 주제로 덮인다.
        await foreach (var part in ai.StreamAsync(
            [new AiChatMessage("user", prompt)], sessionId: null,
            cancellationToken: cancellationToken))
        {
            if (part.IsUsedMarker && part.Notice is { Length: > 0 } used)
            {
                model = used;
                continue;
            }

            // 알림은 글에 섞지 않는다 — 「다른 공급자로 넘깁니다」 같은 말을 답으로 세면 제목 후보 한 줄이 된다.
            if (part.Notice is not null)
            {
                continue;
            }

            answer.Append(part.Text);
        }

        var text = answer.ToString().Trim();

        return (text.Length == 0 ? null : text, model);
    }

    /// <summary>본문 HTML 에서 글자만 뽑아 상한까지 자른다.</summary>
    private static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = TagOrEntity.Replace(html, " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text.Length > MaxChars ? text[..MaxChars] : text;
    }
}
