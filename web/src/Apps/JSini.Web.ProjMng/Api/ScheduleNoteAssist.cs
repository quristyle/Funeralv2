using System.Globalization;
using System.Text;
using System.Text.Json;
using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>본문에서 뽑아 낸 일정 한 건. 못 뽑은 칸은 null 이다.</summary>
public sealed record ScheduleDraft(
    DateOnly? Start,
    DateOnly? End,
    string? Title,
    string? Summary,
    string? Requester = null,
    string? Assignee = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    string? Model = null)
{
    public ScheduleDraft(DateOnly? start, DateOnly? end, string? title, string? summary, string? model)
        : this(start, end, title, summary, null, null, null, null, model) { }

    public DateTime? StartDateTime =>
        Start?.ToDateTime(StartTime ?? TimeOnly.MinValue);

    public DateTime? EndDateTime =>
        End?.ToDateTime(EndTime ?? (StartTime.HasValue && !EndTime.HasValue ? StartTime.Value : TimeOnly.MinValue));
}

/// <summary>일정 메모 화면의 본문을 읽고 날짜·제목·요약·요구자·담당자를 짓는다. 묻는 길은 AI 쳇과 같은 AiChatClient 다(AiTaskAssist 와 같다).</summary>
public sealed class ScheduleNoteAssist(AiChatClient ai)
{
    /// <summary>AI 에게 넘기는 본문의 상한(글자). 넘기면 429·502 로 돌아온다.</summary>
    private const int MaxChars = 6000;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d", "yyyy.MM.dd", "yyyy/MM/dd"];
    private static readonly string[] TimeFormats = ["HH:mm:ss", "HH:mm", "H:mm", "h:mm tt", "hh:mm tt"];

    /// <summary>본문을 읽어 일정 칸을 짓는다. 답을 못 읽으면 칸이 전부 null 인 것을 돌려준다.</summary>
    public Task<ScheduleDraft> DraftAsync(string? contents, CancellationToken cancellationToken = default) =>
        DraftAsync(contents, null, cancellationToken);

    /// <summary>본문과 기본 담당자 성명을 받아 일정 칸(날짜·시간·제목·요약·요구자·담당자)을 짓는다.</summary>
    public async Task<ScheduleDraft> DraftAsync(string? contents, string? defaultAssignee, CancellationToken cancellationToken = default)
    {
        var text = contents?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return new ScheduleDraft(null, null, null, null, null, defaultAssignee, null, null, null);
        }

        if (text.Length > MaxChars)
        {
            text = text[..MaxChars];
        }

        // 「내일」「다음 주 화요일」을 풀려면 오늘이 있어야 한다 — 모델의 시계는 믿을 수 없다.
        var today = AppTime.Today;
        var assigneeHint = !string.IsNullOrWhiteSpace(defaultAssignee)
            ? $"- 기본 담당자 성명은 \"{defaultAssignee.Trim()}\" 이다."
            : string.Empty;

        var (answer, model) = await AskAsync(
            $$"""
              너는 업무 메모를 읽고 일정표에 올릴 한 건으로 정리하는 사람이다.
              오늘은 {{today:yyyy-MM-dd}} ({{today.ToString("dddd", CultureInfo.GetCultureInfo("ko-KR"))}}) 이다.
              {{assigneeHint}}

              아래 메모를 읽고 JSON 하나만 써라. 다른 말은 쓰지 않는다.
              {"start":"yyyy-MM-dd","start_time":"HH:mm","end":"yyyy-MM-dd","end_time":"HH:mm","title":"제목","summary":"요약","requester":"요구자","assignee":"담당자"}

              규칙:
              - 한국어로 쓴다.
              - start 는 일정이 시작하는 날, end 는 끝나는 날이다. 하루짜리면 둘이 같다.
              - start_time, end_time 은 메모에 시작/종료 시간이 명시되어 있으면 "HH:mm"(24시간 형식)으로 적는다. 시간 정보가 없으면 null 로 둔다.
              - 「내일」「다음 주 금요일」처럼 상대적인 날은 오늘을 기준으로 실제 날짜로 바꾼다.
              - 메모에 날짜가 전혀 없으면 start 와 end 를 오늘로 둔다.
              - title 은 서른 글자를 넘기지 않는다. 무엇을 하는 일정인지 드러나게 쓴다.
              - summary 는 두 줄에서 네 줄로, 메모에 없는 말을 지어내지 않는다. 줄은 \n 으로 가른다.
              - requester 는 일정을 요구/요청한 사람의 성명을 적는다. 메모에 없으면 null 로 둔다.
              - assignee 는 일정의 담당자 성명을 적는다. 메모에 별도 담당자 언급이 없으면 기본 담당자({{defaultAssignee ?? "null"}})를 쓴다.

              메모:
              {{text}}
              """,
            cancellationToken);

        return Parse(answer, defaultAssignee, model);
    }

    /// <summary>
    /// 본문에 일정이 여럿 들어 있으면 여럿으로 가른다. 늘 한 건 이상을 돌려준다 —
    /// 답을 못 읽어도 칸이 전부 비어 있는(기본 담당자만 찬) 한 건이 나온다.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleDraft>> DraftManyAsync(string? contents, string? defaultAssignee, CancellationToken cancellationToken = default)
    {
        var text = contents?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return [new ScheduleDraft(null, null, null, null, null, defaultAssignee, null, null, null)];
        }

        if (text.Length > MaxChars)
        {
            text = text[..MaxChars];
        }

        var today = AppTime.Today;
        var assigneeHint = !string.IsNullOrWhiteSpace(defaultAssignee)
            ? $"- 기본 담당자 성명은 \"{defaultAssignee.Trim()}\" 이다."
            : string.Empty;

        var (answer, model) = await AskAsync(
            $$"""
              너는 업무 메모를 읽고 일정표에 올릴 일정으로 정리하는 사람이다.
              오늘은 {{today:yyyy-MM-dd}} ({{today.ToString("dddd", CultureInfo.GetCultureInfo("ko-KR"))}}) 이다.
              {{assigneeHint}}

              아래 메모를 읽고 JSON 하나만 써라. 다른 말은 쓰지 않는다.
              {"schedules":[{"start":"yyyy-MM-dd","start_time":"HH:mm","end":"yyyy-MM-dd","end_time":"HH:mm","title":"제목","summary":"요약","requester":"요구자","assignee":"담당자"}]}

              규칙:
              - 한국어로 쓴다.
              - 메모에 날짜·대상·할 일이 서로 다른 일정이 여럿 있으면 schedules 에 하나씩 따로 담는다(최대 {{MaxDrafts}}건).
                한 가지 일을 여러 줄로 풀어 쓴 것이면 한 건이다. 일정이 하나뿐이어도 schedules 는 배열로 쓰고, 비워 두지 않는다.
              - start 는 일정이 시작하는 날, end 는 끝나는 날이다. 하루짜리면 둘이 같다.
              - start_time, end_time 은 메모에 시작/종료 시간이 명시되어 있으면 "HH:mm"(24시간 형식)으로 적는다. 시간 정보가 없으면 null 로 둔다.
              - 「내일」「다음 주 금요일」처럼 상대적인 날은 오늘을 기준으로 실제 날짜로 바꾼다.
              - 메모에 날짜가 전혀 없으면 start 와 end 를 오늘로 둔다.
              - title 은 서른 글자를 넘기지 않는다. 무엇을 하는 일정인지 드러나게 쓴다.
              - summary 는 그 일정에 해당하는 내용만 두 줄에서 네 줄로, 메모에 없는 말을 지어내지 않는다. 줄은 \n 으로 가른다.
              - requester 는 일정을 요구/요청한 사람의 성명을 적는다. 메모에 없으면 null 로 둔다.
              - assignee 는 일정의 담당자 성명을 적는다. 메모에 별도 담당자 언급이 없으면 기본 담당자({{defaultAssignee ?? "null"}})를 쓴다.

              메모:
              {{text}}
              """,
            cancellationToken);

        return ParseMany(answer, defaultAssignee, model);
    }

    /// <summary>한 메모에서 가르는 일정의 상한. 모델이 줄마다 한 건씩 쪼개 일정표를 덮는 것을 막는다.</summary>
    internal const int MaxDrafts = 10;

    /// <summary>
    /// 모델의 답에서 일정 여럿을 꺼낸다. <c>{"schedules":[…]}</c>·맨 배열·한 건짜리 객체를 모두 받고,
    /// 하나도 못 읽으면 빈 칸 한 건을 돌려준다(결과는 늘 한 건 이상이다).
    /// </summary>
    internal static IReadOnlyList<ScheduleDraft> ParseMany(string? answer, string? defaultAssignee, string? model)
    {
        var empty = new ScheduleDraft(null, null, null, null, null, defaultAssignee, null, null, model);

        if (string.IsNullOrWhiteSpace(answer))
        {
            return [empty];
        }

        var objOpen = answer.IndexOf('{');
        var arrOpen = answer.IndexOf('[');
        var isArray = arrOpen >= 0 && (objOpen < 0 || arrOpen < objOpen);
        var open = isArray ? arrOpen : objOpen;
        var close = answer.LastIndexOf(isArray ? ']' : '}');

        if (open < 0 || close <= open)
        {
            return [empty];
        }

        try
        {
            using var doc = JsonDocument.Parse(answer[open..(close + 1)]);
            var root = doc.RootElement;

            JsonElement? list = root.ValueKind == JsonValueKind.Array ? root : null;
            if (list is null && root.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in (string[])["schedules", "items", "events", "일정"])
                {
                    if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
                    {
                        list = v;
                        break;
                    }
                }
            }

            if (list is null)
            {
                return root.ValueKind == JsonValueKind.Object
                    ? [FromElement(root, defaultAssignee, model)]
                    : [empty];
            }

            var drafts = list.Value.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object)
                .Select(e => FromElement(e, defaultAssignee, model))
                .Where(d => d.Start is not null || d.Title is not null || d.Summary is not null)
                .Take(MaxDrafts)
                .ToList();

            return drafts.Count > 0 ? drafts : [empty];
        }
        catch (JsonException)
        {
            return [empty];
        }
    }

    /// <summary>모델의 답에서 JSON 한 덩이를 꺼내 읽는다. 앞뒤에 코드 울타리(```)나 설명을 붙여 오는 일이 잦아 첫 { 부터 마지막 } 까지만 본다.</summary>
    internal static ScheduleDraft Parse(string? answer, string? model) =>
        Parse(answer, null, model);

    /// <summary>모델의 답에서 JSON 한 덩이를 꺼내 읽는다. 담당자가 명시되지 않았으면 기본 담당자를 채운다.</summary>
    internal static ScheduleDraft Parse(string? answer, string? defaultAssignee, string? model)
    {
        var empty = new ScheduleDraft(null, null, null, null, null, defaultAssignee, null, null, model);

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

            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? FromElement(doc.RootElement, defaultAssignee, model)
                : empty;
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static ScheduleDraft FromElement(JsonElement root, string? defaultAssignee, string? model)
    {
        var (start, startTime) = DateTimeOrDate(root, "start", "start_time");
        var (end, endTime) = DateTimeOrDate(root, "end", "end_time");
        end ??= start;

        if (start is not null && end < start)
        {
            end = start;
            endTime = null;
        }

        if (start is not null && end == start && startTime.HasValue && endTime.HasValue && endTime < startTime)
        {
            endTime = startTime;
        }

        var title = Text(root, "title");
        var summary = Text(root, "summary");
        var requester = Text(root, "requester") ?? Text(root, "req_user") ?? Text(root, "reqUser") ?? Text(root, "요구자") ?? Text(root, "요청자");
        var assignee = Text(root, "assignee") ?? Text(root, "dev_user") ?? Text(root, "devUser") ?? Text(root, "담당자");

        if (string.IsNullOrWhiteSpace(assignee) && !string.IsNullOrWhiteSpace(defaultAssignee))
        {
            assignee = defaultAssignee.Trim();
        }

        return new ScheduleDraft(start, end, title, summary, requester, assignee, startTime, endTime, model);
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && v.GetString() is { Length: > 0 } s && !string.IsNullOrWhiteSpace(s)
            ? s.Trim()
            : null;

    private static (DateOnly? Date, TimeOnly? Time) DateTimeOrDate(JsonElement root, string dateProp, string timeProp)
    {
        DateOnly? d = null;
        TimeOnly? t = Time(root, timeProp);

        if (Text(root, dateProp) is { } s)
        {
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                d = DateOnly.FromDateTime(dt);
                if (t is null && (dt.Hour != 0 || dt.Minute != 0 || dt.Second != 0))
                {
                    t = TimeOnly.FromDateTime(dt);
                }
            }
            else if (DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dOnly))
            {
                d = dOnly;
            }
        }

        return (d, t);
    }

    private static TimeOnly? Time(JsonElement root, string name)
    {
        if (Text(root, name) is { } s)
        {
            if (TimeOnly.TryParseExact(s, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            {
                return t;
            }
            if (TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var t2))
            {
                return t2;
            }
        }
        return null;
    }

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
