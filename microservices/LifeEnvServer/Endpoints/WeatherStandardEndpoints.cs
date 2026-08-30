using GhubServer.Data;
using GhubServer.Models;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.EntityFrameworkCore;

namespace GhubServer.Endpoints;

/// <summary>
/// 날씨 기준 정보 관리 엔드포인트 (GHUB skgRestApi 이식)
/// </summary>
public static class WeatherStandardEndpoints
{
    /// <summary>
    /// 날씨 기준 관리 엔드포인트를 매핑합니다.
    /// </summary>
    public static void MapWeatherStandardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/weather/standards")
            .WithTags("WeatherStandard")
            .AddApiResponseWrapper();

        // 날씨 기준 목록 조회
        group.MapGet("/", async (GhubDbContext db) =>
        {
            var standards = await db.WeatherStandards
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Category).ThenBy(s => s.SortOrder)
                .ToListAsync();
            return Results.Ok(standards);
        })
        .WithName("GetWeatherStandards")
        .WithSummary("날씨 기준 목록 조회");

        // 날씨 기준 단건 조회
        group.MapGet("/{id:int}", async (GhubDbContext db, int id) =>
        {
            var standard = await db.WeatherStandards.FindAsync(id);
            if (standard == null || standard.IsDeleted) return Results.NotFound();
            return Results.Ok(standard);
        })
        .WithName("GetWeatherStandard")
        .WithSummary("날씨 기준 상세 조회");

        // 날씨 기준 생성
        group.MapPost("/", async (GhubDbContext db, WeatherStandard standard, UserContext? user) =>
        {
            standard.CreatedAt = DateTimeOffset.UtcNow;
            standard.CreatedBy = user?.UserId ?? "";

            db.WeatherStandards.Add(standard);
            await db.SaveChangesAsync();
            return Results.Created($"/weather/standards/{standard.Id}", standard);
        })
        .WithName("CreateWeatherStandard")
        .WithSummary("날씨 기준 생성");

        // 날씨 기준 수정
        group.MapPut("/{id:int}", async (GhubDbContext db, int id, WeatherStandard updatedStandard, UserContext? user) =>
        {
            var standard = await db.WeatherStandards.FindAsync(id);
            if (standard == null || standard.IsDeleted) return Results.NotFound();

            standard.Category = updatedStandard.Category;
            standard.Name = updatedStandard.Name;
            standard.ConditionText = updatedStandard.ConditionText;
            standard.ThresholdValue = updatedStandard.ThresholdValue;
            standard.Operator = updatedStandard.Operator;
            standard.ThresholdValue2 = updatedStandard.ThresholdValue2;
            standard.Unit = updatedStandard.Unit;
            standard.WorkStatus = updatedStandard.WorkStatus;
            standard.SortOrder = updatedStandard.SortOrder;
            standard.Duration = updatedStandard.Duration;
            standard.PrevDayDiff = updatedStandard.PrevDayDiff;
            standard.AvgYearDiff = updatedStandard.AvgYearDiff;
            standard.NotificationInterval = updatedStandard.NotificationInterval;
            standard.UseSensibleTemp = updatedStandard.UseSensibleTemp;
            standard.ModifiedAt = DateTimeOffset.UtcNow;
            standard.ModifiedBy = user?.UserId ?? "";

            await db.SaveChangesAsync();
            return Results.Ok(standard);
        })
        .WithName("UpdateWeatherStandard")
        .WithSummary("날씨 기준 수정");

        // 날씨 기준 삭제 (논리 삭제)
        group.MapDelete("/{id:int}", async (GhubDbContext db, int id, UserContext? user) =>
        {
            var standard = await db.WeatherStandards.FindAsync(id);
            if (standard == null || standard.IsDeleted) return Results.NotFound();

            standard.IsDeleted = true;
            standard.ModifiedAt = DateTimeOffset.UtcNow;
            standard.ModifiedBy = user?.UserId ?? "";

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteWeatherStandard")
        .WithSummary("날씨 기준 삭제");
    }
}
