using HelpDeskServer.Data;
using HelpDeskServer.Models;
// AuditUser() 확장 메서드가 여기 있다 (Services/JsiniUser.cs).
// 작성자를 요청 본문이 아니라 로그인한 계정에서 정하는 데 쓴다.
using HelpDeskServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 일정 관리 API 엔드포인트
/// </summary>
/// <remarks>
/// 예전에는 <b>이 파일만 응답 봉투가 달랐다.</b> 헬프데스크의 다른 엔드포인트는 모두
/// <see cref="ApiResponseBuilder"/> 를 거쳐 <c>{ success, message, data, meta }</c> 로
/// 보내는데, 여기만 손으로 만든 모양을 내보내고 있었다. 그것도 다섯 개가 제각각이었다.
///
/// <list type="table">
///   <item><term>GET /</term><description><c>{ data: [...] }</c> — success 조차 없다</description></item>
///   <item><term>GET /{id}</term><description><c>{ data: {...} }</c> 또는 본문 없는 404</description></item>
///   <item><term>POST /</term><description>엔티티를 그대로 (201)</description></item>
///   <item><term>PUT /{id}</term><description>204, 본문 없음</description></item>
///   <item><term>DELETE /{id}</term><description>엔티티를 그대로</description></item>
/// </list>
///
/// <para>
/// 그래서 포털 프론트가 <c>unwrapSchedule()</c> 이라는 예외 처리를 들고 있었다.
/// <b>서버가 표준을 안 지키는 것을 클라이언트가 떠안고 있던 셈이다</b> — 다른 클라이언트가
/// 붙으면 같은 함정을 또 만난다. 이번에 서버를 맞추고 그 예외 처리를 지웠다(결정 D3-A).
/// </para>
///
/// <para>
/// <b>JinReception 영향은 없다.</b> 그쪽은 자체 토큰을 서버가 믿어 주는 구조였는데,
/// D10 으로 게이트웨이가 <c>/api/helpdesk/**</c> 에 인증을 요구하게 되고 D11 로 헬프데스크
/// 자체 로그인이 닫히면서(<c>LocalLogin:Enabled</c> 기본 false) 이미 로그인 자체가 되지 않는다.
/// </para>
///
/// <para>
/// 모양은 <c>ChecklistEndpoints</c> 와 똑같이 맞췄다. 없는 것을 찾으면 <c>null</c> 을
/// 돌려주면 되고, <see cref="ApiResponseBuilder"/> 가 404 봉투로 바꿔 준다.
/// </para>
/// </remarks>
public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/schedules").WithTags("Schedules");

        // 전체 조회 (회사 필터링 및 공통 일정 포함)
        group.MapGet("/", (AppDbContext db, [FromQuery] int? companyId) =>
            ApiResponseBuilder.CreateAsync(() =>
            {
                var query = db.Schedules.AsQueryable();

                if (companyId.HasValue)
                {
                    // 특정 회사 필터링 시: 해당 회사 일정 + 공통 일정
                    query = query.Where(s => s.IsCommon || s.CompanyId == companyId.Value);
                }
                // companyId 가 null 이면 조건 없이 전체 조회 (관리자용)

                return query.OrderBy(s => s.StartDate).ToListAsync();
            }))
        .WithName("GetAllSchedules");

        // 상세 조회. 없으면 null → 404 봉투.
        group.MapGet("/{id}", (Guid id, AppDbContext db) =>
            ApiResponseBuilder.CreateAsync(
                () => db.Schedules.FirstOrDefaultAsync(s => s.Id == id)))
        .WithName("GetScheduleById");

        // 신규 등록
        group.MapPost("/", (Schedule schedule, AppDbContext db, HttpContext http) =>
            ApiResponseBuilder.CreateAsync(async () =>
            {
                if (schedule.Id is null || schedule.Id == Guid.Empty)
                {
                    schedule.Id = Guid.NewGuid();
                }

                // 작성자는 요청 본문이 아니라 로그인한 JSini 계정에서 정한다.
                // 본문 값을 그대로 믿으면 남의 이름으로 일정을 만들 수 있다.
                schedule.CreatedBy = http.AuditUser();
                schedule.CreatedAt = DateTime.UtcNow;
                schedule.UpdatedAt = DateTime.UtcNow;

                db.Schedules.Add(schedule);
                await db.SaveChangesAsync();

                return schedule;
            }, "Schedule created successfully.", 201))
        .WithName("CreateSchedule");

        // 수정
        group.MapPut("/{id}", (Guid id, Schedule inputSchedule, AppDbContext db) =>
            ApiResponseBuilder.CreateAsync<Schedule>(async () =>
            {
                var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id);
                if (schedule is null) return null;

                schedule.Title = inputSchedule.Title;
                schedule.Description = inputSchedule.Description;
                schedule.StartDate = inputSchedule.StartDate;
                schedule.EndDate = inputSchedule.EndDate;
                schedule.IsCommon = inputSchedule.IsCommon;
                schedule.CompanyId = inputSchedule.CompanyId;
                schedule.IsCompleted = inputSchedule.IsCompleted;
                schedule.CompletedDate = inputSchedule.CompletedDate;
                // 작성자는 처음 만든 사람으로 고정한다. 수정 요청이 덮어쓰지 못한다.
                schedule.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                // 예전에는 204(본문 없음)였다. 이제 저장된 결과를 돌려주므로
                // 화면이 다시 조회하지 않아도 된다.
                return schedule;
            }))
        .WithName("UpdateSchedule");

        // 삭제
        group.MapDelete("/{id}", (Guid id, AppDbContext db) =>
            ApiResponseBuilder.CreateAsync<object>(async () =>
            {
                var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id);
                if (schedule is null) return null;

                db.Schedules.Remove(schedule);
                await db.SaveChangesAsync();

                // 지운 것을 그대로 돌려주지 않는다. 이미 없는 데이터를 응답에 실으면
                // 화면이 그것을 살아 있는 것으로 오해할 수 있다 (ChecklistEndpoints 와 같은 방식).
                return new { DeletedId = id };
            }))
        .WithName("DeleteSchedule");
    }
}
