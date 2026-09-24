using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using Microsoft.AspNetCore.Mvc; // FromQueryAttribute 사용을 위해 추가

namespace HelpDeskServer.Endpoints;

/// <summary>
/// WBS 데이터를 트리 구조로 변환하기 위한 DTO
/// </summary>
public class WbsTreeNode {
  /// <summary>
  /// PrimeVue TreeTable에서 각 노드를 고유하게 식별하기 위한 key
  /// </summary>
  public required string Key { get; set; }

  /// <summary>
  /// 실제 WBS 데이터
  /// </summary>
  public required Wbs Data { get; set; }

  /// <summary>
  /// 자식 노드들을 담는 리스트
  /// </summary>
  public List<WbsTreeNode> Children { get; set; } = new();
}

/// <summary>
/// WBS 엔드포인트 — <b>읽기 하나만 남았다.</b>
///
/// <para>
/// 헬프데스크의 「프로젝트」 메뉴 묶음(프로젝트 관리 · WBS · 간트 · 프로젝트
/// 정보)을 2026-09-25 에 걷어내면서 그 화면들만 부르던 길을 함께 지웠다 —
/// 평탄화 조회(<c>/flat</c>, dhtmlx-gantt 용) · 단건 조회 · 등록 · 수정 · 삭제와
/// 그 DTO 들, 부모 진행률·날짜를 거슬러 고치던 재귀 도우미가 그것이다.
/// </para>
///
/// <para>
/// 트리 조회 하나만 남긴 까닭은 <b>「도구 &gt; 다이어그램」 화면</b>
/// (<c>/helpdesk/util/diagram</c>)이 그림을 걸 항목을 여기서 고르기 때문이다.
/// 그 화면은 프로젝트 묶음이 아니라 도구 묶음에 있어 이번에 지우지 않았다.
/// </para>
///
/// <para>
/// WBS <b>레코드</b>와 표는 그대로다. 걷어낸 것은 길뿐이다.
/// </para>
/// </summary>
public static class WbsEndpoints {
  /// <summary>
  /// WBS 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapWbsEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/wbs");

    // 모든 WBS 항목을 트리 구조로 조회
    group.MapGet("/", (AppDbContext db, [FromQuery] int? projectId) => {
      return ApiResponseBuilder.CreateAsync(async () => {
        var baseQuery = db.Wbs.Include(w => w.ParentWbs).AsQueryable();
        if (projectId.HasValue) {
          baseQuery = baseQuery.Where(w => w.ProjectId == projectId.Value);
        }

        var allWbsItems = await baseQuery
            .OrderBy(w => w.WbsCode)
            .OrderBy(w => w.OrderIndex)
            .ToListAsync();

        var allNodes = allWbsItems.ToDictionary(
            item => item.WbsRid,
            item => new WbsTreeNode { Key = item.WbsRid.ToString(), Data = item }
        );

        var rootNodes = new List<WbsTreeNode>();

        foreach (var wbs in allWbsItems) {
          // 부모가 있고, 부모 노드가 딕셔너리에 존재할 경우
          if (wbs.ParentWbsId.HasValue && allNodes.ContainsKey(wbs.ParentWbsId.Value)) {
            if (allNodes.TryGetValue(wbs.ParentWbsId.Value, out var parentNode)) {
              // 부모의 Children 리스트에 현재 노드를 추가
              parentNode.Children.Add(allNodes[wbs.WbsRid]);
            }
          }
          else {
            // 부모가 없는 경우 최상위 노드(root)로 추가
            rootNodes.Add(allNodes[wbs.WbsRid]);
          }
        }
        return rootNodes;
      });
    });
  }
}
