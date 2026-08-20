using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 일정 관리 API 엔드포인트
/// </summary>
public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/schedules").WithTags("Schedules");

        // 전체 일정 조회 (회사 필터링 및 공통 일정 포함)
        group.MapGet("/", async (AppDbContext db, [FromQuery] int? companyId) =>
        {
            var query = db.Schedules.AsQueryable();

            if (companyId.HasValue)
            {
                // 특정 회사 필터링 시: 해당 회사 일정 + 공통 일정
                query = query.Where(s => s.IsCommon || s.CompanyId == companyId.Value);
            }
            // companyId가 null이면 Where 조건 없이 전체 조회 (관리자용)

            var schedules = await query
                .OrderBy(s => s.StartDate)
                .ToListAsync();
            
            return Results.Ok(new { data = schedules });
        })
        .WithName("GetAllSchedules");

        // 상세 조회
        group.MapGet("/{id}", async (Guid id, AppDbContext db) =>
        {
            var schedule = await db.Schedules.FindAsync(id);
            return schedule is not null 
                ? Results.Ok(new { data = schedule }) 
                : Results.NotFound();
        })
        .WithName("GetScheduleById");

        // 신규 등록
        group.MapPost("/", async (Schedule schedule, AppDbContext db) =>
        {
            if (schedule.Id is null || schedule.Id == Guid.Empty)
            {
                schedule.Id = Guid.NewGuid();
            }
            
            schedule.CreatedAt = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;
            
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();

            return Results.Created($"/api/schedules/{schedule.Id}", schedule);
        })
        .WithName("CreateSchedule");

        // 수정
        group.MapPut("/{id}", async (Guid id, Schedule inputSchedule, AppDbContext db) =>
        {
            var schedule = await db.Schedules.FindAsync(id);

            if (schedule is null) return Results.NotFound();

            schedule.Title = inputSchedule.Title;
            schedule.Description = inputSchedule.Description;
            schedule.StartDate = inputSchedule.StartDate;
            schedule.EndDate = inputSchedule.EndDate;
            schedule.IsCommon = inputSchedule.IsCommon;
            schedule.CompanyId = inputSchedule.CompanyId;
            schedule.IsCompleted = inputSchedule.IsCompleted;
            schedule.CompletedDate = inputSchedule.CompletedDate;
            schedule.CreatedBy = inputSchedule.CreatedBy;
            schedule.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("UpdateSchedule");

        // 삭제
        group.MapDelete("/{id}", async (Guid id, AppDbContext db) =>
        {
            if (await db.Schedules.FindAsync(id) is Schedule schedule)
            {
                db.Schedules.Remove(schedule);
                await db.SaveChangesAsync();
                return Results.Ok(schedule);
            }

            return Results.NotFound();
        })
        .WithName("DeleteSchedule");
    }
}
