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

    group.MapGet("/", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      var loginType = http.User.Claims.FirstOrDefault(c => c.Type == "login_type")?.Value;

      Console.WriteLine($"/api/customers loginType : {loginType}");



      var query = db.Customers.AsQueryable();

      if (loginType == "customer") {
        var companyIdClaim = http.User.Claims.FirstOrDefault(c => c.Type == "company_id")?.Value;
        if (!string.IsNullOrEmpty(companyIdClaim) && int.TryParse(companyIdClaim, out var companyId)) {
          query = query.Where(c => c.CompanyId == companyId);
        }
        else {
          // 회사 ID가 없는 고객은 빈 목록을 반환합니다.
          return new List<object>().AsEnumerable();
        }
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

    group.MapPost("/", (AppDbContext db, CustomerCreateDto customerDto, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      // 고객 등록은 '조직 데이터' 등록이지 계정 발급이 아니다(결정 Q4).
      // 로그인 계정은 JSini 포털에서 만든다. 비밀번호 칸은 필수 컬럼이라
      // 아무도 모르는 임의값으로 채운다 — 이 값으로는 로그인할 수 없다.
      var passwordService = new PasswordService();
      var unusablePassword = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
      var customer = new Customer {
        LoginId = customerDto.LoginId,
        UserName = customerDto.UserName,
        Email = customerDto.Email,
        CompanyId = customerDto.CompanyId,
        Sex = customerDto.Sex ?? "M",
        Photo = customerDto.Photo ?? "",
        // 등록자는 로그인한 JSini 계정에서 정한다(요청 본문 값은 쓰지 않는다).
        CreatedBy = http.AuditUser(),
        MenuContext = customerDto.MenuContext
      };
      customer.PasswordHash = passwordService.HashPassword(customer, unusablePassword);
      db.Customers.Add(customer);
      await db.SaveChangesAsync();
      return customer;
    }, "Customer created successfully.", 201));

    group.MapPut("/{id}", (AppDbContext db, int id, Customer input) => ApiResponseBuilder.CreateAsync(async () => {
      var customer = await db.Customers.FindAsync(id);
      if (customer is null) return null;

      customer.UserName = input.UserName;
      customer.LoginId = input.LoginId;
      //customer.Email = input.Email;
      //customer.Sex = input.Sex;
      //customer.Photo = input.Photo;
      customer.CompanyId = input.CompanyId;

      await db.SaveChangesAsync();
      return customer;
    }, "Customer updated successfully."));

    group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
      var customer = await db.Customers.FindAsync(id);
      if (customer is null) return null;
      else if (customer.UserName.Contains("pub_")) { // pub_ 으로 시작하는 아이디 막기. 회사공동아이디 인데 일딴 삭제 불가하도록.
        return null;
      }

      // Soft delete: set IsDeleted to true instead of removing from DB
      customer.IsDeleted = true;
      await db.SaveChangesAsync();
      return new { DeletedId = id };
    }, "Customer deleted successfully."));
  }
}
