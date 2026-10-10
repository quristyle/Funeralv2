using System.Text;
using System.Text.RegularExpressions;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 지어 온 제목 후보와, <b>그것을 만든 AI</b>.
/// </summary>
/// <param name="Titles">제목 후보. 못 지었으면 빈 목록.</param>
/// <param name="Model">
/// 이번 답을 만든 AI 의 이름. 모르면 <c>null</c> — 공급자가 그 표시를
/// 안 보내 줄 수도 있어서 <b>없을 수 있는 값</b>이다.
/// </param>
public sealed record AiTaskTitles(IReadOnlyList<string> Titles, string? Model);

/// <summary>간추린 조각(마크다운)과 그것을 만든 AI.</summary>
public sealed record AiTaskSummary(string? Markdown, string? Model);

/// <summary>
/// AI 작업 지시문을 쓰는 사람을 돕는 두 가지 — <b>제목 짓기</b>와 <b>요약 얹기</b>.
/// </summary>
/// <remarks>
/// <para>
/// [헬프데스크의 같은 기능을 옮겨 온 것이다]
/// </para>
/// <para>
/// 요청 등록 화면(<c>/helpdesk/request/new</c>)이 먼저 하던 일이다
/// (<c>JSini.Web.HelpDesk.Api.RequestAi</c>). 업무 모듈끼리는 서로를 참조할
/// 수 없고(web/CLAUDE.md 의 의존 규칙 2), 저장소의 규칙은 <b>둘이 쓰면 복제,
/// 세 번째부터 승격</b>이다 — 지금이 둘째다.
/// </para>
/// <para>
/// <b>복제이되 같은 글이 아니다.</b> 그쪽 본문은 편집기가 내놓는 HTML 이라
/// 태그를 걷어내고 돌려줄 때도 <c>&lt;blockquote&gt;</c> 로 짓는다(Quill 이
/// 제가 모르는 모양을 통째로 버려서 그 태그여야 했다). 이 화면의 본문은
/// <b>마크다운 글자</b>다(<c>CodeEditor</c> · <c>ai_task.content_format</c>).
/// 그래서 들어오는 것도 나가는 것도 글자이고, 요약은 인용 블록으로 얹는다.
/// </para>
/// <para>
/// [AI 를 고르지 않는다]
/// </para>
/// <para>
/// 포털의 AI 쳇(<c>AiChatPanel</c>)과 같다 — 보내는 것은 물음뿐이고 어느
/// 공급자로 갈지는 서버가 정한다(<c>AI:DefaultProvider</c>). 그쪽이 답을 못
/// 하면 서버가 스스로 다음 공급자로 넘긴다.
/// </para>
/// <para>
/// <b>화면 위쪽의 「AI」 고르개와 헷갈리면 안 된다.</b> 그 칸은 <b>이 지시를
/// 실제로 돌릴 CLI</b>(<c>claude</c> · <c>antigravity</c> · <c>copilot</c>)를
/// 고르는 자리이고, 여기서 제목을 지어 주는 것은 <b>포털의 AI 쳇</b>이다 —
/// 둘은 서로 다른 길로 간다. 그래서 제목을 짓는 데 실행기의 한도를 쓰지
/// 않는다.
/// </para>
/// <para>
/// [흘려 받는 길로 묻고 한 덩이로 받는다]
/// </para>
/// <para>
/// <see cref="AiChatClient"/> 가 내주는 것은 <c>StreamAsync</c> 하나뿐이다.
/// 여기 두 가지는 <b>다 모여야 쓸 수 있는 답</b>이라(제목은 골라야 하고
/// 요약은 통째로 꽂는다) 흘려 보여 줄 데가 없다. 그래서 끝까지 받아 글자
/// 하나로 잇는다.
/// </para>
/// </remarks>
public sealed class AiTaskAssist(AiChatClient ai)
{
    /// <summary>
    /// AI 에게 넘기는 본문의 상한(글자). <b>넘기기 전에 자른다.</b>
    /// </summary>
    /// <remarks>
    /// 이 화면의 지시문은 <b>헬프데스크 요청 글보다 훨씬 길다</b> — 파일
    /// 경로와 코드 조각이 통째로 들어오는 일이 흔하다. 그대로 보내면 토큰
    /// 한도를 넘기고, 넘긴 쪽은 429·502 로 돌아온다. 무엇을 시키는 일인지는
    /// 대개 앞머리에 적혀 있으므로 앞에서 자른다.
    /// </remarks>
    private const int MaxChars = 6000;

    /// <summary>받아 쓸 제목 후보의 최소 개수.</summary>
    public const int MinTitles = 3;

    /// <summary>앞에 붙은 번호·글머리 기호·따옴표. 모델이 시키지 않아도 붙인다.</summary>
    private static readonly Regex Bullet = new(
        """^\s*(?:[-*•]|\d+\s*[.)]|제?\s*\d+\s*[.)])\s*""",
        RegexOptions.Compiled);

    /// <summary>
    /// 지시문을 보고 제목 후보를 짓는다. 못 지으면 빈 목록 —
    /// <b>부르는 쪽이 까닭을 말해야 한다.</b>
    /// </summary>
    public async Task<AiTaskTitles> SuggestTitlesAsync(
        string? contents, CancellationToken cancellationToken = default)
    {
        var text = Trim(contents);

        if (text.Length == 0)
        {
            return new AiTaskTitles([], null);
        }

        // 개수를 넉넉히 부른다. 모델이 겹치는 것을 내놓거나 한두 줄을 빼먹어도
        // 걸러 낸 뒤에 최소 개수가 남아야 한다.
        var (answer, model) = await AskAsync(
            $"""
             너는 개발팀이 AI 에게 맡기는 작업 지시문에 제목을 다는 사람이다.
             아래 지시문을 읽고 제목 후보를 다섯 개 지어라.

             규칙:
             - 한국어로 쓴다.
             - 한 줄에 하나씩, 다섯 줄만 쓴다.
             - 번호·글머리 기호·따옴표를 붙이지 않는다.
             - 한 줄은 마흔 글자를 넘기지 않는다.
             - 무엇을 어디에서 어떻게 고치는 일인지가 드러나게 쓴다.
             - 설명이나 인사말을 덧붙이지 않는다. 제목 다섯 줄만 쓴다.

             지시문:
             {text}
             """,
            cancellationToken);

        if (answer is null)
        {
            return new AiTaskTitles([], model);
        }

        var titles = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Bullet.Replace(line, string.Empty).Trim().Trim('"', '\'', '「', '」'))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();

        return new AiTaskTitles(titles, model);
    }

    /// <summary>
    /// 지시문을 간추린다. 돌려주는 것은 <b>마크다운 조각</b>이라 그대로 본문
    /// 맨 위에 꽂을 수 있다. 못 간추리면 <c>null</c>.
    /// </summary>
    public async Task<AiTaskSummary> SummarizeAsync(
        string? contents, CancellationToken cancellationToken = default)
    {
        var text = Trim(contents);

        if (text.Length == 0)
        {
            return new AiTaskSummary(null, null);
        }

        var (answer, model) = await AskAsync(
            $"""
             너는 개발팀이 AI 에게 맡기는 작업 지시문을 읽고 간추리는 사람이다.
             아래 지시문을 읽고 요약을 써라.

             규칙:
             - 한국어로 쓴다.
             - 한 줄에 하나씩, 세 줄에서 다섯 줄 사이로 쓴다.
             - 번호·글머리 기호를 붙이지 않는다. 줄바꿈으로만 가른다.
             - 지시문에 없는 말을 지어내지 않는다.
             - 무엇을 해야 하는 일인지를 앞에 둔다.
             - 설명이나 인사말을 덧붙이지 않는다. 요약 줄만 쓴다.

             지시문:
             {text}
             """,
            cancellationToken);

        if (answer is null)
        {
            return new AiTaskSummary(null, model);
        }

        var lines = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Bullet.Replace(line, string.Empty).Trim())
            .Where(line => line.Length > 0)
            .Take(5)
            .ToList();

        return new AiTaskSummary(lines.Count == 0 ? null : ToMarkdown(lines), model);
    }

    /// <summary>
    /// 요약 한 덩이의 모양 — <b>인용 블록</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 글자는 본문 맨 위에 꽂혀 <b>그대로 실행기에게 간다.</b> 그래서
    /// 「여기부터 여기까지가 요약이고 지시가 아니다」가 글 안에서 읽혀야 한다 —
    /// 인용 블록(<c>&gt;</c>)이 사람에게도 모델에게도 그 뜻으로 읽히는
    /// 유일한 마크다운 표식이다.
    /// </para>
    /// <para>
    /// <b>제목(<c>#</c>)으로 짓지 않는다.</b> 저장할 때 제목 칸이 비어 있으면
    /// 서버가 <b>본문 첫 줄</b>로 제목을 만드는데(<c>AiTaskService</c>), 요약
    /// 머리글이 첫 줄이 되면 모든 건의 제목이 「요약」이 된다.
    /// </para>
    /// <para>
    /// 끝에 빈 줄을 둔다. 없으면 인용 블록 바로 아래 문단이 블록 안으로
    /// 빨려 들어가(마크다운의 lazy continuation) <b>원래 지시문 첫 문단까지
    /// 요약처럼 보인다.</b>
    /// </para>
    /// </remarks>
    private static string ToMarkdown(IReadOnlyList<string> lines)
    {
        var md = new StringBuilder();

        md.Append("> **요약**\n>\n");

        foreach (var line in lines)
        {
            md.Append("> - ").Append(line).Append('\n');
        }

        md.Append('\n');

        return md.ToString();
    }

    /// <summary>
    /// 한 번 묻고 답을 한 덩이로 받는다. 답과 함께 <b>그것을 만든 AI</b> 를 준다.
    /// </summary>
    /// <remarks>
    /// <b>어느 AI 가 답했는지는 물어서 아는 것이 아니라 흘러온다.</b> 서버가
    /// 공급자를 고르고 실패하면 스스로 다음 것으로 넘기므로, 보내기 전에는
    /// 아무도 모른다. 스트림 안에 <c>kind: "used"</c> 표시 하나가 섞여 오고
    /// 그것이 이번에 실제로 답한 AI 다.
    /// </remarks>
    private async Task<(string? Text, string? Model)> AskAsync(
        string prompt, CancellationToken cancellationToken)
    {
        var answer = new StringBuilder();
        string? model = null;

        // 대화로 담지 않는다(`sessionId` 없음) — 이것은 사람의 대화가 아니라
        // 지시문을 다듬어 달라는 한 번짜리 부탁이다. 담으면 AI쳇 목록이
        // 사람이 연 적 없는 주제로 덮인다.
        await foreach (var part in ai.StreamAsync(
            [new AiChatMessage("user", prompt)], sessionId: null, cancellationToken))
        {
            if (part.IsUsedMarker && part.Notice is { Length: > 0 } used)
            {
                model = used;
                continue;
            }

            // **알림은 글에 섞지 않는다.** 「다른 공급자로 넘깁니다」 같은
            // 말이고, 답으로 세면 그것이 제목 후보 한 줄이 된다.
            if (part.Notice is not null)
            {
                continue;
            }

            answer.Append(part.Text);
        }

        var text = answer.ToString().Trim();

        return (text.Length == 0 ? null : text, model);
    }

    /// <summary>
    /// 지시문을 상한까지 자른다.
    /// </summary>
    /// <remarks>
    /// <b>마크다운 표식을 걷어내지 않는다.</b> 헬프데스크 쪽은 HTML 을 받아
    /// 태그를 지워야 했지만(<c>RequestAi.ToPlainText</c>), 여기 들어오는 것은
    /// 사람이 적은 마크다운이고 그 표식 자체가 글의 짜임이다 — 머리글과 목록을
    /// 지우고 나면 모델이 <b>무엇이 항목이고 무엇이 설명인지</b>를 잃는다.
    /// </remarks>
    private static string Trim(string? contents)
    {
        if (string.IsNullOrWhiteSpace(contents))
        {
            return string.Empty;
        }

        var text = contents.Trim();

        return text.Length > MaxChars ? text[..MaxChars] : text;
    }
}
