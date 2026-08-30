using GhubServer.Data;
using GhubServer.Utilities;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.EntityFrameworkCore;

namespace GhubServer.Endpoints;

/// <summary>
/// 날씨 기준 부합 기록 조회 엔드포인트 (GHUB skgRestApi 이식)
/// </summary>
public static class WeatherEventEndpoints
{
    /// <summary>
    /// 날씨 이벤트 기록 관련 엔드포인트를 매핑합니다.
    /// </summary>
    public static void MapWeatherEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/weather/events")
            .WithTags("WeatherEvent")
            .AddApiResponseWrapper();

        // 기록 목록 조회 (페이징 + 필터)
        group.MapGet("/", async (GhubDbContext db, string? startDate, string? endDate, int? locationId, int page = 1, int pageSize = 20) =>
        {
            var query = db.WeatherEventRecords
                .Include(r => r.WeatherInfo)
                .Include(r => r.WeatherStandard)
                .Where(r => !r.IsDeleted);

            if (locationId.HasValue)
            {
                // WeatherInfo가 null일 수 있으므로 null check 필요
                query = query.Where(r => r.WeatherInfo != null && r.WeatherInfo.WeatherLocationId == locationId);
            }

            // 기간 필터: KST 일자 경계를 UTC 로 환산해 비교한다.
            // 원본은 endDate 를 선언만 하고 쓰지 않아 startDate 하루치만 조회되는 버그가
            // 있었다 — startDate~endDate 범위 필터로 고쳤다 (endDate 그 날까지 포함).
            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
            {
                var startUtc = Kst.ToUtc(start.Date);
                query = query.Where(r => r.EventTime >= startUtc);
            }

            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
            {
                var endUtc = Kst.ToUtc(end.Date.AddDays(1));
                query = query.Where(r => r.EventTime < endUtc);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.EventTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(new { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
        })
        .WithName("GetWeatherEventRecords")
        .WithSummary("날씨 기준 부합 기록 목록 조회");

        // 위젯용 현재 유효 이벤트 조회 (최근 20분)
        group.MapGet("/current", async (GhubDbContext db, int locationId) =>
        {
            // EventTime 은 UTC(DateTimeOffset) 저장이라 절대시간끼리 그대로 비교한다
            var now = DateTimeOffset.UtcNow;
            var from = now.AddMinutes(-20);
            var to = now.AddMinutes(1);

            var events = await db.WeatherEventRecords
                .AsNoTracking()
                .Include(r => r.WeatherStandard)
                .Include(r => r.WeatherInfo)
                .Where(r => !r.IsDeleted)
                .Where(r => r.WeatherInfo != null && r.WeatherInfo.WeatherLocationId == locationId)
                .Where(r => r.EventTime >= from && r.EventTime <= to)
                .OrderByDescending(r => r.EventTime)
                .ToListAsync();

            // 기준(WeatherStandard) 별로 가장 최근 것 하나씩만 추출
            // Category(예: WIND, RAIN)별 1개인지, Standard(강풍주의보, 강풍경보)별 1개인지 모호하나
            // "날씨부합기준별" -> WeatherStandardId 기준이 안전함.
            var distinctEvents = events
                .DistinctBy(e => e.WeatherStandardId)
                .ToList();

            return Results.Ok(distinctEvents);
        })
        .WithName("GetCurrentWeatherEvents")
        .WithSummary("현재 시점 유효 날씨 기준 부합 기록 조회");

        // 기록 삭제 (논리 삭제)
        group.MapDelete("/{id:int}", async (GhubDbContext db, int id, UserContext? user) =>
        {
            var record = await db.WeatherEventRecords.FindAsync(id);
            if (record == null || record.IsDeleted) return Results.NotFound();

            record.IsDeleted = true;
            record.ModifiedAt = DateTimeOffset.UtcNow;
            record.ModifiedBy = user?.UserId ?? "";

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteWeatherEventRecord")
        .WithSummary("날씨 기준 부합 기록 삭제");
    }
}
