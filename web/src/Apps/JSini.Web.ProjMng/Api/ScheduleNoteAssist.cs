using System.Globalization;
using System.Text;
using System.Text.Json;
using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>본문에서 뽑아 낸 일정 한 건. 못 뽑은 칸은 null 이다.</summary>
public sealed record ScheduleDraft(
    DateOnly? Start, DateOnly? End, string? Title, string? Summary, string? Model);

/// <summary>일정 메모 화면의 본문을 읽고 날짜·제목·요약을 짓는다. 묻는 길은 AI 쳇과 같은 AiChatClient 다(AiTaskAssist 와 같다).</summary>
public sealed class ScheduleNoteAssist(AiChatClient ai)
{
    /// <summary>AI 에게 넘기는 본문의 상한(글자). 넘기면 429·502 로 돌아온다.</summary>
    private const int MaxChars = 6000;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d", "yyyy.MM.dd", "yyyy/MM/dd"];

    /// <summary>본문을 읽어 일정 칸을 짓는다. 답을 못 읽으면 칸이 전부 null 인 것을 돌려준다.</summary>
    public async Task<ScheduleDraft> DraftAsync(string? contents, CancellationToken cancellationToken = default)
    {
        var text = contents?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return new ScheduleDraft(null, null, null, null, null);
        }

        if (text.Length > MaxChars)
        {
            text = text[..MaxChars];
        }

        // 「내일」「다음 주 화요일」을 풀려면 오늘이 있어야 한다 — 모델의 시계는 믿을 수 없다.
        var today = AppTime.Today;

        var (answer, model) = await AskAsync(
            $$"""
              너는 업무 메모를 읽고 일정표에 올릴 한 건으로 정리하는 사람이다.
              오늘은 {{today:yyyy-MM-dd}} ({{today.ToString("dddd", CultureInfo.GetCultureInfo("ko-KR"))}}) 이다.

              아래 메모를 읽고 JSON 하나만 써라. 다른 말은 쓰지 않는다.
              {"start":"yyyy-MM-dd","end":"yyyy-MM-dd","title":"제목","summary":"요약"}

              규칙:
              - 한국어로 쓴다.
              - start 는 일정이 시작하는 날, end 는 끝나는 날이다. 하루짜리면 둘이 같다.
              - 「내일」「다음 주 금요일」처럼 상대적인 날은 오늘을 기준으로 실제 날짜로 바꾼다.
              - 메모에 날짜가 전혀 없으면 start 와 end 를 오늘로 둔다.
              - title 은 서른 글자를 넘기지 않는다. 무엇을 하는 일정인지 드러나게 쓴다.
              - summary 는 두 줄에서 네 줄로, 메모에 없는 말을 지어내지 않는다. 줄은 \n 으로 가른다.

              메모:
              {{text}}
              """,
            cancellationToken);

        return Parse(answer, model);
    }

    /// <summary>모델의 답에서 JSON 한 덩이를 꺼내 읽는다. 앞뒤에 코드 울타리(```)나 설명을 붙여 오는 일이 잦아 첫 { 부터 마지막 } 까지만 본다.</summary>
    internal static ScheduleDraft Parse(string? answer, string? model)
    {
        var empty = new ScheduleDraft(null, null, null, null, model);

        if (string.IsNullOrWhiteSpace(answer))
        {
            return empty;
        }

        var open = answer.IndexOf('{');
        var close = answer.LastIndexOf('}');

        if (open < 0 || close <= open)
        {
            return empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(answer[open..(close + 1)]);
            var root = doc.RootElement;

            var start = Date(root, "start");
            var end = Date(root, "end") ?? start;

            if (start is not null && end < start)
            {
                end = start;
            }

            return new ScheduleDraft(start, end, Text(root, "title"), Text(root, "summary"), model);
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && v.GetString() is { Length: > 0 } s && !string.IsNullOrWhiteSpace(s)
            ? s.Trim()
            : null;

    private static DateOnly? Date(JsonElement root, string name) =>
        Text(root, name) is { } s
        && DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>한 번 묻고 답을 한 덩이로 받는다. 대화로 담지 않는다(sessionId 없음) — 글자를 칠 때마다 AI쳇 목록에 한 줄씩 쌓인다.</summary>
    private async Task<(string? Text, string? Model)> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        var answer = new StringBuilder();
        string? model = null;

        await foreach (var part in ai.StreamAsync(
            [new AiChatMessage("user", prompt)], sessionId: null,
            cancellationToken: cancellationToken))
        {
            if (part.IsUsedMarker && part.Notice is { Length: > 0 } used)
            {
                model = used;
                continue;
            }

            // 「다른 공급자로 넘깁니다」 같은 알림이 JSON 앞에 섞이면 못 읽는다.
            if (part.Notice is not null)
            {
                continue;
            }

            answer.Append(part.Text);
        }

        var text = answer.ToString().Trim();

        return (text.Length == 0 ? null : text, model);
    }
}
