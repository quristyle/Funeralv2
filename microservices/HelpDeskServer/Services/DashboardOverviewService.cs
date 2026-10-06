using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using HelpDeskServer.Helpers;
using HelpDeskServer.Models;
using HelpDeskServer.Utilities;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// 헬프데스크 현황판 집계.
///
/// <para>
/// [왜 SQL 로 열몇 번 묻지 않고 한 번 읽어 와서 접는가]
/// </para>
///
/// <para>
/// 현황판이 뽑는 지표가 예순 가지가 넘는데 <b>재료는 요청 목록 하나</b>다.
/// 지표마다 <c>GroupBy</c> 를 짜면 왕복이 스무 번 나고, 같은 표를 스무 번
/// 훑는다. 게다가 「몇 시에 몰리나」·「무슨 요일에 몰리나」는 <b>KST 로
/// 갈라야</b> 맞는데(<see cref="Kst"/> 머리말), 그 환산을 SQL 로 밀어 넣으면
/// 서버 타임존 설정에 기대게 된다.
/// </para>
///
/// <para>
/// 그래서 <b>필요한 칸만 골라 한 번 읽고</b> 나머지는 메모리에서 센다. 읽는
/// 것은 요청 한 줄당 열두 칸이고 본문(<c>description</c>)은 안 가져온다 —
/// 요청 수가 만 건이라도 몇 MB 다. 그보다 커지면 그때 기간을 자르는 조건을
/// 먼저 건다(지금 이 DB 는 세 자리다).
/// </para>
/// </summary>
public sealed class DashboardOverviewService(AppDbContext db, IPortalCompanyDirectory companies) {
  /// <summary>회사를 알 수 없는 요청을 모으는 자리. 빈 칸으로 두면 표에서 사라진다.</summary>
  private const string UnknownCompanyId = "(미지정)";

  /// <summary>대기 경과를 가르는 지점(일). 마지막 구간은 열려 있다.</summary>
  private static readonly (double Days, string Label)[] AgingBuckets = [
    (1, "1일 미만"),
    (3, "1~3일"),
    (7, "3~7일"),
    (14, "7~14일"),
    (30, "14~30일"),
    (double.PositiveInfinity, "30일 이상"),
  ];

  private static readonly string[] WeekdayNames = ["월", "화", "수", "목", "금", "토", "일"];

  /// <summary>
  /// 현황판 한 판을 만든다.
  /// </summary>
  /// <param name="me">지금 보고 있는 사람. 고객이면 제 회사로 범위가 좁혀진다.</param>
  /// <param name="companyId">여기에 값이 있으면 그 회사 것만 센다.</param>
  /// <param name="days">일별 추이 기간(일).</param>
  /// <param name="months">월별 추이 기간(달).</param>
  /// <param name="topN">목록형 조각(오래된 대기 · 요청자 · 최근 접수)의 길이.</param>
  /// <param name="ct">취소 토큰.</param>
  public async Task<DashboardOverviewDto> BuildAsync(
      HelpdeskPrincipal me,
      string? companyId,
      int days,
      int months,
      int topN,
      CancellationToken ct = default) {
    days = Math.Clamp(days, 7, 180);
    months = Math.Clamp(months, 3, 36);
    topN = Math.Clamp(topN, 3, 50);

    // 고객으로 연결된 계정은 **제 회사 밖을 보지 못한다.** 조건으로 남의 회사를
    // 적어 보내도 여기서 덮어쓴다 — 조건은 화면이 보내는 값이라 믿을 수 없다.
    var scoped = me.IsCustomer && me.HasCompany
        ? me.CompanyId
        : (string.IsNullOrWhiteSpace(companyId) ? null : companyId);

    var query = db.Requests.Where(r => r.Status != ImprovementStatus.Delete);
    if (scoped is not null) {
      query = query.Where(r => r.Customer != null && r.Customer.CompanyId == scoped);
    }

    var rows = await query
        .Select(r => new Row {
          Id = r.Id,
          Title = r.Title,
          Status = r.Status,
          Type = r.IpType,
          RequestedAt = r.RequestedAt,
          AcceptedAt = r.AcceptedAt,
          CompletedAt = r.CompletededAt,
          UserCompletedAt = r.UserCompletededAt,
          ModifiedAt = r.ModifiedAt,
          AdminId = r.AdminId,
          AdminName = r.Admin == null ? null : r.Admin.UserName,
          CustomerId = r.CustomerId,
          CustomerName = r.Customer == null ? null : r.Customer.UserName,
          CompanyId = r.Customer == null ? null : r.Customer.CompanyId,
        })
        .ToListAsync(ct);

    // 댓글은 요청별 개수만 있으면 된다. 본문을 끌어오지 않는다.
    // 지운 줄은 빼고 센다 — 「댓글」 칸과 「무응답」 집계가 상세 화면과 같은
    // 수를 가리켜야 한다.
    var commentCounts = await db.Comments
        .Where(c => !c.IsDel)
        .GroupBy(c => c.RequestId)
        .Select(g => new { RequestId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.RequestId, x => x.Count, ct);

    // 담당자 목록은 따로 읽는다 — 요청에서 뽑아 묶으면 **아직 한 건도 안 맡은
    // 담당자가 표에서 통째로 빠진다.** 「배정 0」도 봐야 하는 자료다.
    var admins = await db.Admins
        .Where(a => !a.IsDeleted)
        .Select(a => new { a.Id, a.UserName, a.Photo })
        .ToListAsync(ct);

    var companyNames = await companies.GetNamesAsync(ct);

    return Fold(me, scoped, companyNames, rows, commentCounts,
        admins.Select(a => (a.Id, a.UserName, a.Photo)).ToList(), days, months, topN);
  }

  /// <summary>
  /// 읽어 온 줄을 화면이 쓰는 모양으로 접는다. DB 를 건드리지 않아 순수 계산이다.
  /// </summary>
  private static DashboardOverviewDto Fold(
      HelpdeskPrincipal me,
      string? scopedCompanyId,
      IReadOnlyDictionary<string, string> companyNames,
      List<Row> rows,
      IReadOnlyDictionary<int, int> commentCounts,
      List<(int Id, string UserName, string? Photo)> admins,
      int days,
      int months,
      int topN) {
    var nowUtc = DateTime.UtcNow;
    var today = Kst.Today;
    var monthStart = new DateOnly(today.Year, today.Month, 1);
    var lastMonthStart = monthStart.AddMonths(-1);
    var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // 월요일

    foreach (var row in rows) {
      row.Comments = commentCounts.GetValueOrDefault(row.Id);
      row.RequestedKst = Kst.FromUtc(row.RequestedAt);
      row.ResolvedKst = Kst.FromUtc(row.ResolvedAt);
      row.ClosedKst = Kst.FromUtc(row.ClosedAt);
      row.AgeDays = ((row.ClosedAt ?? nowUtc) - DateTime.SpecifyKind(row.RequestedAt, DateTimeKind.Utc)).TotalDays;
    }

    var total = rows.Count;
    var open = rows.Where(r => r.IsOpen).ToList();
    var resolved = rows.Where(r => r.IsResolved && r.ResolvedAt.HasValue).ToList();
    var accepted = rows.Where(r => r.AcceptedAt.HasValue).ToList();

    var responseHours = accepted
        .Select(r => (DateTime.SpecifyKind(r.AcceptedAt!.Value, DateTimeKind.Utc)
                      - DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc)).TotalHours)
        .Where(h => h >= 0)
        .ToList();

    var resolutionHours = resolved
        .Select(r => (DateTime.SpecifyKind(r.ResolvedAt!.Value, DateTimeKind.Utc)
                      - DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc)).TotalHours)
        .Where(h => h >= 0)
        .ToList();

    var rejected = rows.Count(r => r.Status == ImprovementStatus.Rejected);
    var completed = rows.Count(r => r.Status is ImprovementStatus.Completed or ImprovementStatus.UserCompleted);

    var summary = new DashboardSummaryDto {
      Total = total,
      Pending = rows.Count(r => r.Status == ImprovementStatus.Pending),
      InProgress = rows.Count(r => r.Status == ImprovementStatus.InProgress),
      Consultation = rows.Count(r => r.Status == ImprovementStatus.Consultation),
      Negotiation = rows.Count(r => r.Status == ImprovementStatus.Negotiation),
      Completed = rows.Count(r => r.Status == ImprovementStatus.Completed),
      UserCompleted = rows.Count(r => r.Status == ImprovementStatus.UserCompleted),
      Rejected = rejected,
      Open = open.Count,
      Closed = total - open.Count,
      Unassigned = open.Count(r => r.AdminId is null),
      EmergencyOpen = open.Count(r => r.Type == ImprovementType.Emergency),
      Today = rows.Count(r => DateOnly.FromDateTime(r.RequestedKst) == today),
      Yesterday = rows.Count(r => DateOnly.FromDateTime(r.RequestedKst) == today.AddDays(-1)),
      ThisWeek = rows.Count(r => DateOnly.FromDateTime(r.RequestedKst) >= weekStart),
      ThisMonth = rows.Count(r => DateOnly.FromDateTime(r.RequestedKst) >= monthStart),
      LastMonth = rows.Count(r => {
        var d = DateOnly.FromDateTime(r.RequestedKst);
        return d >= lastMonthStart && d < monthStart;
      }),
      CompletedToday = resolved.Count(r => DateOnly.FromDateTime(r.ResolvedKst!.Value) == today),
      CompletedThisMonth = resolved.Count(r => DateOnly.FromDateTime(r.ResolvedKst!.Value) >= monthStart),
      CompletionRate = Percent(completed, total - rejected),
      AvgResponseHours = Round(responseHours.Count > 0 ? responseHours.Average() : 0),
      ResponseSamples = responseHours.Count,
      AvgResolutionHours = Round(resolutionHours.Count > 0 ? resolutionHours.Average() : 0),
      MedianResolutionHours = Round(Median(resolutionHours)),
      ResolutionSamples = resolutionHours.Count,
      Sla24Rate = Percent(responseHours.Count(h => h <= 24), responseHours.Count),
      Sla72Rate = Percent(resolutionHours.Count(h => h <= 72), resolutionHours.Count),
      OldestPendingDays = Round(open.Count > 0 ? open.Max(r => r.AgeDays) : 0),
      AvgOpenAgeDays = Round(open.Count > 0 ? open.Average(r => r.AgeDays) : 0),
      Comments = rows.Sum(r => r.Comments),
      CommentsPerRequest = Round(total > 0 ? (double)rows.Sum(r => r.Comments) / total : 0),
      NoReplyOpen = open.Count(r => r.Comments == 0),
      Requesters = rows.Select(r => r.CustomerId).Distinct().Count(),
      ActiveAdmins = rows.Where(r => r.AdminId.HasValue).Select(r => r.AdminId!.Value).Distinct().Count(),
      // 회사를 모르는 줄은 세지 않는다 — 아래 표에서는 「(미지정)」 한 줄로
      // 서지만 그것은 회사가 아니다.
      ActiveCompanies = rows.Where(r => r.CompanyId is not null).Select(r => r.CompanyId!).Distinct().Count(),
      LastRequestedAt = rows.Count > 0 ? rows.Max(r => r.RequestedAt) : null,
    };

    summary.MonthOverMonthRate = summary.LastMonth > 0
        ? Round((double)(summary.ThisMonth - summary.LastMonth) / summary.LastMonth * 100)
        : 0;

    return new DashboardOverviewDto {
      Scope = new DashboardScopeDto {
        IsAdmin = me.IsAdmin,
        IsLinked = me.IsLinked,
        CompanyScoped = scopedCompanyId is not null,
        CompanyId = scopedCompanyId,
        CompanyName = scopedCompanyId is null ? null : CompanyName(companyNames, scopedCompanyId),
        Label = scopedCompanyId is null ? "전체" : CompanyName(companyNames, scopedCompanyId),
        Days = days,
        Months = months,
        GeneratedAt = nowUtc,
      },
      Summary = summary,
      Status = StatusSlices(rows),
      Types = TypeSlices(rows),
      Monthly = MonthlySeries(rows, today, months),
      Daily = DailySeries(rows, today, days),
      Weekday = WeekdaySlices(rows),
      Hourly = HourlySlices(rows),
      Companies = CompanyRows(rows, companyNames, monthStart),
      // 담당자별 성적은 **담당자에게만** 보인다. 고객 화면에 남의 완료율·
      // 점유율이 서는 자리가 아니다 — 화면에서 감추는 것으로는 응답에 그대로
      // 실려 나간다.
      Admins = me.IsAdmin ? AdminRows(rows, admins, monthStart, completed) : [],
      PendingAging = AgingSlices(open),
      OldestPending = open
          .Where(r => r.Status == ImprovementStatus.Pending)
          .OrderByDescending(r => r.AgeDays)
          .Take(topN)
          .Select(r => ToRequestDto(r, companyNames))
          .ToList(),
      TopCustomers = CustomerRows(rows, companyNames, topN),
      Recent = rows
          .OrderByDescending(r => r.RequestedAt)
          .Take(topN)
          .Select(r => ToRequestDto(r, companyNames))
          .ToList(),
    };
  }

  // ── 조각들 ─────────────────────────────────────────────────────

  /// <summary>상태별 분포. <b>0 건인 상태도 남긴다</b> — 화면이 골라 뺀다.</summary>
  private static List<DashboardSliceDto> StatusSlices(List<Row> rows) {
    ImprovementStatus[] order = [
      ImprovementStatus.Pending, ImprovementStatus.InProgress, ImprovementStatus.Consultation,
      ImprovementStatus.Negotiation, ImprovementStatus.Completed, ImprovementStatus.UserCompleted,
      ImprovementStatus.Rejected,
    ];

    return [.. order.Select(s => new DashboardSliceDto {
      Key = s.ToString(),
      Label = s.GetDisplayName(),
      Count = rows.Count(r => r.Status == s),
      Share = Percent(rows.Count(r => r.Status == s), rows.Count),
    })];
  }

  /// <summary>유형별 분포. 한 건도 없는 유형은 뺀다 — 일곱 칸이 다 뜨면 읽히지 않는다.</summary>
  private static List<DashboardSliceDto> TypeSlices(List<Row> rows) =>
      [.. Enum.GetValues<ImprovementType>()
          .Select(t => new DashboardSliceDto {
            Key = t.ToString(),
            Label = t.GetDisplayName(),
            Count = rows.Count(r => r.Type == t),
            Share = Percent(rows.Count(r => r.Type == t), rows.Count),
          })
          .Where(s => s.Count > 0)
          .OrderByDescending(s => s.Count)];

  /// <summary>
  /// 월별 추이. <b>빈 달도 0 으로 채워 칸 수를 맞춘다</b> — 안 채우면 차트의
  /// 가로축이 자료 있는 달만 늘어놓아 「한동안 조용했다」가 안 보인다.
  /// </summary>
  private static List<DashboardMonthDto> MonthlySeries(List<Row> rows, DateOnly today, int months) {
    var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
    var result = new List<DashboardMonthDto>(months);

    for (var i = 0; i < months; i++) {
      var start = first.AddMonths(i);
      var end = start.AddMonths(1);

      var requested = rows.Where(r => InMonth(r.RequestedKst, start, end)).ToList();
      var resolvedInMonth = rows
          .Where(r => r.ResolvedKst.HasValue && InMonth(r.ResolvedKst.Value, start, end))
          .ToList();

      var resolutionHours = resolvedInMonth
          .Select(r => (DateTime.SpecifyKind(r.ResolvedAt!.Value, DateTimeKind.Utc)
                        - DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc)).TotalHours)
          .Where(h => h >= 0)
          .ToList();

      // 그 달 말 기준 적체 — 그때까지 들어온 것 중 아직 안 닫혀 있던 것.
      //
      // 닫힌 시각을 모르는 줄(시각 칸이 없는 옛 반려)은 **이미 닫힌 것으로 센다.**
      // 반대로 두면 그런 줄이 마지막 달까지 적체에 남아 곡선이 영영 안 내려온다.
      var backlog = rows.Count(r =>
          DateOnly.FromDateTime(r.RequestedKst) < end
          && (r.IsOpen || (r.ClosedKst.HasValue && DateOnly.FromDateTime(r.ClosedKst.Value) >= end)));

      result.Add(new DashboardMonthDto {
        Month = $"{start.Year:D4}-{start.Month:D2}",
        Label = start.Year == today.Year ? $"{start.Month}월" : $"{start.Year % 100:D2}/{start.Month}월",
        Requested = requested.Count,
        Completed = resolvedInMonth.Count,
        Rejected = requested.Count(r => r.Status == ImprovementStatus.Rejected),
        Backlog = backlog,
        AvgResolutionHours = Round(resolutionHours.Count > 0 ? resolutionHours.Average() : 0),
      });
    }

    return result;
  }

  /// <summary>일별 추이. 월별과 같은 이유로 빈 날도 채운다.</summary>
  private static List<DashboardDayDto> DailySeries(List<Row> rows, DateOnly today, int days) {
    var requestedByDay = rows
        .GroupBy(r => DateOnly.FromDateTime(r.RequestedKst))
        .ToDictionary(g => g.Key, g => g.Count());

    var resolvedByDay = rows
        .Where(r => r.ResolvedKst.HasValue)
        .GroupBy(r => DateOnly.FromDateTime(r.ResolvedKst!.Value))
        .ToDictionary(g => g.Key, g => g.Count());

    var result = new List<DashboardDayDto>(days);

    for (var i = days - 1; i >= 0; i--) {
      var date = today.AddDays(-i);
      var weekday = ((int)date.DayOfWeek + 6) % 7;

      result.Add(new DashboardDayDto {
        Date = date.ToString("yyyy-MM-dd"),
        Label = date.ToString("MM/dd"),
        Weekday = WeekdayNames[weekday],
        IsWeekend = weekday >= 5,
        Requested = requestedByDay.GetValueOrDefault(date),
        Completed = resolvedByDay.GetValueOrDefault(date),
      });
    }

    return result;
  }

  /// <summary>요일별 접수. 월요일부터 일곱 칸 고정이다.</summary>
  private static List<DashboardSliceDto> WeekdaySlices(List<Row> rows) =>
      [.. Enumerable.Range(0, 7).Select(i => {
        var count = rows.Count(r => ((int)r.RequestedKst.DayOfWeek + 6) % 7 == i);
        return new DashboardSliceDto {
          Key = i.ToString(),
          Label = WeekdayNames[i],
          Count = count,
          Share = Percent(count, rows.Count),
        };
      })];

  /// <summary>시간대별 접수. 0~23 스물네 칸 고정이다.</summary>
  private static List<DashboardSliceDto> HourlySlices(List<Row> rows) =>
      [.. Enumerable.Range(0, 24).Select(h => {
        var count = rows.Count(r => r.RequestedKst.Hour == h);
        return new DashboardSliceDto {
          Key = h.ToString(),
          Label = $"{h}시",
          Count = count,
          Share = Percent(count, rows.Count),
        };
      })];

  /// <summary>고객사별 집계. 건수 많은 회사부터.</summary>
  private static List<DashboardCompanyDto> CompanyRows(
      List<Row> rows, IReadOnlyDictionary<string, string> names, DateOnly monthStart) =>
      [.. rows
          .GroupBy(r => r.CompanyId ?? UnknownCompanyId)
          .Select(g => {
            var list = g.ToList();
            var rejected = list.Count(r => r.Status == ImprovementStatus.Rejected);
            var completed = list.Count(r => r.IsResolved);
            var pending = list.Where(r => r.Status == ImprovementStatus.Pending).ToList();

            return new DashboardCompanyDto {
              CompanyId = g.Key,
              CompanyName = CompanyName(names, g.Key),
              Total = list.Count,
              Pending = pending.Count,
              InProgress = list.Count(r => r.Status == ImprovementStatus.InProgress),
              Talking = list.Count(r => r.Status is ImprovementStatus.Consultation or ImprovementStatus.Negotiation),
              Completed = completed,
              Rejected = rejected,
              Open = list.Count(r => r.IsOpen),
              CompletionRate = Percent(completed, list.Count - rejected),
              AvgResponseHours = Round(AverageResponseHours(list)),
              AvgResolutionHours = Round(AverageResolutionHours(list)),
              ThisMonth = list.Count(r => DateOnly.FromDateTime(r.RequestedKst) >= monthStart),
              EmergencyOpen = list.Count(r => r.IsOpen && r.Type == ImprovementType.Emergency),
              OldestPendingDays = Round(pending.Count > 0 ? pending.Max(r => r.AgeDays) : 0),
              LastRequestedAt = list.Max(r => r.RequestedAt),
              Requesters = list.Select(r => r.CustomerId).Distinct().Count(),
            };
          })
          .OrderByDescending(c => c.Total)
          .ThenBy(c => c.CompanyName, StringComparer.Ordinal)];

  /// <summary>담당자별 집계. 한 건도 안 맡은 담당자도 0 으로 한 줄 선다.</summary>
  private static List<DashboardAdminDto> AdminRows(
      List<Row> rows,
      List<(int Id, string UserName, string? Photo)> admins,
      DateOnly monthStart,
      int totalCompleted) {
    var byAdmin = rows
        .Where(r => r.AdminId.HasValue)
        .GroupBy(r => r.AdminId!.Value)
        .ToDictionary(g => g.Key, g => g.ToList());

    // 담당자 표에서 지워졌는데 요청에는 남아 있는 번호가 있을 수 있다.
    // 그런 줄까지 세려면 두 쪽을 합쳐야 한다 — 안 합치면 합계가 안 맞는다.
    var ids = admins.Select(a => a.Id).Union(byAdmin.Keys).Distinct();

    return [.. ids
        .Select(id => {
          var list = byAdmin.GetValueOrDefault(id, []);
          var rejected = list.Count(r => r.Status == ImprovementStatus.Rejected);
          var completed = list.Count(r => r.IsResolved);
          var who = admins.FirstOrDefault(a => a.Id == id);

          return new DashboardAdminDto {
            AdminId = id,
            AdminName = who.UserName ?? list.FirstOrDefault()?.AdminName ?? $"#{id}",
            Photo = who.Photo,
            Assigned = list.Count,
            InProgress = list.Count(r => r.Status == ImprovementStatus.InProgress),
            Talking = list.Count(r => r.Status is ImprovementStatus.Consultation or ImprovementStatus.Negotiation),
            Completed = completed,
            Rejected = rejected,
            Open = list.Count(r => r.IsOpen),
            CompletionRate = Percent(completed, list.Count - rejected),
            AvgResolutionHours = Round(AverageResolutionHours(list)),
            CompletedThisMonth = list.Count(r =>
                r.ResolvedKst.HasValue && DateOnly.FromDateTime(r.ResolvedKst.Value) >= monthStart),
            Share = Percent(completed, totalCompleted),
            LastCompletedAt = list.Where(r => r.ResolvedAt.HasValue).Max(r => r.ResolvedAt),
          };
        })
        .OrderByDescending(a => a.Completed)
        .ThenByDescending(a => a.Assigned)
        .ThenBy(a => a.AdminName, StringComparer.Ordinal)];
  }

  /// <summary>안 닫힌 건이 얼마나 묵었나. 구간은 <see cref="AgingBuckets"/>.</summary>
  private static List<DashboardSliceDto> AgingSlices(List<Row> open) {
    var result = new List<DashboardSliceDto>(AgingBuckets.Length);
    var lower = 0d;

    foreach (var (upper, label) in AgingBuckets) {
      var count = open.Count(r => r.AgeDays >= lower && r.AgeDays < upper);
      result.Add(new DashboardSliceDto {
        Key = label,
        Label = label,
        Count = count,
        Share = Percent(count, open.Count),
      });
      lower = upper;
    }

    return result;
  }

  /// <summary>요청을 많이 올린 사람들.</summary>
  private static List<DashboardCustomerDto> CustomerRows(
      List<Row> rows, IReadOnlyDictionary<string, string> names, int topN) =>
      [.. rows
          .GroupBy(r => r.CustomerId)
          .Select(g => new DashboardCustomerDto {
            CustomerId = g.Key,
            UserName = g.Select(r => r.CustomerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"#{g.Key}",
            CompanyName = CompanyName(names, g.Select(r => r.CompanyId).FirstOrDefault(c => c is not null) ?? UnknownCompanyId),
            Total = g.Count(),
            Open = g.Count(r => r.IsOpen),
            Completed = g.Count(r => r.IsResolved),
            LastRequestedAt = g.Max(r => r.RequestedAt),
          })
          .OrderByDescending(c => c.Total)
          .ThenByDescending(c => c.LastRequestedAt)
          .Take(topN)];

  private static DashboardRequestDto ToRequestDto(Row r, IReadOnlyDictionary<string, string> names) => new() {
    Id = r.Id,
    Title = r.Title,
    Status = r.Status.ToString(),
    StatusName = r.Status.GetDisplayName(),
    TypeName = r.Type.GetDisplayName(),
    IsEmergency = r.Type == ImprovementType.Emergency,
    CompanyName = CompanyName(names, r.CompanyId ?? UnknownCompanyId),
    CustomerName = string.IsNullOrWhiteSpace(r.CustomerName) ? $"#{r.CustomerId}" : r.CustomerName!,
    AdminName = r.AdminName,
    RequestedAt = r.RequestedAt,
    AcceptedAt = r.AcceptedAt,
    AgeDays = Round(r.AgeDays),
    Comments = r.Comments,
  };

  // ── 잔셈 ────────────────────────────────────────────────────────

  /// <summary>이름을 못 찾으면 아이디를 그대로 준다. 빈 칸보다는 낫다.</summary>
  private static string CompanyName(IReadOnlyDictionary<string, string> names, string id) =>
      names.GetValueOrDefault(id, id);

  private static bool InMonth(DateTime kst, DateOnly start, DateOnly end) {
    var d = DateOnly.FromDateTime(kst);
    return d >= start && d < end;
  }

  private static double AverageResponseHours(List<Row> list) {
    var hours = list
        .Where(r => r.AcceptedAt.HasValue)
        .Select(r => (DateTime.SpecifyKind(r.AcceptedAt!.Value, DateTimeKind.Utc)
                      - DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc)).TotalHours)
        .Where(h => h >= 0)
        .ToList();
    return hours.Count > 0 ? hours.Average() : 0;
  }

  private static double AverageResolutionHours(List<Row> list) {
    var hours = list
        .Where(r => r.ResolvedAt.HasValue)
        .Select(r => (DateTime.SpecifyKind(r.ResolvedAt!.Value, DateTimeKind.Utc)
                      - DateTime.SpecifyKind(r.RequestedAt, DateTimeKind.Utc)).TotalHours)
        .Where(h => h >= 0)
        .ToList();
    return hours.Count > 0 ? hours.Average() : 0;
  }

  private static double Median(List<double> values) {
    if (values.Count == 0) {
      return 0;
    }

    var sorted = values.Order().ToList();
    var mid = sorted.Count / 2;
    return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
  }

  /// <summary>모수가 0 이면 0 이다 — 「0 으로 나누어 100%」가 되는 것을 막는다.</summary>
  private static double Percent(int part, int whole) =>
      whole > 0 ? Math.Round((double)part / whole * 100, 1) : 0;

  private static double Round(double value) => Math.Round(value, 1);

  /// <summary>
  /// 한 번 읽어 온 요청 한 줄. DB 칸과 셈에 쓰는 파생 값을 함께 든다.
  /// </summary>
  private sealed class Row {
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public ImprovementStatus Status { get; init; }
    public ImprovementType Type { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? UserCompletedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public int? AdminId { get; init; }
    public string? AdminName { get; init; }
    public int CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CompanyId { get; init; }

    /// <summary>달린 댓글 수. 읽어 온 뒤에 채운다.</summary>
    public int Comments { get; set; }

    /// <summary>접수 시각을 KST 벽시계로.</summary>
    public DateTime RequestedKst { get; set; }

    /// <summary>완료·종료 시각을 KST 벽시계로. 아직 안 끝났으면 null.</summary>
    public DateTime? ResolvedKst { get; set; }

    /// <summary>닫힌 시각(반려 포함)을 KST 벽시계로.</summary>
    public DateTime? ClosedKst { get; set; }

    /// <summary>지금까지(닫혔으면 닫힐 때까지) 흐른 날수.</summary>
    public double AgeDays { get; set; }

    /// <summary>완료·종료된 것인가. 반려는 「해결」이 아니다.</summary>
    public bool IsResolved =>
        Status is ImprovementStatus.Completed or ImprovementStatus.UserCompleted;

    /// <summary>아직 닫히지 않았는가.</summary>
    public bool IsOpen =>
        Status is ImprovementStatus.Pending or ImprovementStatus.InProgress
            or ImprovementStatus.Consultation or ImprovementStatus.Negotiation;

    /// <summary>
    /// <b>해결된 시각</b> — 완료 칸 둘 중 있는 쪽. 반려는 여기 들어오지 않는다
    /// (반려는 해결이 아니라서 처리 시간 평균에 섞이면 안 된다).
    /// </summary>
    public DateTime? ResolvedAt =>
        IsResolved ? CompletedAt ?? UserCompletedAt : null;

    /// <summary>
    /// <b>닫힌 시각</b> — 해결이든 반려든 손을 뗀 때. 반려에는 시각 칸이 없어
    /// 마지막 수정 시각으로 대신하고, 그마저 없으면 null 이다(추측하지 않는다).
    /// 경과 일수와 적체 곡선이 이 값을 본다.
    /// </summary>
    public DateTime? ClosedAt =>
        Status == ImprovementStatus.Rejected ? ModifiedAt : ResolvedAt;
  }
}
