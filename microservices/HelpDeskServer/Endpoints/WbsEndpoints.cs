using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using Microsoft.AspNetCore.Mvc; // FromQueryAttribute 사용을 위해 추가

namespace HelpDeskServer.Endpoints;

/// <summary>WBS 데이터를 트리 구조로 변환하기 위한 DTO</summary>
public class WbsTreeNode {
  /// <summary>PrimeVue TreeTable에서 각 노드를 고유하게 식별하기 위한 key</summary>
  public required string Key { get; set; }

  /// <summary>실제 WBS 데이터</summary>
  public required Wbs Data { get; set; }

  /// <summary>자식 노드들을 담는 리스트</summary>
  public List<WbsTreeNode> Children { get; set; } = new();
}

/// <summary>WBS 엔드포인트. 읽기(트리 조회) 하나만 남았다 — 「도구 &gt; 다이어그램」이 그림을 걸 항목을 고르는 데 쓴다. WBS 레코드와 표는 그대로다.</summary>
public static class WbsEndpoints {
  /// <summary>WBS 관련 엔드포인트를 애플리케이션에 매핑합니다.</summary>
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
