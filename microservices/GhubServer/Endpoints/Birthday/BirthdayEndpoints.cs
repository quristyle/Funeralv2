using System.Globalization;
using GhubServer.Data;
using GhubServer.Models;
using GhubServer.Utilities;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.EntityFrameworkCore;

namespace GhubServer.Endpoints;

/// <summary>
/// 생일자 관련 엔드포인트 — GHUB(skgRestApi Endpoints/Birthday)에서 이식.
///
/// 원본과 다른 점:
/// - 원본은 자체 사용자 테이블(UserProfiles)의 생일 컬럼을 읽었지만,
///   여기서는 생일 모듈 전용 테이블(ghub.birthday_profiles = BirthdayProfile)을 쓴다.
///   사용자 정본이 scom(AuthServer)이라 이 DB 안에 사용자 테이블이 없기 때문이다.
/// - 인증은 게이트웨이가 끝냈으므로 RequireAuthorization · 감사 필터를 쓰지 않는다.
///   호출자 식별은 X-User-* 헤더(UserContext)로 한다.
/// - "오늘"/"이번 달" 판정은 전부 KST(Kst.Now) 기준이다. 원본은 서버 TZ 에 의존하는
///   DateTime.Now/Today 를 썼다. 저장은 전부 UTC.
/// </summary>
public static class BirthdayEndpoints
{
    /// <summary>
    /// 생일자 엔드포인트를 매핑합니다.
    /// </summary>
    /// <param name="app">엔드포인트 라우트 빌더</param>
    public static void MapBirthdayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/birthday")
            .WithTags("Birthday")
            .AddApiResponseWrapper();

        // 캘린더용 생일자 목록 조회 (기간 지정)
        group.MapGet("/calendar", async (GhubDbContext db, string start, string end, string? companyCode = null) =>
        {
            // Parse start/end manually to handle potential format issues (e.g. space instead of +)
            var now = Kst.Now;
            if (!DateTime.TryParse(start, out var startDate)) startDate = now;
            if (!DateTime.TryParse(end, out var endDate)) endDate = now;

            var query = db.BirthdayProfiles.AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && u.BirthDate != null);

            if (!string.IsNullOrEmpty(companyCode))
            {
                query = query.Where(u => u.CompanyCode == companyCode);
            }

            var users = await query
                .Select(u => new
                {
                    u.Id,
                    u.UserId,
                    u.FullName,
                    BirthDate = u.BirthDate!.Value,
                    u.IsLunar,
                })
                .ToListAsync();

            var events = new List<object>();
            var koreanCal = new KoreanLunisolarCalendar();

            foreach (var user in users)
            {
                // Check years covered by the range (startDate.Year - 1 to endDate.Year + 1) to handle Lunar date shifts
                for (int year = startDate.Year - 1; year <= endDate.Year + 1; year++)
                {
                    DateTime? birthdayThisYear = null;

                    if (user.IsLunar)
                    {
                        try
                        {
                            // Verify valid date in Lunar Calendar for this year
                            int daysInMonth = koreanCal.GetDaysInMonth(year, user.BirthDate.Month);
                            if (user.BirthDate.Day <= daysInMonth)
                            {
                                birthdayThisYear = koreanCal.ToDateTime(year, user.BirthDate.Month, user.BirthDate.Day, 0, 0, 0, 0);
                            }
                        }
                        catch
                        {
                            // Invalid date conversion (e.g. leap month issues or invalid date for that year)
                            continue;
                        }
                    }
                    else
                    {
                        // Solar
                        try
                        {
                            birthdayThisYear = new DateTime(year, user.BirthDate.Month, user.BirthDate.Day);
                        }
                        catch
                        {
                            // e.g. Feb 29 on non-leap year
                            continue;
                        }
                    }

                    if (birthdayThisYear.HasValue)
                    {
                        // Check if the calculated date is within the requested range
                        if (birthdayThisYear.Value.Date >= startDate.Date && birthdayThisYear.Value.Date <= endDate.Date)
                        {
                            events.Add(new
                            {
                                id = $"{user.UserId}_{birthdayThisYear.Value:yyyyMMdd}",
                                title = $"{user.FullName} 생일",
                                start = birthdayThisYear.Value.ToString("yyyy-MM-dd"),
                                allDay = true,
                                extendedProps = new
                                {
                                    type = "birthday",
                                    isLunar = user.IsLunar,
                                    userId = user.UserId,
                                    dbId = user.Id,
                                    originalBirthDate = user.BirthDate.ToString("yyyy-MM-dd"),
                                },
                                backgroundColor = "#FF6B6B", // Reddish for birthdays
                                borderColor = "#FF6B6B",
                            });
                        }
                    }
                }
            }

            return Results.Ok(events);
        })
        .WithName("GetBirthdayCalendarEvents")
        .WithSummary("기간별 생일자 조회 (FullCalendar용)")
        .WithDescription("지정된 기간(start~end) 내의 생일자를 양력/음력을 고려하여 반환합니다.");

        // 월별 생일자 수 조회 (올해 기준, 음력 변환 적용)
        group.MapGet("/stats", async (GhubDbContext db, string? companyCode = null) =>
        {
            var query = db.BirthdayProfiles.AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && u.BirthDate != null);

            if (!string.IsNullOrEmpty(companyCode))
            {
                query = query.Where(u => u.CompanyCode == companyCode);
            }

            var users = await query
                .Select(u => new { u.BirthDate, u.IsLunar })
                .ToListAsync();

            var stats = Enumerable.Range(1, 12)
                .Select(m => new BirthdayMonthStat(m, 0, 0, 0))
                .ToArray();

            var koreanCal = new KoreanLunisolarCalendar();
            int currentYear = Kst.Now.Year;

            foreach (var user in users)
            {
                try
                {
                    int month;
                    if (user.IsLunar)
                    {
                        // Treat stored BirthDate as Lunar Month/Day
                        int lunarMonth = user.BirthDate!.Value.Month;
                        int lunarDay = user.BirthDate!.Value.Day;

                        // Validate lunar date for current year (handle leap/days in month)
                        int daysInMonth = koreanCal.GetDaysInMonth(currentYear, lunarMonth);
                        if (lunarDay > daysInMonth) lunarDay = daysInMonth;

                        var date = koreanCal.ToDateTime(currentYear, lunarMonth, lunarDay, 0, 0, 0, 0);
                        month = date.Month;
                    }
                    else
                    {
                        month = user.BirthDate!.Value.Month;
                    }

                    if (month >= 1 && month <= 12)
                    {
                        var mIdx = month - 1;
                        var current = stats[mIdx];
                        stats[mIdx] = current with
                        {
                            Total = current.Total + 1,
                            Solar = current.Solar + (user.IsLunar ? 0 : 1),
                            Lunar = current.Lunar + (user.IsLunar ? 1 : 0),
                        };
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Results.Ok(stats);
        })
        .WithName("GetBirthdayStats");

        // 월별 생일자 목록 조회 (올해 기준, 음력 변환 적용)
        group.MapGet("/list", async (GhubDbContext db, int month, string? companyCode = null) =>
        {
            var users = await LoadProfilesAsync(db, companyCode);

            var result = new List<BirthdayListItem>();
            var koreanCal = new KoreanLunisolarCalendar();
            int currentYear = Kst.Now.Year;

            foreach (var user in users)
            {
                try
                {
                    var occurrenceDate = ToOccurrence(koreanCal, currentYear, user.BirthDate, user.IsLunar);

                    if (occurrenceDate.Month == month)
                    {
                        result.Add(new BirthdayListItem(
                            user.Id, user.UserId, user.FullName, user.BirthDate,
                            DateOnly.FromDateTime(occurrenceDate), user.IsLunar, user.IsCelebrated,
                            user.CompanyCode, user.Department));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Results.Ok(result.OrderBy(x => x.OccurrenceDate));
        })
        .WithName("GetBirthdayListByMonth");

        // 현재 달의 모든 생일자 목록 (KST 기준)
        group.MapGet("/current", async (GhubDbContext db, string? companyCode = null) =>
        {
            var now = Kst.Now;
            int currentMonth = now.Month;
            int currentYear = now.Year;

            var users = await LoadProfilesAsync(db, companyCode);

            var result = new List<BirthdayListItem>();
            var koreanCal = new KoreanLunisolarCalendar();

            foreach (var user in users)
            {
                try
                {
                    var occurrenceDate = ToOccurrence(koreanCal, currentYear, user.BirthDate, user.IsLunar);

                    if (occurrenceDate.Month == currentMonth)
                    {
                        result.Add(new BirthdayListItem(
                            user.Id, user.UserId, user.FullName, user.BirthDate,
                            DateOnly.FromDateTime(occurrenceDate), user.IsLunar, user.IsCelebrated,
                            user.CompanyCode, user.Department));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Results.Ok(result.OrderBy(x => x.OccurrenceDate));
        })
        .WithName("GetCurrentMonthBirthdays");

        // 오늘의 모든 생일자 목록 (KST 기준)
        group.MapGet("/today", async (GhubDbContext db, string? companyCode = null) =>
        {
            var today = Kst.Now.Date;
            int currentYear = today.Year;

            // 메시지 수는 올해분만 센다 (KST 기준 올해 1월 1일 이후 · 삭제 제외).
            // 원본은 전체 누적에 IsDeleted 도 무시하는 버그가 있었다 — 여기서 고쳤다.
            var yearStartUtc = Kst.ToUtc(new DateTime(currentYear, 1, 1));

            var query = db.BirthdayProfiles.AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && u.BirthDate != null);
            if (!string.IsNullOrEmpty(companyCode)) query = query.Where(u => u.CompanyCode == companyCode);

            var users = await query.Select(u => new
            {
                u.Id,
                u.UserId,
                u.FullName,
                BirthDate = u.BirthDate!.Value,
                u.IsLunar,
                u.IsCelebrated,
                u.CompanyCode,
                u.Department,
                u.ThumbnailUrl,
                MessageCount = db.BirthdayMessages.Count(m =>
                    m.RecipientId == u.UserId && !m.IsDeleted && m.CreatedAt >= yearStartUtc),
            }).ToListAsync();

            var result = new List<BirthdayTodayItem>();
            var koreanCal = new KoreanLunisolarCalendar();

            foreach (var user in users)
            {
                try
                {
                    var occurrenceDate = ToOccurrence(koreanCal, currentYear, user.BirthDate, user.IsLunar);

                    if (occurrenceDate.Date == today)
                    {
                        result.Add(new BirthdayTodayItem(
                            user.Id, user.UserId, user.FullName, user.BirthDate,
                            DateOnly.FromDateTime(occurrenceDate), user.IsLunar, user.IsCelebrated,
                            user.CompanyCode, user.Department, user.ThumbnailUrl, user.MessageCount));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Results.Ok(result);
        })
        .WithName("GetTodayBirthdays");

        // 생일자 등록 — 원본은 기존 사용자 프로필이 없으면 404 였지만,
        // 여기는 생일 전용 테이블이므로 upsert 로 바꿨다: UserId 로 찾아 있으면 갱신, 없으면 신규.
        group.MapPost("/", async (GhubDbContext db, BirthdayEntryDto entry, UserContext? user) =>
        {
            var actor = user?.UserId ?? "system";
            var profile = await db.BirthdayProfiles.FirstOrDefaultAsync(u => u.UserId == entry.SubjectId);

            if (profile is null)
            {
                profile = new BirthdayProfile
                {
                    UserId = entry.SubjectId,
                    FullName = string.IsNullOrEmpty(entry.Name) ? entry.SubjectId : entry.Name,
                    IsActive = true,
                    CreatedBy = actor,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.BirthdayProfiles.Add(profile);
            }
            else
            {
                profile.ModifiedBy = actor;
                profile.ModifiedAt = DateTimeOffset.UtcNow;
            }

            profile.BirthDate = entry.BirthDate;
            profile.IsLunar = entry.IsLunar;
            profile.IsCelebrated = entry.IsCelebrated;
            // Optionally update Name if provided, but typically the roster name is master
            if (!string.IsNullOrEmpty(entry.Name))
            {
                profile.FullName = entry.Name;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                profile.Id,
                SubjectId = profile.UserId,
                Name = profile.FullName,
                profile.BirthDate,
                profile.IsLunar,
                profile.IsCelebrated,
            });
        })
        .WithName("CreateBirthdayEntry")
        .WithSummary("생일자 등록 (없으면 프로필 신규 생성 — upsert)");

        // 생일자 수정 — 생일 필드만 갱신한다.
        // 원본의 "SubjectId 로 UserId 를 덮어쓰는" 동작은 위험해서 제거했다 (UserId 는 불변).
        group.MapPut("/{id:int}", async (GhubDbContext db, int id, BirthdayEntryDto updated, UserContext? user) =>
        {
            var profile = await db.BirthdayProfiles.FindAsync(id);
            if (profile is null) return Results.NotFound();

            // Update fields
            profile.BirthDate = updated.BirthDate;
            profile.IsLunar = updated.IsLunar;
            profile.IsCelebrated = updated.IsCelebrated;
            if (!string.IsNullOrEmpty(updated.Name)) profile.FullName = updated.Name;
            profile.ModifiedBy = user?.UserId ?? "system";
            profile.ModifiedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                profile.Id,
                SubjectId = profile.UserId,
                Name = profile.FullName,
                profile.BirthDate,
                profile.IsLunar,
                profile.IsCelebrated,
            });
        })
        .WithName("UpdateBirthdayEntry")
        .WithSummary("생일자 수정");

        // 생일자 삭제 (생일 정보 초기화 — 행은 남긴다, 원본과 같다)
        group.MapDelete("/{id:int}", async (GhubDbContext db, int id, UserContext? user) =>
        {
            var profile = await db.BirthdayProfiles.FindAsync(id);
            if (profile is null) return Results.NotFound();

            profile.BirthDate = null;
            profile.IsCelebrated = false;
            profile.IsLunar = false;
            profile.ModifiedBy = user?.UserId ?? "system";
            profile.ModifiedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteBirthdayEntry")
        .WithSummary("생일자 삭제 (정보 초기화)");

        // 생일 축하 메시지 전송 — 저장만 한다.
        // 원본의 알림 4갈래(통합알림 · 웹푸시 · 이메일 · 카카오)는 이식하지 않는다.
        // 알림 발송은 포털 NotificationServer 연동 결정 대기.
        group.MapPost("/message", async (GhubDbContext db, BirthdayMessageDto dto, UserContext? user) =>
        {
            if (user is null || string.IsNullOrEmpty(user.UserId)) return Results.Unauthorized();
            var senderId = user.UserId;

            var message = new BirthdayMessage
            {
                SenderId = senderId,
                RecipientId = dto.RecipientId,
                Content = dto.Content,
                CreatedBy = senderId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.BirthdayMessages.Add(message);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "축하 메시지를 보냈습니다." });
        })
        .WithName("SendBirthdayMessage")
        .WithSummary("생일 축하 메시지 전송");

        // 오늘의 생일자들의 올해 받은 축하 메시지 목록 조회
        group.MapGet("/today/messages", async (GhubDbContext db) =>
        {
            var today = Kst.Now.Date;
            var currentYear = today.Year;

            // 1. 오늘의 생일자 ID 목록 추출
            var allUsers = await db.BirthdayProfiles.AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && u.BirthDate != null)
                .Select(u => new { u.UserId, BirthDate = u.BirthDate!.Value, u.IsLunar })
                .ToListAsync();

            var todayRecipientIds = new List<string>();
            var koreanCal = new KoreanLunisolarCalendar();

            foreach (var user in allUsers)
            {
                try
                {
                    var occurrenceDate = ToOccurrence(koreanCal, currentYear, user.BirthDate, user.IsLunar);

                    if (occurrenceDate.Date == today)
                    {
                        todayRecipientIds.Add(user.UserId);
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (todayRecipientIds.Count == 0) return Results.Ok(new List<object>());

            // 2. 해당 사용자들의 올해 받은 메시지 조회
            // 연도 경계는 KST 기준이다 — 원본은 UTC 자정(new DateTimeOffset(..., TimeSpan.Zero))이라
            // 1월 1일 0시~9시(KST) 메시지가 작년으로 밀리는 버그가 있었다.
            var yearStartUtc = Kst.ToUtc(new DateTime(currentYear, 1, 1));

            var messages = await db.BirthdayMessages.AsNoTracking()
                .Where(m => todayRecipientIds.Contains(m.RecipientId)
                            && m.CreatedAt >= yearStartUtc
                            && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Content, m.CreatedAt, m.SenderId, m.RecipientId })
                .ToListAsync();

            // 3. 이름 · 부서는 birthday_profiles 딕셔너리로 조인 (네비게이션이 없다)
            var userIds = messages.Select(m => m.SenderId)
                .Concat(messages.Select(m => m.RecipientId))
                .Distinct()
                .ToList();
            var profiles = await LoadProfileNamesAsync(db, userIds);

            var result = messages.Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                SenderName = profiles.TryGetValue(m.SenderId, out var s) ? s.FullName : m.SenderId,
                SenderDepartment = profiles.TryGetValue(m.SenderId, out var s2) ? s2.Department : null,
                RecipientName = profiles.TryGetValue(m.RecipientId, out var r) ? r.FullName : m.RecipientId,
                RecipientId = m.RecipientId,
            });

            return Results.Ok(result);
        })
        .WithName("GetTodayBirthdayMessages")
        .WithSummary("오늘의 생일자들의 올해 받은 축하 메시지 조회");

        // 나에게 온 생일 축하 메시지 조회
        group.MapGet("/message", async (GhubDbContext db, UserContext? user) =>
        {
            if (user is null || string.IsNullOrEmpty(user.UserId)) return Results.Unauthorized();
            var userId = user.UserId;

            var messages = await db.BirthdayMessages.AsNoTracking()
                .Where(m => m.RecipientId == userId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Content, m.CreatedAt, m.SenderId })
                .ToListAsync();

            var profiles = await LoadProfileNamesAsync(db, messages.Select(m => m.SenderId).Distinct().ToList());

            var result = messages.Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                SenderName = profiles.TryGetValue(m.SenderId, out var s) ? s.FullName : m.SenderId,
                SenderDepartment = profiles.TryGetValue(m.SenderId, out var s2) ? s2.Department : null,
            });

            return Results.Ok(result);
        })
        .WithName("GetMyBirthdayMessages")
        .WithSummary("나에게 온 생일 메시지 조회");

        // 내가 보낸 생일 축하 메시지 조회
        group.MapGet("/message/sent", async (GhubDbContext db, UserContext? user) =>
        {
            if (user is null || string.IsNullOrEmpty(user.UserId)) return Results.Unauthorized();
            var userId = user.UserId;

            var messages = await db.BirthdayMessages.AsNoTracking()
                .Where(m => m.SenderId == userId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Content, m.CreatedAt, m.RecipientId })
                .ToListAsync();

            var profiles = await LoadProfileNamesAsync(db, messages.Select(m => m.RecipientId).Distinct().ToList());

            var result = messages.Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                RecipientName = profiles.TryGetValue(m.RecipientId, out var r) ? r.FullName : m.RecipientId,
                RecipientDepartment = profiles.TryGetValue(m.RecipientId, out var r2) ? r2.Department : null,
            });

            return Results.Ok(result);
        })
        .WithName("GetMySentBirthdayMessages")
        .WithSummary("내가 보낸 생일 메시지 조회");
    }

    // ── 내부 도우미 ──────────────────────────────────────────

    /// <summary>목록류 엔드포인트가 공통으로 쓰는 생일 프로필 조회.</summary>
    private static async Task<List<BirthdayProfileRow>> LoadProfilesAsync(GhubDbContext db, string? companyCode)
    {
        var query = db.BirthdayProfiles.AsNoTracking()
            .Where(u => !u.IsDeleted && u.IsActive && u.BirthDate != null);

        if (!string.IsNullOrEmpty(companyCode))
        {
            query = query.Where(u => u.CompanyCode == companyCode);
        }

        return await query
            .Select(u => new BirthdayProfileRow(
                u.Id, u.UserId, u.FullName, u.BirthDate!.Value,
                u.IsLunar, u.IsCelebrated, u.CompanyCode, u.Department))
            .ToListAsync();
    }

    /// <summary>이름 · 부서 조인용 딕셔너리 (UserId → 이름/부서).</summary>
    private static async Task<Dictionary<string, ProfileName>> LoadProfileNamesAsync(GhubDbContext db, List<string> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<string, ProfileName>();

        return await db.BirthdayProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.FullName, p.Department })
            .ToDictionaryAsync(p => p.UserId, p => new ProfileName(p.FullName, p.Department));
    }

    /// <summary>
    /// 저장된 생일(음력이면 음력 월·일로 해석)을 해당 연도의 실제 발생일로 변환한다.
    /// 음력 변환 로직은 원본 그대로다 — 그 해에 없는 날(윤달 · 작은 달)은 말일로 당기고,
    /// 양력 2/29 는 평년이면 2/28 로 대체한다.
    /// </summary>
    private static DateTime ToOccurrence(KoreanLunisolarCalendar koreanCal, int year, DateOnly birthDate, bool isLunar)
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
}

// ── DTO · 응답 형 ───────────────────────────────────────────

/// <summary>생일 축하 메시지 전송 DTO</summary>
/// <param name="RecipientId">수신자 ID</param>
/// <param name="Content">메시지 내용</param>
public record BirthdayMessageDto(string RecipientId, string Content);

/// <summary>생일자 정보 DTO</summary>
/// <param name="SubjectId">대상 사용자 ID</param>
/// <param name="Name">사용자 이름</param>
/// <param name="BirthDate">생년월일 (음력이면 음력 월·일)</param>
/// <param name="IsLunar">음력 여부</param>
/// <param name="IsCelebrated">생일 축하 대상 여부</param>
public record BirthdayEntryDto(string SubjectId, string Name, DateOnly BirthDate, bool IsLunar, bool IsCelebrated);

/// <summary>월별 생일자 수 (stats 응답 항목)</summary>
public record BirthdayMonthStat(int Month, int Total, int Solar, int Lunar);

/// <summary>월별 생일자 목록 항목 (list · current 응답)</summary>
public record BirthdayListItem(
    int Id,
    string SubjectId,
    string Name,
    DateOnly BirthDate,
    DateOnly OccurrenceDate,
    bool IsLunar,
    bool IsCelebrated,
    string? CompanyCode,
    string? Department);

/// <summary>오늘의 생일자 항목 (today 응답 — 썸네일 · 올해 메시지 수 포함)</summary>
public record BirthdayTodayItem(
    int Id,
    string SubjectId,
    string Name,
    DateOnly BirthDate,
    DateOnly OccurrenceDate,
    bool IsLunar,
    bool IsCelebrated,
    string? CompanyCode,
    string? Department,
    string? ThumbnailUrl,
    int MessageCount);

/// <summary>이름 · 부서 조인 결과</summary>
internal record ProfileName(string FullName, string? Department);

/// <summary>목록류 엔드포인트 공용 프로필 투영 (BirthDate 는 non-null 확정)</summary>
internal record BirthdayProfileRow(
    int Id,
    string UserId,
    string FullName,
    DateOnly BirthDate,
    bool IsLunar,
    bool IsCelebrated,
    string? CompanyCode,
    string? Department);
