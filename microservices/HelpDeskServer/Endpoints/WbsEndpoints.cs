
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using System.Linq.Dynamic.Core;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc; // FromQueryAttribute 사용을 위해 추가

using HelpDeskServer.Utilities;
using System.Diagnostics;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// WBS 데이터를 트리 구조로 변환하기 위한 DTO
/// </summary>
public class WbsTreeNode {
  /// <summary>
  /// PrimeVue TreeTable에서 각 노드를 고유하게 식별하기 위한 key
  /// </summary>
  public string Key { get; set; }

  /// <summary>
  /// 실제 WBS 데이터
  /// </summary>
  public Wbs Data { get; set; }

  /// <summary>
  /// 자식 노드들을 담는 리스트
  /// </summary>
  public List<WbsTreeNode> Children { get; set; } = new();
}

/// <summary>
/// WBS 생성을 위한 DTO
/// </summary>
public class WbsCreateDto {
  /// <summary>부모 WBS ID</summary>
  [JsonConverter(typeof(NullableIntConverter))]
  public int? ParentWbsId { get; set; }
  /// <summary>WBS 코드</summary>
  public string WbsCode { get; set; }
  /// <summary>WBS 이름</summary>
  public string WbsName { get; set; }
  /// <summary>WBS 타입</summary>
  public string? WbsType { get; set; }
  /// <summary>WBS 레벨</summary>
  public int? WbsLevel { get; set; }
  /// <summary>계획 시작일</summary>
  [JsonConverter(typeof(DateOnlyConverter))]
  public DateTime? PlanStart { get; set; }
  /// <summary>계획 종료일</summary>
  [JsonConverter(typeof(DateOnlyConverter))]
  public DateTime? PlanEnd { get; set; }
  /// <summary>진행률</summary>
  [JsonConverter(typeof(NullableDecimalConverter))]
  public decimal? Progress { get; set; }
  /// <summary>우선순위</summary>
  public string? Priority { get; set; }
  /// <summary>상태</summary>
  public string Status { get; set; } = "Pending";
  /// <summary>프로젝트 ID</summary>
  public int? ProjectId { get; set; } // ProjectId 추가
  /// <summary>정렬 순서</summary>
  [JsonConverter(typeof(NullableIntConverter))]
  public int? OrderIndex { get; set; } // OrderIndex 추가
}

/// <summary>
/// WBS 수정을 위한 DTO
/// </summary>
public class WbsUpdateDto {
  /// <summary>WBS 코드</summary>
  public string WbsCode { get; set; }
  /// <summary>WBS 이름</summary>
  public string WbsName { get; set; }
  /// <summary>WBS 타입</summary>
  public string? WbsType { get; set; }
  /// <summary>WBS 레벨</summary>
  public int? WbsLevel { get; set; }
  /// <summary>계획 시작일</summary>
  [JsonConverter(typeof(DateOnlyConverter))]
  public DateTime? PlanStart { get; set; }
  /// <summary>계획 종료일</summary>
  [JsonConverter(typeof(DateOnlyConverter))]
  public DateTime? PlanEnd { get; set; }
  /// <summary>실제 시작일</summary>
  public DateTime? ActualStart { get; set; }
  /// <summary>실제 종료일</summary>
  public DateTime? ActualEnd { get; set; }
  /// <summary>진행률</summary>
  public decimal Progress { get; set; }
  /// <summary>우선순위</summary>
  public string? Priority { get; set; }
  /// <summary>상태</summary>
  public string Status { get; set; }
  /// <summary>프로젝트 ID</summary>
  public int? ProjectId { get; set; } // ProjectId 추가
  /// <summary>정렬 순서</summary>
  [JsonConverter(typeof(NullableIntConverter))]
  public int? OrderIndex { get; set; } // OrderIndex 추가

  /// <summary>부모 WBS ID</summary>
  [JsonConverter(typeof(NullableIntConverter))]
  public int? ParentWbsId { get; set; }
}

/// <summary>
/// WBS 엔드포인트
/// </summary>
public static class WbsEndpoints {
  /// <summary>
  /// dhtmlx-gantt를 위한 평탄화된 WBS DTO
  /// </summary>
  public class WbsGanttTaskDto {
    /// <summary>Gantt ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
    /// <summary>Gantt 텍스트</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; }
    /// <summary>Gantt 시작일</summary>
    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }
    /// <summary>Gantt 종료일</summary>
    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }
    /// <summary>Gantt 진행률</summary>
    [JsonPropertyName("progress")]
    public decimal Progress { get; set; }
    /// <summary>Gantt 부모 ID</summary>
    [JsonPropertyName("parent")]
    public int? Parent { get; set; }
    /// <summary>Gantt 노드 펼침 여부</summary>
    [JsonPropertyName("open")]
    public bool Open { get; set; } = true;

    /// <summary>
    /// 원본 WBS 코드
    /// </summary>
    [JsonPropertyName("wbsCode")]
    public string WbsCode { get; set; }
    /// <summary>
    /// 원본 상태
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }
    /// <summary>
    /// 원본 우선순위
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
    /// <summary>
    /// 원본 프로젝트 ID
    /// </summary>
    [JsonPropertyName("projectId")]
    public int? ProjectId { get; set; } // ProjectId 추가



    /// <summary>
    /// 원본 정렬 순서
    /// </summary>
    [JsonPropertyName("orderIndex")]
    public int? OrderIndex { get; set; } // OrderIndex 추가

    /// <summary>
    /// 원본 부모 WBS ID
    /// </summary>
    [JsonPropertyName("parentWbsId")]
    [JsonConverter(typeof(NullableIntConverter))]
    public int? ParentWbsId { get; set; } // ParentWbsId 추가

  }

  /// <summary>
  /// dhtmlx-gantt 링크 전용 DTO
  /// </summary>
  public class WbsLinkGanttDto {
    /// <summary>링크 ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
    /// <summary>소스 Task ID</summary>
    [JsonPropertyName("source")]
    public int Source { get; set; }
    /// <summary>타겟 Task ID</summary>
    [JsonPropertyName("target")]
    public int Target { get; set; }
    /// <summary>링크 타입</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }
  }

  /// <summary>
  /// WBS 트리 구조를 dhtmlx-gantt가 요구하는 평탄화된 리스트로 변환합니다.
  /// </summary>
  /// <param name="nodes">변환할 트리 노드 목록</param>
  /// <param name="parentId">부모 노드의 ID</param>
  /// <returns>평탄화된 WBS DTO 리스트</returns>
  private static List<WbsGanttTaskDto> FlattenWbsTree(List<WbsTreeNode> nodes, int? parentId) {
    var flatList = new List<WbsGanttTaskDto>();
    foreach (var node in nodes) {
      flatList.Add(new WbsGanttTaskDto {
        Id = node.Data.WbsRid,
        Text = node.Data.WbsName,
        StartDate = node.Data.PlanStart?.ToString("yyyy-MM-dd"),
        EndDate = node.Data.PlanEnd?.ToString("yyyy-MM-dd"),
        Progress = node.Data.Progress / 100, // 0-100 범위를 0-1로 변환
        Parent = parentId,
        WbsCode = node.Data.WbsCode,
        Status = node.Data.Status,
        Priority = node.Data.Priority,
        ProjectId = node.Data.ProjectId, // ProjectId 추가
        OrderIndex = node.Data.OrderIndex, // OrderIndex 추가
        ParentWbsId = node.Data.ParentWbsId
      });

      if (node.Children.Any()) {
        flatList.AddRange(FlattenWbsTree(node.Children, node.Data.WbsRid));
      }
    }
    return flatList;
  }

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

    // 모든 WBS 항목을 평탄화된 리스트로 조회 (Gantt 차트용)
    group.MapGet("/flat", (AppDbContext db, [FromQuery] int? projectId) => {
      return ApiResponseBuilder.CreateAsync(async () => {
        // 1. 모든 항목을 가져와 트리 구조로 재구성 (기존 로직과 동일)
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
          if (wbs.ParentWbsId.HasValue && allNodes.TryGetValue(wbs.ParentWbsId.Value, out var parentNode)) {
            parentNode.Children.Add(allNodes[wbs.WbsRid]);
          }
          else {
            rootNodes.Add(allNodes[wbs.WbsRid]);
          }
        }

        var flatData = FlattenWbsTree(rootNodes, null);

        // WbsLink 데이터도 함께 조회
        var links = await db.WbsLinks
            .Where(l => l.SourceWbs.ProjectId == projectId.Value || l.TargetWbs.ProjectId == projectId.Value) // ProjectId로 링크 필터링
            .Select(l => new WbsLinkGanttDto {
              Id = l.Id,
              Source = l.SourceWbsId,
              Target = l.TargetWbsId,
              Type = l.Type
            })
            .ToListAsync();

        // dhtmlx-gantt가 기대하는 { data: [], links: [] } 형태로 반환
        return new { data = flatData, links = links };
      });
    });

    // 단일 WBS 항목 조회
    group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
        () => db.Wbs.Include(w => w.Project).FirstOrDefaultAsync(w => w.WbsRid == id) // Project 정보 포함
    ));

    // 새 WBS 항목 생성
    group.MapPost("/", (AppDbContext db, WbsCreateDto wbsDto) => ApiResponseBuilder.CreateAsync(async () => {
      Wbs? parent = null;
      if (wbsDto.ParentWbsId.HasValue) {
        parent = await db.Wbs.FindAsync(wbsDto.ParentWbsId.Value);
      }


      var project = await db.Projects.FindAsync(wbsDto.ProjectId); // ProjectId로 프로젝트 조회

      var wbs = new Wbs {
        ParentWbs = parent,
        WbsCode = wbsDto.WbsCode ?? wbsDto.WbsName,
        WbsName = wbsDto.WbsName,
        WbsType = wbsDto.WbsType,
        WbsLevel = wbsDto.WbsLevel,
        PlanStart = wbsDto.PlanStart ?? parent?.PlanStart ?? project?.ProjectStart ?? DateTime.Now,
        PlanEnd = wbsDto.PlanEnd ?? parent?.PlanEnd ?? project?.ProjectEnd ?? DateTime.Now.AddDays(7),
        Progress = wbsDto.Progress ?? 0,
        Priority = wbsDto.Priority,
        Status = wbsDto.Status,
        ProjectId = wbsDto.ProjectId, // ProjectId 저장
        OrderIndex = wbsDto.OrderIndex // OrderIndex 추가
      };
      db.Wbs.Add(wbs);
      await db.SaveChangesAsync();
      return wbs;
    }, "WBS created successfully.", 201));

    // 기존 WBS 항목 수정
    group.MapPut("/{id}", (AppDbContext db, int id, WbsUpdateDto input) => ApiResponseBuilder.CreateAsync(async () => {
      var wbs = await db.Wbs
          .Include(w => w.ParentWbs) // 부모 WBS를 함께 조회합니다.
          .FirstOrDefaultAsync(w => w.WbsRid == id);
      if (wbs is null) return null;

      wbs.WbsCode = input.WbsCode;
      wbs.WbsName = input.WbsName;
      wbs.WbsType = input.WbsType;
      wbs.WbsLevel = input.WbsLevel;
      wbs.PlanStart = input.PlanStart;
      wbs.PlanEnd = input.PlanEnd;
      wbs.ActualStart = input.ActualStart;
      wbs.ActualEnd = input.ActualEnd;
      wbs.Progress = input.Progress;
      wbs.Priority = input.Priority;
      wbs.Status = input.Status;
      //wbs.ProjectId = input.ProjectId; // ProjectId 수정
      wbs.OrderIndex = input.OrderIndex; // 
      wbs.ParentWbsId = input.ParentWbsId;

      await db.SaveChangesAsync();


      // 부모의 날짜와 진행률을 재귀적으로 업데이트합니다.
      await AdjustChildDatesRecursive(db, wbs);
      await UpdateParentProgressRecursive(db, wbs);

      await db.SaveChangesAsync();


      return wbs;
    }, "WBS updated successfully."));

    // WBS 항목 삭제
    group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
      var wbs = await db.Wbs.FindAsync(id);
      if (wbs is null) return null;

      // 자식 노드가 있는 경우 삭제 정책이 필요합니다.
      // 여기서는 간단히 단일 노드만 삭제합니다.
      db.Wbs.Remove(wbs);
      await db.SaveChangesAsync();
      return new { DeletedId = id };
    }, "WBS deleted successfully."));
  }

  /// <summary>
  /// 특정 WBS 항목의 변경에 따라 부모 WBS의 진행률을 재귀적으로 업데이트합니다.
  /// </summary>
  /// <param name="db">데이터베이스 컨텍스트</param>
  /// <param name="currentWbs">시작 WBS 항목</param>
  private static async Task UpdateParentProgressRecursive(AppDbContext db, Wbs? currentWbs) {

    Console.WriteLine($"Updating progress for WBS ID {currentWbs?.WbsRid}: WbsName ={currentWbs?.WbsName}, Progress={currentWbs?.Progress}");
    // 현재 WBS나 부모가 없으면 재귀를 중단합니다.
    if (currentWbs?.ParentWbs == null) {
      Console.WriteLine("No parent WBS found. Stopping recursion.");
      return;
    }

    var parent = currentWbs.ParentWbs;

    // 부모의 모든 자식(형제)들의 진행률을 가져옵니다.
    var siblingProgresses = await db.Wbs
                .Include(w => w.ParentWbs) // 부모 WBS를 함께 조회합니다.
                .Where(w => w.ParentWbs.WbsRid == currentWbs.ParentWbs.WbsRid)
        .Select(w => w.Progress)
        .ToListAsync();

    Console.WriteLine($"xxxxxxxxxxxxxxxxxx count: {siblingProgresses.Count}");



    // 평균을 계산하여 부모의 진행률을 업데이트합니다.
    if (siblingProgresses.Any()) {
      Console.WriteLine($"Sibling progresses: {string.Join(", ", siblingProgresses)}");
      parent.Progress = siblingProgresses.Average();
    }

    // 부모의 부모를 업데이트하기 위해 재귀 호출을 합니다.
    await UpdateParentProgressRecursive(db, parent);
  }

  /// <summary>
  /// 부모 WBS의 날짜 변경에 따라 자식 WBS의 날짜를 재귀적으로 조정합니다.
  /// </summary>
  /// <param name="db">데이터베이스 컨텍스트</param>
  /// <param name="parentWbs">부모 WBS 항목</param>
  private static async Task AdjustChildDatesRecursive(AppDbContext db, Wbs parentWbs) {

    Console.WriteLine($"Adjusting children of WBS ID {parentWbs.WbsRid}: WbsName ={parentWbs.WbsName}, PlanStart={parentWbs.PlanStart}, PlanEnd={parentWbs.PlanEnd}");

    var children = await db.Wbs.Where(w => w.ParentWbs != null && w.ParentWbs.WbsRid == parentWbs.WbsRid).ToListAsync();

    foreach (var child in children) {
      bool modified = false;

      // 부모의 시작일자가 자식의 시작일자 보다 작은 경우
      if (parentWbs.PlanStart.HasValue && child.PlanStart.HasValue && child.PlanStart < parentWbs.PlanStart) {
        if (child.PlanEnd.HasValue) {
          var duration = child.PlanEnd.Value - child.PlanStart.Value;
          child.PlanEnd = parentWbs.PlanStart.Value.Add(duration);
        }
        child.PlanStart = parentWbs.PlanStart;
        modified = true;
      }

      if (parentWbs.PlanEnd.HasValue && child.PlanEnd.HasValue && child.PlanEnd > parentWbs.PlanEnd) {
        child.PlanEnd = parentWbs.PlanEnd;
        modified = true;
      }


      /*
            // 자식의 시작/종료일이 유효하지 않게 된 경우 (e.g., 종료일 < 시작일)
            if (child.PlanStart.HasValue && child.PlanEnd.HasValue && child.PlanStart > child.PlanEnd) {
              child.PlanEnd = child.PlanStart; // 최소한 시작일과 같게 조정
              modified = true;
            }
      */

      /*
            // 시작일자과 종료일자가 24시간 이내로 같으면 종료일자는 +2 일 처리
            if (child.PlanStart.HasValue && child.PlanEnd.HasValue && child.PlanStart <= child.PlanEnd.Value.AddDays(1)) {
              child.PlanEnd = child.PlanStart.Value.AddDays(2); // 종료일을 시작일보다 1시간 뒤로 설정
              modified = true;
            }
      */


      // 시작일자과 종료일자가 같으면 종료일자는 +1 일 처리
      // if (child.PlanStart.HasValue && child.PlanEnd.HasValue && child.PlanStart == child.PlanEnd)
      // {
      //     child.PlanEnd = child.PlanEnd.Value.AddDays(1); // 종료일을 시작일보다 1시간 뒤로 설정
      //     modified = true;
      // }

      // 자식의 날짜가 변경되었다면, 그 자식의 자식들도 재귀적으로 조정합니다.
      if (modified) {
        await AdjustChildDatesRecursive(db, child);
      }
    }
  }
}
