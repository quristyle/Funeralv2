using System.Linq.Expressions;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Endpoints;

/// <summary>
/// 포털 오류 기록 — 오류 화면이 보여 준 <b>추적 번호로 까닭을 찾는</b> 통로.
/// </summary>
/// <remarks>
/// <para>
/// 길이 둘이다. 포털 셸이 미처리 예외를 <c>POST</c> 로 밀어 넣고(익명),
/// 포털관리의 「오류 추적」 화면이 <c>GET</c> 으로 꺼내 본다(관리자만).
/// </para>
///
/// <para>
/// [넣는 길이 익명인 이유]
/// </para>
///
/// <para>
/// 오류는 <b>로그인 화면에서도 난다.</b> 토큰을 요구하면 정작 가장 알고 싶은
/// 오류(로그인이 안 된다)가 한 건도 안 들어온다. 게이트웨이의
/// <c>/api/auth/**</c> 가 익명이라 그 길은 이미 열려 있고, 그래서 **아무나
/// 쓰레기를 밀어 넣을 수 있다** — 공유 비밀 하나로 막는다
/// (<c>PortalError:Token</c>, 헤더 <c>X-Portal-Error-Token</c>).
/// 배포 알림(<c>DeployNotify:Token</c>)과 같은 방식이다.
/// </para>
///
/// <para>
/// 설정이 비어 있으면 검사하지 않는다 — 개발 장비를 위해서다. 운영은
/// compose 의 환경변수로 넣는다. 값이 비면 <b>조용히 열려 있게 되므로</b>
/// 기동 때 경고를 한 줄 남긴다.
/// </para>
///
/// <para>
/// [꺼내는 길은 관리자만]
/// </para>
///
/// <para>
/// 스택 추적에는 내부 경로와 질의가 묻어 나온다. 배포 현황·컨테이너 로그와
/// 같은 급으로 다룬다(ADMINISTRATOR · SYSTEM_ADMINISTRATOR).
/// </para>
/// </remarks>
public static class PortalErrorEndpoints
{
    /// <summary>얼마나 오래 들고 있을지. 넘은 줄은 넣을 때 하루 한 번 걷어낸다.</summary>
    private const int RetentionDays = 180;

    /// <summary>한 번에 돌려주는 최대 건수. 화면이 기간으로 좁히게 한다.</summary>
    private const int MaxTake = 500;

    /// <summary>
    /// 마지막으로 오래된 줄을 걷어낸 때(UTC). 배경 작업을 하나 더 두지 않으려고
    /// <b>넣는 길에 얹었다</b> — 오류가 안 들어오는 동안에는 지울 것도 안 생긴다.
    /// </summary>
    private static DateTime _lastSweepUtc = DateTime.MinValue;

    public static void MapPortalErrorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/portal-errors").WithTags("PortalError");

        // ── 넣기 (포털 셸이 부른다) ─────────────────────────
        //
        // **언제나 200 으로 답한다.** 보내는 쪽은 오류를 보고하려다 실패해도 할
        // 수 있는 일이 없고, 실패를 알려 주면 거기서 또 예외가 날 자리만 는다.
        // 막은 경우(비밀 불일치)만 401 이다 — 그것은 설정 실수라 드러나야 한다.
        group.MapPost("", async (
            [FromBody] PortalErrorReportDto report,
            HttpContext http,
            IConfiguration config,
            AppDbContext db,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            var expected = config["PortalError:Token"];
            if (!string.IsNullOrWhiteSpace(expected) && !expected.StartsWith("__"))
            {
                var given = http.Request.Headers["X-Portal-Error-Token"].ToString();
                if (!string.Equals(given, expected, StringComparison.Ordinal))
                {
                    return Results.Json(
                        ApiResponse<object>.Fail("허용되지 않은 호출입니다.", "UNAUTHORIZED"),
                        statusCode: StatusCodes.Status401Unauthorized);
                }
            }

            var traceId = NormalizeTrace(report.TraceId ?? report.Traceparent);
            if (string.IsNullOrEmpty(traceId))
            {
                // 번호가 없으면 찾을 수가 없다. 받아 두면 영영 안 읽힐 줄만 쌓인다.
                return Results.Ok(ApiResponse<object>.Ok(new { stored = false }));
            }

            var entity = new PortalErrorLog
            {
                TraceId = traceId,
                Traceparent = Cut(report.Traceparent, 128),
                OccurredAt = report.OccurredAt ?? AppTime.UtcNow,
                Source = Cut(report.Source, 32) ?? "portal",
                Path = Cut(report.Path, 512),
                QueryString = Cut(report.QueryString, 1024),
                Method = Cut(report.Method, 16),
                UserId = Cut(report.UserId, 128),
                Ip = Cut(report.Ip, 64),
                UserAgent = Cut(report.UserAgent, 512),
                ExceptionType = Cut(report.ExceptionType, 256),
                Message = Cut(report.Message, 2048),

                // 스택만 상한이 넉넉하다(열이 text 다). 그래도 무한은 아니다 —
                // 재귀가 터지면 스택 하나가 수 MB 가 된다.
                Detail = Cut(report.Detail, 64 * 1024),
            };

            try
            {
                db.PortalErrorLogs.Add(entity);
                await db.SaveChangesAsync(ct);
                await SweepAsync(db, ct);
            }
            catch (Exception ex)
            {
                // 여기서 던지면 보내는 쪽이 재시도하고, 같은 까닭으로 또 실패한다.
                loggers.CreateLogger(typeof(PortalErrorEndpoints))
                    .LogError(ex, "포털 오류 기록을 저장하지 못했다 (trace {TraceId})", traceId);

                return Results.Ok(ApiResponse<object>.Ok(new { stored = false }));
            }

            return Results.Ok(ApiResponse<object>.Ok(new { stored = true, id = entity.Id }));
        })
        .AllowAnonymous()
        .WithName("ReportPortalError");

        // ── 추적 번호로 집어 오기 ───────────────────────────
        //
        // 전체 번호를 쳐도, 가운데 32자리만 쳐도 같은 줄이 나온다. 사람이
        // 전화로 불러 주는 값이라 어느 쪽으로 받아 적을지 고를 수 없다.
        group.MapGet("/{trace}", async (string trace,
            UserContext? user,
            HttpContext http,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();
            if (!IsAdmin(user, http)) return Forbidden();

            var traceId = NormalizeTrace(trace);
            if (string.IsNullOrEmpty(traceId))
            {
                return Results.Ok(ApiResponse<List<PortalErrorDto>>.Ok([]));
            }

            var rows = await db.PortalErrorLogs.AsNoTracking()
                .Where(e => e.TraceId == traceId)
                .OrderByDescending(e => e.OccurredAt)
                .Select(Full)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<List<PortalErrorDto>>.Ok(rows));
        })
        .WithName("GetPortalErrorByTrace");

        // ── 최근 목록 ──────────────────────────────────────
        //
        // 번호를 못 받았을 때의 길이다. 「언제쯤 났다」만 아는 신고가 실제로
        // 더 많고, 같은 오류가 여러 사람에게 났는지도 여기서만 보인다.
        group.MapGet("", async (UserContext? user,
            HttpContext http,
            AppDbContext db,
            [FromQuery] string? keyword,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? take,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();
            if (!IsAdmin(user, http)) return Forbidden();

            var query = db.PortalErrorLogs.AsNoTracking().AsQueryable();

            // 화면이 보내는 것은 **사람이 고른 한국 달력의 날짜**다. 저장값은
            // UTC 이고 서버 시계도 UTC 라(TZ=Etc/UTC), 그대로 비교하면 하루가
            // 아홉 시간 어긋난다 — 한국의 오전 9시 전에 난 오류가 전날 칸으로
            // 밀린다. 경계를 한국 자정 기준으로 옮긴다(docs/utc-time.md).
            if (from is { } f)
            {
                var start = AppTime.StartOfDayUtc(DateOnly.FromDateTime(f));
                query = query.Where(e => e.OccurredAt >= start);
            }

            if (to is { } t)
            {
                var end = AppTime.StartOfDayUtc(DateOnly.FromDateTime(t).AddDays(1));
                query = query.Where(e => e.OccurredAt < end);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                query = query.Where(e =>
                    EF.Functions.ILike(e.Path ?? "", $"%{k}%")
                    || EF.Functions.ILike(e.ExceptionType ?? "", $"%{k}%")
                    || EF.Functions.ILike(e.Message ?? "", $"%{k}%")
                    || EF.Functions.ILike(e.UserId ?? "", $"%{k}%")
                    || EF.Functions.ILike(e.TraceId, $"%{k}%"));
            }

            var rows = await query
                .OrderByDescending(e => e.OccurredAt)
                .Take(Math.Clamp(take ?? 200, 1, MaxTake))
                .Select(Summary)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<List<PortalErrorDto>>.Ok(rows));
        })
        .WithName("GetPortalErrors");
    }

    /// <summary>
    /// 추적 번호를 조회 열쇠로 바꾼다.
    ///
    /// <para>
    /// 받는 모양이 셋이다 — 전체(<c>00-{32}-{16}-00</c>), 가운데 32자리,
    /// 그리고 <c>Activity.Id</c> 가 아닌 <c>HttpContext.TraceIdentifier</c>
    /// (<c>0HN7…:00000003</c>). 앞의 둘은 가운데를 뽑고, 셋째는 그대로 쓴다.
    /// </para>
    /// </summary>
    private static string NormalizeTrace(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return string.Empty;

        if (text.StartsWith("00-", StringComparison.Ordinal))
        {
            var parts = text.Split('-');
            if (parts.Length >= 2 && parts[1].Length > 0) return parts[1].ToLowerInvariant();
        }

        return text.Length > 64 ? text[..64] : text.ToLowerInvariant();
    }

    private static string? Cut(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>
    /// 한 건을 통째로 — <b>스택까지</b>. 번호로 집어 올 때만 쓴다.
    /// </summary>
    private static readonly Expression<Func<PortalErrorLog, PortalErrorDto>> Full = e => new PortalErrorDto
    {
        Id = e.Id,
        TraceId = e.TraceId,
        Traceparent = e.Traceparent,
        OccurredAt = e.OccurredAt,
        Source = e.Source,
        Path = e.Path,
        QueryString = e.QueryString,
        Method = e.Method,
        UserId = e.UserId,
        Ip = e.Ip,
        UserAgent = e.UserAgent,
        ExceptionType = e.ExceptionType,
        Message = e.Message,
        Detail = e.Detail,
    };

    /// <summary>
    /// 목록용 — <b>스택을 빼고</b> 가져온다.
    ///
    /// <para>
    /// 스택 하나가 수십 KB 다. 200줄을 그대로 끌어오면 응답이 몇 MB 가 되고,
    /// 그 중 사람이 실제로 펴 보는 것은 한둘이다. 화면은 줄을 고르면
    /// 번호로 다시 집어 온다.
    /// </para>
    /// </summary>
    private static readonly Expression<Func<PortalErrorLog, PortalErrorDto>> Summary = e => new PortalErrorDto
    {
        Id = e.Id,
        TraceId = e.TraceId,
        Traceparent = e.Traceparent,
        OccurredAt = e.OccurredAt,
        Source = e.Source,
        Path = e.Path,
        QueryString = e.QueryString,
        Method = e.Method,
        UserId = e.UserId,
        Ip = e.Ip,
        UserAgent = e.UserAgent,
        ExceptionType = e.ExceptionType,
        Message = e.Message,
    };

    /// <summary>
    /// 오래된 줄을 걷어낸다. 하루 한 번만 실제로 돈다.
    /// </summary>
    /// <remarks>
    /// 실패해도 삼키는 이유는 이것이 <b>곁다리</b>이기 때문이다 — 청소가 안 돼서
    /// 방금 들어온 오류 기록을 날리면 본말이 뒤집힌다.
    /// </remarks>
    private static async Task SweepAsync(AppDbContext db, CancellationToken ct)
    {
        var now = AppTime.UtcNow;
        if (now - _lastSweepUtc < TimeSpan.FromDays(1)) return;
        _lastSweepUtc = now;

        try
        {
            await db.PortalErrorLogs
                .Where(e => e.OccurredAt < now.AddDays(-RetentionDays))
                .ExecuteDeleteAsync(ct);
        }
        catch
        {
            // 다음 날 다시 해 본다.
        }
    }

    private static IResult Forbidden() => Results.Json(
        ApiResponse<object>.Fail("관리자만 볼 수 있습니다.", "FORBIDDEN"),
        statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// 관리자 계열 판정. <c>X-User-Role</c> 은 첫 역할 하나뿐이라
    /// 전체 목록(<c>X-User-Roles</c>)으로 함께 본다.
    /// </summary>
    /// <remarks>
    /// 배포 현황(<see cref="DeployStatusEndpoints"/>)과 같은 판정이다. 공용으로
    /// 빼지 않은 이유는 그쪽이 「컨테이너를 만질 수 있는 사람」이고 여기는
    /// 「스택 추적을 볼 수 있는 사람」이라, 한쪽만 넓히고 싶어질 때
    /// 한 곳을 고치면 다른 쪽이 말없이 따라 넓어지기 때문이다.
    /// </remarks>
    private static bool IsAdmin(UserContext user, HttpContext http)
    {
        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Contains("ADMINISTRATOR") || roles.Contains("SYSTEM_ADMINISTRATOR")
               || user.Role is "ADMINISTRATOR" or "SYSTEM_ADMINISTRATOR";
    }
}
