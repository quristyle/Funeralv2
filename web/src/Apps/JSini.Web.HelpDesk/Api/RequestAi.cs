using System.Text;
using System.Text.RegularExpressions;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 요청 글을 쓰는 사람을 돕는 두 가지 — <b>제목 짓기</b>와 <b>요약 얹기</b>.
/// </summary>
/// <remarks>
/// <para>
/// [AI 를 고르지 않는다 — AI 쳇과 같은 방식이다]
/// </para>
/// <para>
/// 포털의 AI 쳇(<c>AiChatPanel</c>)에는 <b>모델을 고르는 칸이 없다.</b>
/// 보내는 것은 주고받은 말뿐이고(<c>new { messages }</c>), 어느 공급자로
/// 갈지는 서버가 정한다(<c>AI:DefaultProvider</c>, 기본 <c>jsini</c>).
/// 그쪽이 답을 못 하면 서버가 **스스로 다음 공급자로 넘긴다**. 그래서 쳇
/// 화면은 「무엇을 쓸까」를 묻지 않고 「무엇이 쓰였나」를 나중에 알려 준다
/// (스트림에 섞여 오는 <c>kind: "used"</c> 표시).
/// </para>
/// <para>
/// 여기서도 그대로 한다 — <see cref="AiChatClient"/> 를 그대로 쓴다.
/// 고르는 칸을 이 화면에만 따로 달면 <b>같은 포털 안에서 AI 가 두 갈래로
/// 갈린다</b>: 쳇은 서버 기본값으로, 요청 등록은 사람이 고른 것으로. 어느
/// 쪽이 답했는지를 설명할 자리가 두 곳이 되고, 공급자 설정을 바꿀 때
/// 고쳐야 할 자리도 둘이 된다.
/// </para>
/// <para>
/// [흘려 받는 길로 묻고 한 덩이로 받는다]
/// </para>
/// <para>
/// <c>AiChatClient</c> 가 내주는 것은 <c>StreamAsync</c> 하나뿐이다.
/// 여기 두 가지는 <b>다 모여야 쓸 수 있는 답</b>이라(제목은 골라야 하고
/// 요약은 통째로 꽂는다) 흘려 보여 줄 데가 없다. 그래서 끝까지 받아
/// 글자 하나로 잇는다. 한 번 더 감싸는 대신 쳇과 <b>같은 끝점</b>을
/// 쓰는 쪽을 골랐다 — 공급자 넘김·사용량 세기가 전부 그 길에 걸려 있다.
/// </para>
/// </remarks>
public sealed class RequestAi(AiChatClient ai)
{
    /// <summary>
    /// AI 에게 넘기는 본문의 상한(글자). <b>넘기기 전에 자른다.</b>
    /// 요청 본문에는 화면 캡처가 통째로 들어 있어 그대로 보내면 토큰 한도를
    /// 넘기고, 넘긴 쪽은 429·502 로 돌아온다. 증상은 대개 앞머리에 있다.
    /// </summary>
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

    /// <summary>
    /// 본문을 보고 제목 후보를 짓는다. 못 지으면 빈 목록 —
    /// <b>부르는 쪽이 까닭을 말해야 한다.</b>
    /// </summary>
    public async Task<IReadOnlyList<string>> SuggestTitlesAsync(
        string? contentHtml, CancellationToken cancellationToken = default)
    {
        var text = ToPlainText(contentHtml);

        if (text.Length == 0)
        {
            return [];
        }

        // 개수를 넉넉히 부른다. 모델이 겹치는 것을 내놓거나 한두 줄을 빼먹어도
        // 걸러 낸 뒤에 최소 개수가 남아야 한다.
        var answer = await AskAsync(
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
            return [];
        }

        return answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Bullet.Replace(line, string.Empty).Trim().Trim('"', '\'', '「', '」'))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// 본문을 간추린다. 돌려주는 것은 <b>HTML 조각</b>이라 그대로 본문
    /// 맨 위에 꽂을 수 있다. 못 간추리면 <c>null</c>.
    /// </summary>
    public async Task<string?> SummarizeAsync(
        string? contentHtml, CancellationToken cancellationToken = default)
    {
        var text = ToPlainText(contentHtml);

        if (text.Length == 0)
        {
            return null;
        }

        var answer = await AskAsync(
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
            return null;
        }

        var lines = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Bullet.Replace(line, string.Empty).Trim())
            .Where(line => line.Length > 0)
            .Take(5)
            .ToList();

        return lines.Count == 0 ? null : ToHtml(lines);
    }

    /// <summary>
    /// 요약 한 덩이의 모양.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>편집기가 지우지 않는 태그만 쓴다.</b> 이 조각은 본문 DOM 에 직접
    /// 꽂히고(<c>request-editor.js</c> 의 <c>prependHtml</c>), 바로 뒤에
    /// 편집기가 그 DOM 을 제 모형으로 다시 읽는다. 그때 <b>제가 아는 모양이
    /// 아닌 것을 통째로 버린다</b> — 안쪽이 Quill 이라 그렇다.
    /// </para>
    /// <para>
    /// 재어 본 값이다(DevExpress 26.1.5). 테두리와 바탕을 인라인 서식으로
    /// 두른 <c>&lt;div&gt;</c> 는 꽂은 직후 <b>흔적도 없이</b> 사라졌고
    /// (<c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c> 만 남는다), <c>&lt;ul&gt;&lt;li&gt;</c>
    /// 도 빈 문단으로 바뀌었다. 살아남은 것은
    /// <c>&lt;p&gt;</c> · <c>&lt;strong&gt;</c> 뿐이다.
    /// </para>
    /// <para>
    /// 그래서 글머리를 목록 태그가 아니라 <b>가운뎃점 글자</b>로 그린다.
    /// 모양은 수수하지만 <b>남는다</b> — 그리고 이 조각은 본문에 섞여 DB 에
    /// 저장된 뒤 상세·메일에서 우리 CSS 없이 다시 그려지므로, 거기서도
    /// 같은 모양으로 선다.
    /// </para>
    /// </remarks>
    private static string ToHtml(IReadOnlyList<string> lines)
    {
        var html = new StringBuilder();

        html.Append("<p><strong>요약</strong></p>");

        foreach (var line in lines)
        {
            html.Append("<p>· ").Append(Escape(line)).Append("</p>");
        }

        // 요약과 본문 사이의 빈 줄. 이것이 없으면 요약 마지막 줄과 원래
        // 첫 문단이 붙어 한 덩이로 읽힌다.
        html.Append("<p><br></p>");

        return html.ToString();
    }

    /// <summary>
    /// AI 가 내놓은 글자를 HTML 에 넣기 전에 막는다. <b>모델이 쓴 글이라
    /// 더 믿을 것이 없다</b> — 본문에 그대로 꽂으면 태그가 살아난다.
    /// </summary>
    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>한 번 묻고 답을 한 덩이로 받는다. 실패하면 <c>null</c>.</summary>
    private async Task<string?> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        var answer = new StringBuilder();

        await foreach (var part in ai.StreamAsync(
            [new AiChatMessage("user", prompt)], cancellationToken))
        {
            // **알림은 글에 섞지 않는다.** 「다른 공급자로 넘깁니다」 같은
            // 말이고, 답으로 세면 그것이 제목 후보 한 줄이 된다.
            if (part.Notice is not null)
            {
                continue;
            }

            answer.Append(part.Text);
        }

        var text = answer.ToString().Trim();

        return text.Length == 0 ? null : text;
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
