using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Utilities;
using System.Text.Json.Serialization;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// Project 생성을 위한 DTO
/// </summary>
public class ProjectCreateDto
{
    /// <summary>프로젝트 명</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>프로젝트 시작일</summary>
    [JsonConverter(typeof(DateOnlyConverter))]
    public DateTime? ProjectStart { get; set; }
    /// <summary>프로젝트 종료일</summary>
    [JsonConverter(typeof(DateOnlyConverter))]
    public DateTime? ProjectEnd { get; set; }
    /// <summary>담당 팀 ID</summary>
    public int? TeamId { get; set; } // TeamId 추가
}

/// <summary>
/// Project 수정을 위한 DTO
/// </summary>
public class ProjectUpdateDto
{
    /// <summary>프로젝트 명</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>프로젝트 시작일</summary>
    [JsonConverter(typeof(DateOnlyConverter))]
    public DateTime? ProjectStart { get; set; }
    /// <summary>프로젝트 종료일</summary>
    [JsonConverter(typeof(DateOnlyConverter))]
    public DateTime? ProjectEnd { get; set; }
    /// <summary>담당 팀 ID</summary>
    public int? TeamId { get; set; } // TeamId 추가
}

/// <summary>
/// Project 엔드포인트
/// </summary>
public static class ProjectEndpoints
{
    /// <summary>
    /// 프로젝트 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapProjectEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/project");

        // 모든 Project 조회
        group.MapGet("/", (AppDbContext db) =>
        {
            return ApiResponseBuilder.CreateAsync(async () =>
            {
                return await db.Projects.Include(p => p.Team).ToListAsync(); // Team 정보 포함
            });
        });

        // 단일 Project 조회
        group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
            () => db.Projects.Include(p => p.Team).FirstOrDefaultAsync(p => p.Id == id) // Team 정보 포함
        ));

        // 새 Project 생성
        group.MapPost("/", (AppDbContext db, ProjectCreateDto projectDto) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var project = new Project
            {
                Name = projectDto.Name,
                ProjectStart = projectDto.ProjectStart,
                ProjectEnd = projectDto.ProjectEnd,
                TeamId = projectDto.TeamId // TeamId 저장
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project;
        }, "Project created successfully.", 201));

        // 기존 Project 수정
        group.MapPut("/{id}", (AppDbContext db, int id, ProjectUpdateDto input) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var project = await db.Projects.FindAsync(id);
            if (project is null) return null;

            project.Name = input.Name;
            project.ProjectStart = input.ProjectStart;
            project.ProjectEnd = input.ProjectEnd;
            project.TeamId = input.TeamId; // TeamId 수정

            await db.SaveChangesAsync();
            return project;
        }, "Project updated successfully."));

        // Project 삭제
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () =>
        {
            var project = await db.Projects.FindAsync(id);
            if (project is null) return null;

            db.Projects.Remove(project);
            await db.SaveChangesAsync();
            return new { DeletedId = id };
        }, "Project deleted successfully."));
    }
}
