using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using System.ComponentModel.DataAnnotations;
using HelpDeskServer.Services;
using HelpDeskServer.Dtos;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using System.Linq.Dynamic.Core;
using HelpDeskServer.Dtos;
using HelpDeskServer.Helpers;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.ComponentModel;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace HelpDeskServer.Endpoints;

/// <summary>
/// 고객(사용자) 관련 엔드포인트
/// </summary>
public static class CustomerEndpoints {
  //public record CustomerCreateDto([Required] string LoginId, [Required] string UserName, [Required] string Email, [Required] string Password, int CompanyId, string? Sex, string? Photo, string? CreatedBy, string? MenuContext);

  /// <summary>
  /// 고객(사용자) 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapCustomerEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/customers");

    // 고객 목록. 볼 수 있는 범위는 신원이 정한다.
    //
    // 전에는 login_type 이 "customer" 일 때만 회사로 좁히고 **그 밖에는 전부 반환**했다.
    // 계정 연결이 없는 포털 계정에는 login_type 자체가 없으므로, 권한이 없는 사용자가
    // 고객 27명 전원을 그대로 받아 갔다. 판정하지 못했을 때 열리는 방향은 반대여야 한다.
    group.MapGet("/", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      var me = http.GetHelpdeskPrincipal();

      var query = db.Customers.AsQueryable();

      if (me.IsAdmin) {
        // 담당자는 전체를 본다. 연결이 없어도 포털 관리자 역할이면 여기에 해당한다.
      }
      else if (me.IsCustomer && me.CompanyId.HasValue) {
        // 고객은 자기 회사만 본다.
        query = query.Where(c => c.CompanyId == me.CompanyId.Value);
      }
      else {
        // 담당자도 아니고 회사를 알 수 있는 고객도 아니다 — 아무것도 주지 않는다.
        return new List<object>().AsEnumerable();
      }

      return (await query.Select(u => new { u.Id, u.UserName, u.LoginId, u.Sex, u.Photo, u.Email, u.CompanyId }).ToListAsync()).AsEnumerable();
    })).RequireAuthorization();




    // 검색
    group.MapPost("/srch", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      using var reader = new StreamReader(http.Request.Body);
      var body = await reader.ReadToEndAsync();

      IQueryCollection bodyQuery = new QueryCollection();
      if (!string.IsNullOrWhiteSpace(body)) {
        bodyQuery = JsonToQueryHelper.ConvertJsonToQuery(body);
      }

      var finalQuery = QueryCollectionMerger.Merge(http.Request.Query, bodyQuery);

      var queryWithIncludes = db.Customers
          .AsQueryable();

      var countQueryDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
      foreach (var kvp in finalQuery) {
        if (!kvp.Key.Equals("page", StringComparison.OrdinalIgnoreCase) &&
            !kvp.Key.Equals("pageSize", StringComparison.OrdinalIgnoreCase)) {
          countQueryDict[kvp.Key] = kvp.Value;
        }
      }
      var countQueryCollection = new QueryCollection(countQueryDict);

      var countQueryWithFilters = queryWithIncludes.ApplyAll(countQueryCollection);
      var countList = await (countQueryWithFilters is IQueryable<object> cq ? cq.ToDynamicListAsync() : ((IQueryable)countQueryWithFilters).ToDynamicListAsync());
      var totalCount = countList.Count;
      var resultQuery = queryWithIncludes.ApplyAll(finalQuery);

      var requests = await (resultQuery is IQueryable<object> q ? q.ToDynamicListAsync() : ((IQueryable)resultQuery).ToDynamicListAsync());

      var data = requests.ToList();

      int pageSize = 0;
      if (finalQuery.TryGetValue("pageSize", out var psVal) && int.TryParse(psVal, out var ps)) {
        pageSize = Math.Max(0, ps);
      }

      int totalPageCount = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

      return new {
        data = data,
        totalpagecount = totalPageCount,
        totalcount = totalCount
      };

    }, "Request srch successfully.", 201));





    group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
        () => db.Customers
            .Where(c => c.Id == id)
            .Select(u => new { u.Id, u.UserName, u.LoginId, u.Sex, u.Photo, u.Email, u.CompanyId })
            .FirstOrDefaultAsync()
    ));

    // 등록·수정·삭제 엔드포인트는 제거했다.
    //
    // 고객 사용자 **관리 화면(`/helpdesk/org/customer`)이 없어졌다.** 이식본에만 있던
    // 화면이고, 계정과 사람 정보는 JSini 관리 포털이 단독으로 맡는다.
    // 쓰는 화면이 없는 쓰기 통로를 열어 둘 이유가 없다.
    //
    // 조회(`GET /`)는 남긴다 — 요청 화면들의 고객 셀렉트와 계정 대조 화면이 쓴다.
    // 고객을 '관리'하는 것이 아니라 업무 데이터에서 **가리키기 위한** 읽기다.
    //
    // 헬프데스크 DB(`jsini.customer`)는 손대지 않았다. 기존 요청·댓글이 이 행들을
    // 참조하고 있어 그대로 있어야 한다.
  }
}
