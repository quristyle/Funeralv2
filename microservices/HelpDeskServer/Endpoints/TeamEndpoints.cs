using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 팀 엔드포인트 — <b>읽기 하나만 남았다.</b>
///
/// 조직(팀·담당자)과 계정은 JSini 관리 포털(AuthServer)이 단독으로 맡는다.
/// 그래서 헬프데스크의 「조직 관리」 메뉴(HD_ORG 아래 팀·팀-고객사·담당자)를
/// 걷어냈고, 그 화면들만 부르던 등록·수정·삭제·검색과 팀-고객사 배정
/// 엔드포인트도 함께 지웠다. <c>/api/admins</c> 는 묶음째 없앴다.
///
/// 팀 <b>레코드</b>는 남는다 — 요청 배정(<c>Admin.AdminTeams</c>)과
/// 프로젝트가 참조한다. 남긴 목록 조회는 프로젝트 관리 화면
/// (<c>/helpdesk/project/manage</c>)이 팀을 고르는 데 쓴다.
/// </summary>
public static class TeamEndpoints
{
    /// <summary>
    /// 팀 관련 엔드포인트를 애플리케이션에 매핑합니다.
    /// </summary>
    public static void MapTeamEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/teams");

        // 전체 조회
        group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
            () => db.Teams.ToListAsync()
        ));
    }
}
