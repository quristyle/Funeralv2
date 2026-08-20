using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Helpers;
using System.Linq.Dynamic.Core;

namespace HelpDeskServer.Endpoints;

/// <summary> 시스템 운영전환 체크리스트 엔드포인트 </summary>
public static class ChecklistEndpoints
{
    /// <summary>
    /// 체크리스트 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapChecklistEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/checklists");

        // 전체 조회 (정렬 포함)
        group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
            () => db.Checklists.OrderBy(c => c.Category).ThenBy(c => c.SortOrder).ToListAsync()
        ));

        // 단일 조회
        group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
            () => db.Checklists.FirstOrDefaultAsync(c => c.Id == id)
        ));

        // 등록
        group.MapPost("/", (AppDbContext db, ChecklistCreateDto dto) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var checklist = new Checklist
            {
                Category = dto.Category,
                ItemName = dto.ItemName,
                SortOrder = dto.SortOrder,
                Note = dto.Note,
                IsChecked = false, // 기본값
                CreatedAt = DateTime.UtcNow
            };

            db.Checklists.Add(checklist);
            await db.SaveChangesAsync();

            return checklist;
        }));

        // 수정
        group.MapPut("/{id}", (AppDbContext db, int id, ChecklistUpdateDto dto) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var checklist = await db.Checklists.FindAsync(id);
            if (checklist is null) return null;

            checklist.Category = dto.Category;
            checklist.ItemName = dto.ItemName;
            checklist.Note = dto.Note;
            checklist.SortOrder = dto.SortOrder;

            // 완료 상태 변경에 따른 시간 처리
            if (dto.IsChecked && !checklist.IsChecked)
            {
                // 완료로 변경 시: 시간이 없으면 현재 시간
                checklist.CompletedAt = dto.CompletedAt ?? DateTime.UtcNow;
            }
            else if (!dto.IsChecked)
            {
                // 미완료로 변경 시: 시간 초기화
                checklist.CompletedAt = null;
            }
            else if (dto.IsChecked && checklist.IsChecked)
            {
                // 이미 완료 상태인데 시간만 변경하거나 그대로 유지
                if (dto.CompletedAt.HasValue)
                {
                    checklist.CompletedAt = dto.CompletedAt;
                }
            }
            
            checklist.IsChecked = dto.IsChecked;

            await db.SaveChangesAsync();
            return checklist;
        }));

        // 삭제
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var checklist = await db.Checklists.FindAsync(id);
            if (checklist is null) return null;

            db.Checklists.Remove(checklist);
            await db.SaveChangesAsync();
            return new { DeletedId = id };
        }));
    }
}