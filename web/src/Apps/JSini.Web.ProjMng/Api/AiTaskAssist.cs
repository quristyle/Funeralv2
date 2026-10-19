using System.Text;
using System.Text.RegularExpressions;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>지어 온 제목 후보와, AI 이름(Model)은 공급자가 안 보내 줄 수 있어 null 일 수 있다.</summary>
public sealed record AiTaskTitles(IReadOnlyList<string> Titles, string? Model);

/// <summary>간추린 조각(마크다운)과 그것을 만든 AI.</summary>
public sealed record AiTaskSummary(string? Markdown, string? Model);

/// <summary>AI 작업 지시문을 쓰는 사람을 돕는 두 가지 — 제목 짓기와 요약 얹기. 헬프데스크의 같은 기능을 옮겼고(둘이 쓰면 복제), 본문은 마크다운이라 요약은 인용 블록으로 얹는다. AI 는 고르지 않는다 — 서버가 공급자를 정한다. 위쪽 「AI」 고르개(실행기)와 달리 여기는 포털의 AI 쳇 길이라 실행기 한도를 쓰지 않는다.</summary>
public sealed class AiTaskAssist(AiChatClient ai)
{
    /// <summary>AI 에게 넘기는 본문의 상한(글자). 넘기기 전에 자른다 — 넘기면 429·502 로 돌아오고, 무엇을 시키는지는 앞머리에 있다.</summary>
    private const int MaxChars = 6000;

    /// <summary>받아 쓸 제목 후보의 최소 개수.</summary>
    public const int MinTitles = 3;

    /// <summary>앞에 붙은 번호·글머리 기호·따옴표. 모델이 시키지 않아도 붙인다.</summary>
    private static readonly Regex Bullet = new(
        """^\s*(?:[-*•]|\d+\s*[.)]|제?\s*\d+\s*[.)])\s*""",
        RegexOptions.Compiled);

    /// <summary>지시문을 보고 제목 후보를 짓는다. 못 지으면 빈 목록이다 — 부르는 쪽이 까닭을 말해야 한다.</summary>
    public async Task<AiTaskTitles> SuggestTitlesAsync(
        string? contents, CancellationToken cancellationToken = default)
    {
        var text = Trim(contents);

        if (text.Length == 0)
        {
            return new AiTaskTitles([], null);
        }

        // 개수를 넉넉히 부른다. 겹치거나 빼먹는 줄을 걸러 낸 뒤에도 최소 개수가 남아야 한다.
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

    /// <summary>지시문을 간추린다. 돌려주는 것은 마크다운 조각이라 본문 맨 위에 그대로 꽂는다. 못 간추리면 null.</summary>
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

    /// <summary>요약 한 덩이의 모양은 인용 블록이다. 본문 맨 위에 꽂혀 그대로 실행기에 가므로 요약과 지시의 경계가 글 안에서 읽혀야 한다. 제목(#)으로 짓지 않는다 — 제목 칸이 비면 본문 첫 줄이 제목이 된다. 끝에 빈 줄을 둔다 — 없으면 다음 문단까지 블록 안으로 빨려 든다.</summary>
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

    /// <summary>한 번 묻고 답을 한 덩이로 받는다. 어느 AI 가 답했는지는 물어서 아는 것이 아니라 스트림의 kind: "used" 표시로 흘러온다.</summary>
    private async Task<(string? Text, string? Model)> AskAsync(
        string prompt, CancellationToken cancellationToken)
    {
        var answer = new StringBuilder();
        string? model = null;

        // 대화로 담지 않는다(sessionId 없음). 사람이 연 적 없는 주제로 AI쳇 목록이 덮이지 않게.
        await foreach (var part in ai.StreamAsync(
            [new AiChatMessage("user", prompt)], sessionId: null,
            cancellationToken: cancellationToken))
        {
            if (part.IsUsedMarker && part.Notice is { Length: > 0 } used)
            {
                model = used;
                continue;
            }

            // 알림은 글에 섞지 않는다 — 답으로 세면 「다른 공급자로 넘깁니다」가 제목 후보 한 줄이 된다.
            if (part.Notice is not null)
            {
                continue;
            }

            answer.Append(part.Text);
        }

        var text = answer.ToString().Trim();

        return (text.Length == 0 ? null : text, model);
    }

    /// <summary>지시문을 상한까지 자른다. 마크다운 표식은 걷지 않는다 — 머리글과 목록을 지우면 모델이 항목과 설명을 잃는다.</summary>
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
