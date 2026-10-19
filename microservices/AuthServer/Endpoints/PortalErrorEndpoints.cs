using System.Linq.Expressions;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Endpoints;

/// <summary>포털 오류 기록 — 오류 화면의 추적 번호로 까닭을 찾는 통로. 넣기는 익명, 꺼내기는 관리자만.</summary>
/// 넣기를 익명으로 둔 것은 로그인 화면 오류도 받아야 해서이며, 공유 비밀(PortalError:Token)로 쓰레기를 막는다.
public static class PortalErrorEndpoints
{
    /// <summary>얼마나 오래 들고 있을지. 넘은 줄은 넣을 때 하루 한 번 걷어낸다.</summary>
    private const int RetentionDays = 180;

    /// <summary>한 번에 돌려주는 최대 건수. 화면이 기간으로 좁히게 한다.</summary>
    private const int MaxTake = 500;

    /// <summary>마지막으로 오래된 줄을 걷어낸 때(UTC). 넣는 길에 얹어 두고 별도 배경 작업은 두지 않는다.</summary>
    private static DateTime _lastSweepUtc = DateTime.MinValue;

    public static void MapPortalErrorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/portal-errors").WithTags("PortalError");

        // ── 넣기 (포털 셸이 부른다) ─────────────────────────
        // 언제나 200 으로 답한다 — 보내는 쪽은 실패를 알려 줘도 할 일이 없다. 비밀이 틀린 경우만 401 이다.
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
        // 전체 번호든 가운데 32자리든 같은 줄이 나온다 — 사람이 전화로 불러 주는 값이라 받는 쪽에서 형식을 고를 수 없다.
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
        // 번호를 못 받았을 때의 길이다. 「언제쯤 났다」만 아는 신고가 더 많고, 같은 오류의 중복도 여기서만 보인다.
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

            // 화면이 보내는 것은 한국 달력 날짜다. 저장값은 UTC 라 그대로 비교하면 하루가 아홉 시간 어긋나므로 경계를 한국 자정으로 옮긴다(docs/utc-time.md).
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

    /// <summary>추적 번호를 조회 열쇠로 바꾼다. 전체·가운데 32자리는 가운데를 뽑고, TraceIdentifier 는 그대로 쓴다.</summary>
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

    /// <summary>한 건을 통째로 — <b>스택까지</b>. 번호로 집어 올 때만 쓴다.</summary>
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

    /// <summary>목록용 — 스택을 뺀 줄을 가져온다. 스택 하나가 수십 KB 라 목록에서는 뺀다.</summary>
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

    /// <summary>오래된 줄을 걷어낸다. 하루 한 번만 실제로 돈다. 실패해도 삼킨다 — 청소 실패로 방금 들어온 기록을 날리면 본말이 뒤집힌다.</summary>
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

    /// <summary>관리자 계열 판정. X-User-Role 은 첫 역할만 담으므로 전체 목록(X-User-Roles)으로 본다.</summary>
    /// 배포 현황과 같은 판정이지만 공용으로 빼지 않는다 — 여기는 스택을 보는 사람이고 그쪽은 컨테이너를 만지는 사람이라 기준이 따로 움직인다.
    private static bool IsAdmin(UserContext user, HttpContext http)
    {
        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Contains("ADMINISTRATOR") || roles.Contains("SYSTEM_ADMINISTRATOR")
               || user.Role is "ADMINISTRATOR" or "SYSTEM_ADMINISTRATOR";
    }
}
