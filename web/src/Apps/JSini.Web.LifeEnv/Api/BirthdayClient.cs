using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.LifeEnv.Api;

/// <summary>
/// 생일 조회·축하. 게이트웨이의 <c>/auth/birthday/*</c> 로 나간다.
/// 생일 자료는 사람(계정)에 딸린 것이라 백엔드는 AuthServer 다 — 화면만 생활과환경 모듈에 있다.
/// </summary>
public sealed class BirthdayClient(GatewayClient gateway)
{
    /// <summary>이 달의 생일자. 달을 주지 않으면 오늘이 속한 달.</summary>
    public Task<IReadOnlyList<BirthdayPerson>> GetMonthAsync(
        int? month = null, string? companyId = null, string? departmentId = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayPerson>(
            month is null
                ? "auth/birthday/current" + Query(("companyId", companyId), ("departmentId", departmentId))
                : "auth/birthday/list" + Query(("month", month), ("companyId", companyId), ("departmentId", departmentId)),
            ct);

    /// <summary>오늘의 생일자. 올해 받은 축하 수가 함께 온다.</summary>
    public Task<IReadOnlyList<BirthdayToday>> GetTodayAsync(
        string? companyId = null, string? departmentId = null, CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayToday>(
            "auth/birthday/today" + Query(("companyId", companyId), ("departmentId", departmentId)), ct);

    /// <summary>달별 생일자 수. 캘린더 화면의 요약 줄이 쓴다.</summary>
    public Task<IReadOnlyList<BirthdayMonthStat>> GetStatsAsync(
        string? companyId = null, string? departmentId = null, CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayMonthStat>(
            "auth/birthday/stats" + Query(("companyId", companyId), ("departmentId", departmentId)), ct);

    /// <summary>
    /// 캘린더에 찍을 사건. 응답이 FullCalendar 모양이라 화면에서 스케줄러 모양으로 옮겨 담는다 — 백엔드는 건드리지 않는다.
    /// 기간은 yyyy-MM-dd 로 넘긴다.
    /// </summary>
    public Task<IReadOnlyList<BirthdayCalendarEvent>> GetCalendarAsync(
        DateTime start, DateTime end, string? companyId = null, string? departmentId = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayCalendarEvent>(
            "auth/birthday/calendar" + Query(
                ("start", start.ToString("yyyy-MM-dd")),
                ("end", end.ToString("yyyy-MM-dd")),
                ("companyId", companyId), ("departmentId", departmentId)), ct);

    /// <summary>오늘의 생일자들이 올해 받은 축하 메시지.</summary>
    public Task<IReadOnlyList<BirthdayMessage>> GetTodayMessagesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayMessage>("auth/birthday/today/messages", ct);

    /// <summary>내가 받은 축하.</summary>
    public Task<IReadOnlyList<BirthdayMessage>> GetReceivedAsync(CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayMessage>("auth/birthday/message", ct);

    /// <summary>내가 보낸 축하.</summary>
    public Task<IReadOnlyList<BirthdayMessage>> GetSentAsync(CancellationToken ct = default)
        => gateway.GetListAsync<BirthdayMessage>("auth/birthday/message/sent", ct);

    /// <summary>축하를 보낸다.</summary>
    public Task SendAsync(string recipientId, string content, CancellationToken ct = default)
        => gateway.PostAsync("auth/birthday/message", new { recipientId, content }, ct);

    /// <summary>쿼리스트링을 만든다. 값이 null 이거나 빈 문자열이면 뺀다.</summary>
    private static string Query(params (string Key, object? Value)[] parameters)
    {
        var parts = new List<string>();

        foreach (var (key, value) in parameters)
        {
            var text = value switch
            {
                null => null,
                string s => string.IsNullOrWhiteSpace(s) ? null : s,
                _ => value.ToString(),
            };

            if (text is not null)
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
            }
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}

/// <summary>
/// 이 달(또는 지정한 달)의 생일자 한 명. <c>sealed</c> 가 아닌 것은 <see cref="BirthdayToday"/> 가 이것에 축하 수 한 칸을 더해 쓰기 때문이다.
/// </summary>
public class BirthdayPerson
{
    public string Id { get; set; } = string.Empty;

    /// <summary>축하를 보낼 상대의 계정 아이디.</summary>
    public string SubjectId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>실제 생년월일.</summary>
    public DateOnly BirthDate { get; set; }

    /// <summary>올해 그 생일이 오는 날 — 음력 생일은 해마다 달라 BirthDate 만으로는 캘린더에 찍을 수 없다.</summary>
    public DateOnly OccurrenceDate { get; set; }

    public bool IsLunar { get; set; }

    /// <summary>축하 대상인가. 퇴사자·비대상은 꺼진다.</summary>
    public bool IsCelebrated { get; set; }

    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
}

/// <summary>오늘의 생일자. 올해 받은 축하 수가 붙는다.</summary>
public sealed class BirthdayToday : BirthdayPerson
{
    public int MessageCount { get; set; }
}

/// <summary>달별 생일자 수.</summary>
public sealed class BirthdayMonthStat
{
    public int Month { get; set; }
    public int Total { get; set; }
    public int Solar { get; set; }
    public int Lunar { get; set; }
}

/// <summary>
/// 축하 메시지 한 건. 세 엔드포인트의 모양이 조금씩 달라 <b>합집합 하나</b>로 받는다 — 안 오는 칸은 null.
/// </summary>
public sealed class BirthdayMessage
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string? SenderName { get; set; }
    public string? SenderDepartment { get; set; }

    public string? RecipientName { get; set; }
    public string? RecipientDepartment { get; set; }
    public string? RecipientId { get; set; }
}

/// <summary>
/// 캘린더 사건. FullCalendar 모양 그대로 받는다.
/// </summary>
public sealed class BirthdayCalendarEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>시작일. <c>yyyy-MM-dd</c> 문자열로 온다.</summary>
    public string Start { get; set; } = string.Empty;

    public bool AllDay { get; set; }
    public BirthdayCalendarEventProps ExtendedProps { get; set; } = new();

    /// <summary>화면에 쓸 <see cref="DateTime"/>. 못 읽으면 오늘로 둔다.</summary>
    public DateTime StartDate =>
        DateTime.TryParse(Start, out var value) ? value.Date : AppTime.TodayDate;
}

/// <summary>캘린더 사건의 부가 정보.</summary>
public sealed class BirthdayCalendarEventProps
{
    public string Type { get; set; } = string.Empty;
    public bool IsLunar { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OriginalBirthDate { get; set; } = string.Empty;
}
