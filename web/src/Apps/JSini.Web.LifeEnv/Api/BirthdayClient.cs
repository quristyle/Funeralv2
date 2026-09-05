using JSini.Web.Http;

namespace JSini.Web.LifeEnv.Api;

/// <summary>
/// 생일 조회·축하 메시지. 게이트웨이의 <c>/auth/birthday/*</c> 로 나간다.
///
/// [왜 LifeEnvServer 가 아니라 AuthServer 인가]
///
/// 생일은 <b>사람에 딸린 자료</b>다. 생년월일도, 소속도, 축하를 보낼 상대도
/// 전부 계정 표에 있다. 한때 LifeEnvServer 가 들고 있었는데 계정을 조인하려고
/// 사용자 표를 복제하게 되어 포털로 옮겼다
/// (<c>LifeEnvServer/Program.cs</c> 에 그 자취가 주석으로 남아 있다).
///
/// 화면이 생활과환경 모듈에 있는 것은 <b>사용자에게 그렇게 보이기 때문</b>이다 —
/// 기상과 생일이 "오늘 알아 둘 것" 으로 같은 묶음이다. 자료의 주인과 화면의
/// 자리가 다른 것은 이상한 일이 아니다.
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
    /// 캘린더에 찍을 사건들. 기간은 <c>yyyy-MM-dd</c> 로 넘긴다.
    ///
    /// 응답이 FullCalendar 모양인 것은 Vue 화면이 그 라이브러리를 썼기 때문이다.
    /// Blazor 는 DevExpress 스케줄러를 쓰므로 화면에서 옮겨 담는다 —
    /// <b>백엔드를 건드리지 않는다.</b> 이행하는 동안 백엔드는 그대로 두는 것이
    /// 이 저장소의 규칙이다.
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
/// 이 달(또는 지정한 달)의 생일자 한 명.
///
/// <c>sealed</c> 가 아닌 이유는 <see cref="BirthdayToday"/> 가 여기에 축하 수
/// 한 칸만 더해서 쓰기 때문이다. 서버 응답이 실제로 그 관계다.
/// </summary>
public class BirthdayPerson
{
    public string Id { get; set; } = string.Empty;

    /// <summary>축하를 보낼 상대의 계정 아이디.</summary>
    public string SubjectId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>실제 생년월일.</summary>
    public DateOnly BirthDate { get; set; }

    /// <summary>
    /// 올해 그 생일이 오는 날.
    ///
    /// 음력 생일은 해마다 양력 날짜가 달라져서 <see cref="BirthDate"/> 만으로는
    /// 캘린더에 찍을 수 없다. 서버가 그 해 기준으로 환산해 준다.
    /// </summary>
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
/// 축하 메시지 한 건.
///
/// 서버가 세 엔드포인트에서 조금씩 다른 모양으로 준다(받은 것 · 보낸 것 ·
/// 오늘의 것). 화면이 셋을 같은 표로 보여 주므로 <b>합집합 하나</b>로 받는다 —
/// 안 오는 칸은 <c>null</c> 이다. 셋으로 나누면 화면도 셋이 된다.
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
        DateTime.TryParse(Start, out var value) ? value.Date : DateTime.Today;
}

/// <summary>캘린더 사건의 부가 정보.</summary>
public sealed class BirthdayCalendarEventProps
{
    public string Type { get; set; } = string.Empty;
    public bool IsLunar { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OriginalBirthDate { get; set; } = string.Empty;
}
