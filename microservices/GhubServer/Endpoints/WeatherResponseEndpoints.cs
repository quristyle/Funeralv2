using GhubServer.Data;
using GhubServer.Dtos;
using GhubServer.Models;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.EntityFrameworkCore;

namespace GhubServer.Endpoints;

/// <summary>
/// 날씨 기준별 대응 정보 관리 엔드포인트 (GHUB skgRestApi 이식)
/// </summary>
public static class WeatherResponseEndpoints
{
    /// <summary>
    /// 날씨 기준 대응 정보 엔드포인트를 매핑합니다.
    /// </summary>
    public static void MapWeatherResponseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/weather/responses")
            .WithTags("WeatherResponse")
            .AddApiResponseWrapper();

        // 대응 정보 전체 조회
        group.MapGet("/", async (GhubDbContext db) =>
        {
            var responses = await db.WeatherResponses
                .Include(r => r.WeatherStandard)
                .Where(r => !r.IsDeleted && !r.WeatherStandard!.IsDeleted)
                .OrderBy(r => r.WeatherStandard!.Category)
                .ThenBy(r => r.SortOrder)
                .ToListAsync();
            return Results.Ok(responses);
        })
        .WithName("GetWeatherResponses")
        .WithSummary("날씨 대응 정보 목록 조회");

        // 기준별 대응 정보 조회
        group.MapGet("/by-standard/{standardId:int}", async (GhubDbContext db, int standardId) =>
        {
            var responses = await db.WeatherResponses
                .Where(r => r.WeatherStandardId == standardId && !r.IsDeleted)
                .OrderBy(r => r.SortOrder)
                .ToListAsync();
            return Results.Ok(responses);
        })
        .WithName("GetWeatherResponsesByStandard")
        .WithSummary("기준별 대응 정보 조회");

        // 대응 정보 생성
        group.MapPost("/", async (GhubDbContext db, WeatherResponse response, UserContext? user) =>
        {
            // 기준 존재 여부 확인
            if (!await db.WeatherStandards.AnyAsync(s => s.Id == response.WeatherStandardId && !s.IsDeleted))
            {
                return Results.BadRequest("존재하지 않는 날씨 기준입니다.");
            }

            response.CreatedAt = DateTimeOffset.UtcNow;
            response.CreatedBy = user?.UserId ?? "";

            db.WeatherResponses.Add(response);
            await db.SaveChangesAsync();
            return Results.Created($"/weather/responses/{response.Id}", response);
        })
        .WithName("CreateWeatherResponse")
        .WithSummary("날씨 대응 정보 생성");

        // 대응 정보 수정
        group.MapPut("/{id:int}", async (GhubDbContext db, int id, WeatherResponse updatedResponse, UserContext? user) =>
        {
            var response = await db.WeatherResponses.FindAsync(id);
            if (response == null || response.IsDeleted) return Results.NotFound();

            response.ActionContent = updatedResponse.ActionContent;
            response.Description = updatedResponse.Description;
            response.SortOrder = updatedResponse.SortOrder;
            response.WeatherStandardId = updatedResponse.WeatherStandardId;
            response.ModifiedAt = DateTimeOffset.UtcNow;
            response.ModifiedBy = user?.UserId ?? "";

            await db.SaveChangesAsync();
            return Results.Ok(response);
        })
        .WithName("UpdateWeatherResponse")
        .WithSummary("날씨 대응 정보 수정");

        // 대응 정보 삭제 (논리 삭제)
        group.MapDelete("/{id:int}", async (GhubDbContext db, int id, UserContext? user) =>
        {
            var response = await db.WeatherResponses.FindAsync(id);
            if (response == null || response.IsDeleted) return Results.NotFound();

            response.IsDeleted = true;
            response.ModifiedAt = DateTimeOffset.UtcNow;
            response.ModifiedBy = user?.UserId ?? "";

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteWeatherResponse")
        .WithSummary("날씨 대응 정보 삭제");

        // 대응 정보 순서 변경
        group.MapPut("/reorder", async (GhubDbContext db, List<WeatherResponseReorderRequest> requests, UserContext? user) =>
        {
            var ids = requests.Select(r => r.Id).ToList();
            var responses = await db.WeatherResponses.Where(r => ids.Contains(r.Id)).ToListAsync();

            foreach (var req in requests)
            {
                var response = responses.FirstOrDefault(r => r.Id == req.Id);
                if (response != null)
                {
                    response.SortOrder = req.SortOrder;
                    response.ModifiedAt = DateTimeOffset.UtcNow;
                    response.ModifiedBy = user?.UserId ?? "";
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        })
        .WithName("ReorderWeatherResponses")
        .WithSummary("날씨 대응 정보 순서 변경");
    }
}
