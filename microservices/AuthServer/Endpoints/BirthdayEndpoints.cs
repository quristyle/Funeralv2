using System.Globalization;
using AuthServer.Data;
using AuthServer.Entities;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Endpoints;

/// <summary>
/// 생일 엔드포인트 — GHUB(생활과환경)에서 이식 (A안: 생일 정본·API 모두 포털).
/// </summary>
/// <remarks>
/// 생일의 정본은 scom.accounts 다 (birth_date · birth_date_is_lunar · birthday_celebrated).
/// 입력·수정은 계정 관리(/system/account/*)가 하고, 여기서는 조회와 축하 메시지만 다룬다.
///
/// 원본(GhubServer BirthdayEndpoints)과 다른 점:
/// - 명단이 별도 표(ghub.birthday_profiles)가 아니라 포털 계정이다.
///   이름은 RealName ?? UserName, 소속은 CompanyId/DepartmentId 를
///   Companies·Departments 와 조인해 얻는다 (같은 DbContext 라 DB 조인).
/// - 필터가 companyId(그 회사 전체) · departmentId(그 부서) 둘이다.
///   departmentId 가 있으면 companyId 는 무시한다.
/// - 메시지는 scom.birthday_messages (BirthdayMessage) 에 저장한다.
///
/// 게이트웨이의 `/api/auth/**` 는 Anonymous 라 인증을 걸지 않는다 —
/// **모든 엔드포인트는 UserContext 가 없으면 401 을 돌려준다.**
/// "오늘"/"이번 달"/"올해" 판정은 전부 KST(<see cref="Kst"/>) 기준이고, 저장 시각은 UTC 다.
///
/// 알림 발송(웹푸시 · 이메일 · 카카오 알림톡)은 이식하지 않았다 —
/// NotificationServer 연동이 결정 대기다 (docs/analysis/38-ghub-migration.md D-G1).
/// </remarks>
public static class BirthdayEndpoints
{
    public static void MapBirthdayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/birthday").WithTags("Birthday");

        // ── 조회 ───────────────────────────────────────────────

        // 캘린더용 생일 이벤트 (FullCalendar 형)
        group.MapGet("/calendar", async (UserContext? user, [FromServices] AppDbContext db,
            [FromQuery] string? start, [FromQuery] string? end,
            [FromQuery] string? companyId, [FromQuery] string? departmentId) =>
        {
            if (user is null) return Results.Unauthorized();

            // 형식이 어긋난 값('+' 가 공백으로 오는 등)은 오늘로 대신한다 (원본과 같다)
            var now = Kst.Now;
            if (!DateTime.TryParse(start, out var startDate)) startDate = now;
            if (!DateTime.TryParse(end, out var endDate)) endDate = now;

            var rows = await LoadBirthdayRowsAsync(db, companyId, departmentId);

            var events = new List<BirthdayCalendarEvent>();
            var koreanCal = new KoreanLunisolarCalendar();

            foreach (var row in rows)
            {
                // 음력 생일은 해마다 양력 날짜가 달라진다. 범위 앞뒤 1년까지 훑어
                // 경계(연말·연초)에 걸친 발생일을 놓치지 않는다.
                for (int year = startDate.Year - 1; year <= endDate.Year + 1; year++)
                {
                    DateTime? occurrence = null;

                    if (row.IsLunar)
                    {
                        try
                        {
                            // 그 해 음력에 실제로 있는 날인지 확인 (윤달 · 작은 달)
                            int daysInMonth = koreanCal.GetDaysInMonth(year, row.BirthDate.Month);
                            if (row.BirthDate.Day <= daysInMonth)
                            {
                                occurrence = koreanCal.ToDateTime(
                                    year, row.BirthDate.Month, row.BirthDate.Day, 0, 0, 0, 0);
                            }
                        }
                        catch
                        {
                            // 그 해로 변환할 수 없는 날짜는 건너뛴다
                            continue;
                        }
                    }
                    else
                    {
                        try
                        {
                            occurrence = new DateTime(year, row.BirthDate.Month, row.BirthDate.Day);
                        }
                        catch
                        {
                            // 평년의 2/29 등
                            continue;
                        }
                    }

                    if (occurrence.HasValue
                        && occurrence.Value.Date >= startDate.Date
                        && occurrence.Value.Date <= endDate.Date)
                    {
                        events.Add(new BirthdayCalendarEvent(
                            Id: $"{row.UserId}_{occurrence.Value:yyyyMMdd}",
                            Title: $"{row.Name} 생일",
                            Start: occurrence.Value.ToString("yyyy-MM-dd"),
                            AllDay: true,
                            ExtendedProps: new BirthdayCalendarEventProps(
                                Type: "birthday",
                                IsLunar: row.IsLunar,
                                UserId: row.UserId,
                                OriginalBirthDate: row.BirthDate.ToString("yyyy-MM-dd")),
                            BackgroundColor: "#FF6B6B",
                            BorderColor: "#FF6B6B"));
                    }
                }
            }

            return Results.Ok(ApiResponse<List<BirthdayCalendarEvent>>.Ok(events));
        })
        .WithName("GetBirthdayCalendarEvents")
        .WithOpenApi();

        // 월별 생일자 수 (올해 기준, 음력은 양력으로 환산해 집계)
        group.MapGet("/stats", async (UserContext? user, [FromServices] AppDbContext db,
            [FromQuery] string? companyId, [FromQuery] string? departmentId) =>
        {
            if (user is null) return Results.Unauthorized();

            var rows = await LoadBirthdayRowsAsync(db, companyId, departmentId);

            var stats = Enumerable.Range(1, 12)
                .Select(m => new BirthdayMonthStat(m, 0, 0, 0))
                .ToArray();

            var koreanCal = new KoreanLunisolarCalendar();
            int currentYear = Kst.Now.Year;

            foreach (var row in rows)
            {
                try
                {
                    // 윤달 · 말일은 ToOccurrence 가 보정한다
                    int month = ToOccurrence(koreanCal, currentYear, row.BirthDate, row.IsLunar).Month;
                    if (month is >= 1 and <= 12)
                    {
                        var current = stats[month - 1];
                        stats[month - 1] = current with
                        {
                            Total = current.Total + 1,
                            Solar = current.Solar + (row.IsLunar ? 0 : 1),
                            Lunar = current.Lunar + (row.IsLunar ? 1 : 0),
                        };
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Results.Ok(ApiResponse<BirthdayMonthStat[]>.Ok(stats));
        })
        .WithName("GetBirthdayStats")
        .WithOpenApi();

        // 월별 생일자 목록 (올해 기준, 음력은 양력으로 환산)
        group.MapGet("/list", async (UserContext? user, [FromServices] AppDbContext db,
            [FromQuery] int month,
            [FromQuery] string? companyId, [FromQuery] string? departmentId) =>
        {
            if (user is null) return Results.Unauthorized();

            var items = await BuildMonthListAsync(db, month, companyId, departmentId);
            return Results.Ok(ApiResponse<List<BirthdayListItem>>.Ok(items));
        })
        .WithName("GetBirthdayListByMonth")
        .WithOpenApi();

        // 이번 달(KST) 생일자 목록
        group.MapGet("/current", async (UserContext? user, [FromServices] AppDbContext db,
            [FromQuery] string? companyId, [FromQuery] string? departmentId) =>
        {
            if (user is null) return Results.Unauthorized();

            var items = await BuildMonthListAsync(db, Kst.Now.Month, companyId, departmentId);
            return Results.Ok(ApiResponse<List<BirthdayListItem>>.Ok(items));
        })
        .WithName("GetCurrentMonthBirthdays")
        .WithOpenApi();

        // 오늘(KST)의 생일자 목록 — 올해 받은 축하 메시지 수를 함께 준다
        group.MapGet("/today", async (UserContext? user, [FromServices] AppDbContext db,
            [FromQuery] string? companyId, [FromQuery] string? departmentId) =>
        {
            if (user is null) return Results.Unauthorized();

            var today = Kst.Now.Date;
            int currentYear = today.Year;

            // 메시지 수는 올해분만 센다 (KST 기준 올해 1월 1일 이후 · 삭제 제외)
            var yearStartUtc = Kst.ToUtc(new DateTime(currentYear, 1, 1));

            var rows = await FilterBirthdayAccounts(db, companyId, departmentId)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    Name = a.RealName ?? a.UserName ?? a.UserId,
                    BirthDate = a.BirthDate!.Value,
                    IsLunar = a.BirthDateIsLunar,
                    IsCelebrated = a.BirthdayCelebrated,
                    a.CompanyId,
                    CompanyName = a.Company != null ? a.Company.Name : null,
                    a.DepartmentId,
                    DepartmentName = a.Department != null ? a.Department.Name : null,
                    MessageCount = db.BirthdayMessages.Count(m =>
                        m.RecipientId == a.UserId && !m.IsDeleted && m.CreatedAt >= yearStartUtc),
                })
                .ToListAsync();

            var result = new List<BirthdayTodayItem>();
            var koreanCal = new KoreanLunisolarCalendar();

            foreach (var row in rows)
            {
                try
                {
                    var occurrence = ToOccurrence(koreanCal, currentYear, row.BirthDate, row.IsLunar);
                    if (occurrence.Date == today)
                    {
                        result.Add(new BirthdayTodayItem(
                            row.Id, row.UserId, row.Name, row.BirthDate,
                            DateOnly.FromDateTime(occurrence), row.IsLunar, row.IsCelebrated,
                            row.CompanyId, row.CompanyName, row.DepartmentId, row.DepartmentName,
                            row.MessageCount));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Results.Ok(ApiResponse<List<BirthdayTodayItem>>.Ok(result));
        })
        .WithName("GetTodayBirthdays")
        .WithOpenApi();

        // ── 축하 메시지 ────────────────────────────────────────

        // 축하 메시지 전송 — 저장하고, 받는 사람에게 웹푸시로 알린다 (D-G1a · 2026-09-04).
        // 발송은 NotificationServer 가 맡고(사용자 결정), 실패해도 저장은 이미 끝났다.
        group.MapPost("/message", async (UserContext? user, [FromServices] AppDbContext db,
            [FromServices] AuthServer.Services.BirthdayNotifyClient notify,
            [FromBody] SendBirthdayMessageDto request) =>
        {
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.RecipientId) || string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("받는 사람과 내용을 입력하세요.", "400"));
            }

            var message = new BirthdayMessage
            {
                RecipientId = request.RecipientId,
                SenderId = user.UserId,
                Content = request.Content.Trim(),
                CreatedBy = user.UserId,
            };

            db.BirthdayMessages.Add(message);
            await db.SaveChangesAsync();

            // 보낸 사람 표시 이름 — 없으면 클라이언트가 아이디로 대신한다.
            var senderName = await db.Accounts
                .Where(a => a.UserId == user.UserId && !a.IsDeleted)
                .Select(a => a.RealName ?? a.UserName)
                .FirstOrDefaultAsync() ?? string.Empty;

            await notify.NotifyAsync(request.RecipientId, user.UserId, senderName);

            return Results.Ok(ApiResponse<object>.Ok(null, "축하 메시지를 보냈습니다."));
        })
        .WithName("SendBirthdayMessage")
        .WithOpenApi();

        // 오늘(KST)의 생일자들이 올해 받은 축하 메시지 목록
        group.MapGet("/today/messages", async (UserContext? user, [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            var today = Kst.Now.Date;
            int currentYear = today.Year;

            // 1. 오늘 생일자의 user_id 목록
            var rows = await LoadBirthdayRowsAsync(db, companyId: null, departmentId: null);
            var koreanCal = new KoreanLunisolarCalendar();
            var todayRecipientIds = new List<string>();

            foreach (var row in rows)
            {
                try
                {
                    if (ToOccurrence(koreanCal, currentYear, row.BirthDate, row.IsLunar).Date == today)
                    {
                        todayRecipientIds.Add(row.UserId);
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (todayRecipientIds.Count == 0)
            {
                return Results.Ok(ApiResponse<List<BirthdayTodayMessageItem>>.Ok(new List<BirthdayTodayMessageItem>()));
            }

            // 2. 올해 받은 메시지 — 연도 경계는 KST 기준이다. UTC 자정으로 자르면
            //    1월 1일 0시~9시(KST) 메시지가 작년으로 밀린다.
            var yearStartUtc = Kst.ToUtc(new DateTime(currentYear, 1, 1));

            var messages = await db.BirthdayMessages.AsNoTracking()
                .Where(m => todayRecipientIds.Contains(m.RecipientId)
                            && m.CreatedAt >= yearStartUtc
                            && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Content, m.CreatedAt, m.SenderId, m.RecipientId })
                .ToListAsync();

            // 3. 이름 · 부서는 계정 딕셔너리로 조인
            var names = await LoadAccountNamesAsync(db,
                messages.Select(m => m.SenderId)
                    .Concat(messages.Select(m => m.RecipientId))
                    .Distinct()
                    .ToList());

            var result = messages.Select(m => new BirthdayTodayMessageItem(
                m.Id,
                m.Content,
                m.CreatedAt,
                names.TryGetValue(m.SenderId, out var s) ? s.Name : m.SenderId,
                names.TryGetValue(m.SenderId, out var s2) ? s2.DepartmentName : null,
                names.TryGetValue(m.RecipientId, out var r) ? r.Name : m.RecipientId,
                m.RecipientId)).ToList();

            return Results.Ok(ApiResponse<List<BirthdayTodayMessageItem>>.Ok(result));
        })
        .WithName("GetTodayBirthdayMessages")
        .WithOpenApi();

        // 내가 받은 축하 메시지
        group.MapGet("/message", async (UserContext? user, [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            var messages = await db.BirthdayMessages.AsNoTracking()
                .Where(m => m.RecipientId == user.UserId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Content, m.CreatedAt, m.SenderId })
                .ToListAsync();

            var names = await LoadAccountNamesAsync(db,
                messages.Select(m => m.SenderId).Distinct().ToList());

            var result = messages.Select(m => new BirthdayReceivedMessageItem(
                m.Id,
                m.Content,
                m.CreatedAt,
                names.TryGetValue(m.SenderId, out var s) ? s.Name : m.SenderId,
                names.TryGetValue(m.SenderId, out var s2) ? s2.DepartmentName : null)).ToList();

            return Results.Ok(ApiResponse<List<BirthdayReceivedMessageItem>>.Ok(result));
        })
        .WithName("GetMyBirthdayMessages")
        .WithOpenApi();

        // 내가 보낸 축하 메시지
        group.MapGet("/message/sent", async (UserContext? user, [FromServices] AppDbContext db) =>
        {
            if (user is null) return Results.Unauthorized();

            var messages = await db.BirthdayMessages.AsNoTracking()
                .Where(m => m.SenderId == user.UserId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Content, m.CreatedAt, m.RecipientId })
                .ToListAsync();

            var names = await LoadAccountNamesAsync(db,
                messages.Select(m => m.RecipientId).Distinct().ToList());

            var result = messages.Select(m => new BirthdaySentMessageItem(
                m.Id,
                m.Content,
                m.CreatedAt,
                names.TryGetValue(m.RecipientId, out var r) ? r.Name : m.RecipientId,
                names.TryGetValue(m.RecipientId, out var r2) ? r2.DepartmentName : null)).ToList();

            return Results.Ok(ApiResponse<List<BirthdaySentMessageItem>>.Ok(result));
        })
        .WithName("GetMySentBirthdayMessages")
        .WithOpenApi();
    }

    // ── 내부 도우미 ──────────────────────────────────────────

    /// <summary>
    /// 생일 대상 계정 쿼리. 삭제되지 않았고 생일이 입력된 계정이 대상이다.
    /// departmentId 가 있으면 그 부서만, 없이 companyId 만 있으면 그 회사 전체다.
    /// </summary>
    private static IQueryable<Account> FilterBirthdayAccounts(
        AppDbContext db, string? companyId, string? departmentId)
    {
        var query = db.Accounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.BirthDate != null);

        if (!string.IsNullOrEmpty(departmentId))
        {
            query = query.Where(a => a.DepartmentId == departmentId);
        }
        else if (!string.IsNullOrEmpty(companyId))
        {
            query = query.Where(a => a.CompanyId == companyId);
        }

        return query;
    }

    /// <summary>목록류 엔드포인트가 공통으로 쓰는 생일 대상 조회 (소속 이름 조인 포함).</summary>
    private static async Task<List<BirthdayAccountRow>> LoadBirthdayRowsAsync(
        AppDbContext db, string? companyId, string? departmentId)
    {
        return await FilterBirthdayAccounts(db, companyId, departmentId)
            .Select(a => new BirthdayAccountRow(
                a.Id,
                a.UserId,
                a.RealName ?? a.UserName ?? a.UserId,
                a.BirthDate!.Value,
                a.BirthDateIsLunar,
                a.BirthdayCelebrated,
                a.CompanyId,
                a.Company != null ? a.Company.Name : null,
                a.DepartmentId,
                a.Department != null ? a.Department.Name : null))
            .ToListAsync();
    }

    /// <summary>지정한 달(올해 기준)의 생일자 목록. /list 와 /current 가 함께 쓴다.</summary>
    private static async Task<List<BirthdayListItem>> BuildMonthListAsync(
        AppDbContext db, int month, string? companyId, string? departmentId)
    {
        var rows = await LoadBirthdayRowsAsync(db, companyId, departmentId);

        var result = new List<BirthdayListItem>();
        var koreanCal = new KoreanLunisolarCalendar();
        int currentYear = Kst.Now.Year;

        foreach (var row in rows)
        {
            try
            {
                var occurrence = ToOccurrence(koreanCal, currentYear, row.BirthDate, row.IsLunar);
                if (occurrence.Month == month)
                {
                    result.Add(new BirthdayListItem(
                        row.Id, row.UserId, row.Name, row.BirthDate,
                        DateOnly.FromDateTime(occurrence), row.IsLunar, row.IsCelebrated,
                        row.CompanyId, row.CompanyName, row.DepartmentId, row.DepartmentName));
                }
            }
            catch
            {
                continue;
            }
        }

        return result.OrderBy(x => x.OccurrenceDate).ToList();
    }

    /// <summary>
    /// 이름 · 부서 조인용 딕셔너리 (user_id → 이름/부서).
    /// user_id 에 유니크 제약이 없어 중복이 있을 수 있다 — 첫 행을 쓴다.
    /// </summary>
    private static async Task<Dictionary<string, BirthdayPersonName>> LoadAccountNamesAsync(
        AppDbContext db, List<string> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<string, BirthdayPersonName>();

        var rows = await db.Accounts.AsNoTracking()
            .Where(a => !a.IsDeleted && userIds.Contains(a.UserId))
            .Select(a => new
            {
                a.UserId,
                Name = a.RealName ?? a.UserName ?? a.UserId,
                DepartmentName = a.Department != null ? a.Department.Name : null,
            })
            .ToListAsync();

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => new BirthdayPersonName(g.First().Name, g.First().DepartmentName));
    }

    /// <summary>
    /// 저장된 생일(음력이면 음력 월·일로 해석)을 해당 연도의 실제 발생일로 변환한다.
    /// 음력 변환 로직은 원본 그대로다 — 그 해에 없는 날(윤달 · 작은 달)은 말일로 당기고,
    /// 양력 2/29 는 평년이면 2/28 로 대체한다.
    /// </summary>
    private static DateTime ToOccurrence(
        KoreanLunisolarCalendar koreanCal, int year, DateOnly birthDate, bool isLunar)
    {
        if (isLunar)
        {
            int lunarMonth = birthDate.Month;
            int lunarDay = birthDate.Day;

            int daysInMonth = koreanCal.GetDaysInMonth(year, lunarMonth);
            if (lunarDay > daysInMonth) lunarDay = daysInMonth;

            return koreanCal.ToDateTime(year, lunarMonth, lunarDay, 0, 0, 0, 0);
        }

        try
        {
            return new DateTime(year, birthDate.Month, birthDate.Day);
        }
        catch
        {
            return new DateTime(year, 2, 28);
        }
    }

    /// <summary>
    /// 한국 표준시 도우미 (이 파일 전용).
    /// AuthServer 는 저장을 전부 UTC 로 하므로, "오늘"/"이번 달"/"올해" 같은
    /// 벽시계 판정에만 쓴다. 서버 TZ 에 의존하는 DateTime.Now 는 쓰지 않는다.
    /// </summary>
    private static class Kst
    {
        private static readonly TimeZoneInfo Zone = ResolveZone();

        private static TimeZoneInfo ResolveZone()
        {
            // 리눅스("Asia/Seoul")와 윈도우("Korea Standard Time")의 아이디가 다르다
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
        }

        /// <summary>지금(KST)</summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

        /// <summary>KST 벽시계 시각 → UTC (timestamptz 컬럼 비교용, Kind=Utc)</summary>
        public static DateTime ToUtc(DateTime kstWallClock)
        {
            var unspecified = DateTime.SpecifyKind(kstWallClock, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
        }
    }
}

// ── DTO · 응답 형 ───────────────────────────────────────────

/// <summary>생일 축하 메시지 전송 DTO</summary>
/// <param name="RecipientId">받는 이 (accounts.user_id)</param>
/// <param name="Content">메시지 내용</param>
public record SendBirthdayMessageDto(string RecipientId, string Content);

/// <summary>캘린더용 생일 이벤트 (FullCalendar 형)</summary>
public record BirthdayCalendarEvent(
    string Id,
    string Title,
    string Start,
    bool AllDay,
    BirthdayCalendarEventProps ExtendedProps,
    string BackgroundColor,
    string BorderColor);

/// <summary>캘린더 이벤트의 extendedProps</summary>
public record BirthdayCalendarEventProps(
    string Type,
    bool IsLunar,
    string UserId,
    string OriginalBirthDate);

/// <summary>월별 생일자 수 (stats 응답 항목)</summary>
public record BirthdayMonthStat(int Month, int Total, int Solar, int Lunar);

/// <summary>월별 생일자 목록 항목 (list · current 응답)</summary>
/// <param name="Id">계정 id (scom.accounts.id, GUID)</param>
/// <param name="SubjectId">계정 user_id (로그인 아이디)</param>
/// <param name="Name">이름 (RealName ?? UserName)</param>
/// <param name="BirthDate">저장된 생일 (음력이면 음력 월·일)</param>
/// <param name="OccurrenceDate">올해의 실제 발생일 (양력)</param>
/// <param name="IsLunar">음력 여부</param>
/// <param name="IsCelebrated">생일 축하 대상 여부</param>
/// <param name="CompanyId">소속 회사 id</param>
/// <param name="CompanyName">소속 회사 이름</param>
/// <param name="DepartmentId">소속 부서 id</param>
/// <param name="DepartmentName">소속 부서 이름</param>
public record BirthdayListItem(
    string Id,
    string SubjectId,
    string Name,
    DateOnly BirthDate,
    DateOnly OccurrenceDate,
    bool IsLunar,
    bool IsCelebrated,
    string? CompanyId,
    string? CompanyName,
    string? DepartmentId,
    string? DepartmentName);

/// <summary>오늘의 생일자 항목 (today 응답 — 올해 받은 메시지 수 포함)</summary>
public record BirthdayTodayItem(
    string Id,
    string SubjectId,
    string Name,
    DateOnly BirthDate,
    DateOnly OccurrenceDate,
    bool IsLunar,
    bool IsCelebrated,
    string? CompanyId,
    string? CompanyName,
    string? DepartmentId,
    string? DepartmentName,
    int MessageCount);

/// <summary>오늘의 생일자들이 올해 받은 메시지 항목 (today/messages 응답)</summary>
public record BirthdayTodayMessageItem(
    int Id,
    string Content,
    DateTime CreatedAt,
    string SenderName,
    string? SenderDepartment,
    string RecipientName,
    string RecipientId);

/// <summary>내가 받은 메시지 항목 (message 응답)</summary>
public record BirthdayReceivedMessageItem(
    int Id,
    string Content,
    DateTime CreatedAt,
    string SenderName,
    string? SenderDepartment);

/// <summary>내가 보낸 메시지 항목 (message/sent 응답)</summary>
public record BirthdaySentMessageItem(
    int Id,
    string Content,
    DateTime CreatedAt,
    string RecipientName,
    string? RecipientDepartment);

/// <summary>목록류 엔드포인트 공용 계정 투영 (BirthDate 는 non-null 확정)</summary>
internal record BirthdayAccountRow(
    string Id,
    string UserId,
    string Name,
    DateOnly BirthDate,
    bool IsLunar,
    bool IsCelebrated,
    string? CompanyId,
    string? CompanyName,
    string? DepartmentId,
    string? DepartmentName);

/// <summary>이름 · 부서 조인 결과</summary>
internal record BirthdayPersonName(string Name, string? DepartmentName);
