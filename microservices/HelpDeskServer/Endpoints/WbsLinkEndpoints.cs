using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using System.Text.Json.Serialization;

namespace HelpDeskServer.Endpoints;
    /// <summary>
    /// WbsLink 생성을 위한 DTO
    /// </summary>
public class WbsLinkCreateDto
{
        /// <summary>소스 WBS ID</summary>
    public int SourceWbsId { get; set; }
        /// <summary>타겟 WBS ID</summary>
    public int TargetWbsId { get; set; }
        /// <summary>연결 타입</summary>
    public string Type { get; set; } = "0";
}

    /// <summary>
    /// WbsLink 수정을 위한 DTO
    /// </summary>
public class WbsLinkUpdateDto
{
        /// <summary>소스 WBS ID</summary>
    public int SourceWbsId { get; set; }
        /// <summary>타겟 WBS ID</summary>
    public int TargetWbsId { get; set; }
        /// <summary>연결 타입</summary>
    public string Type { get; set; } = "0";
}

/// <summary>
/// WbsLink 엔드포인트
/// </summary>
public static class WbsLinkEndpoints
{
    /// <summary>
    /// WBS 연결 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapWbsLinkEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/wbslink");

        // 모든 WbsLink 조회
        group.MapGet("/", (AppDbContext db) =>
        {
            return ApiResponseBuilder.CreateAsync(async () =>
            {
                return await db.WbsLinks.ToListAsync();
            });
        });

        // 단일 WbsLink 조회
        group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
            () => db.WbsLinks.FirstOrDefaultAsync(l => l.Id == id)
        ));

        // 새 WbsLink 생성
        group.MapPost("/", (AppDbContext db, WbsLinkCreateDto linkDto) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var link = new WbsLink
            {
                SourceWbsId = linkDto.SourceWbsId,
                TargetWbsId = linkDto.TargetWbsId,
                Type = linkDto.Type
            };
            db.WbsLinks.Add(link);
            await db.SaveChangesAsync();
            return link;
        }, "WbsLink created successfully.", 201));

        // 기존 WbsLink 수정
        group.MapPut("/{id}", (AppDbContext db, int id, WbsLinkUpdateDto input) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var link = await db.WbsLinks.FindAsync(id);
            if (link is null) return null;

            link.SourceWbsId = input.SourceWbsId;
            link.TargetWbsId = input.TargetWbsId;
            link.Type = input.Type;

            await db.SaveChangesAsync();
            return link;
        }, "WbsLink updated successfully."));

        // WbsLink 삭제
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var link = await db.WbsLinks.FindAsync(id);
            if (link is null) return null;

            db.WbsLinks.Remove(link);
            await db.SaveChangesAsync();
            return new { DeletedId = id };
        }, "WbsLink deleted successfully."));
    }
}
