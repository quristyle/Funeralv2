using HelpDeskServer.Models;
using HelpDeskServer.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Dtos;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 대시보드 통계 관련 엔드포인트
/// </summary>
public static class DashboardEndpoints {
  /// <summary>
  /// 대시보드 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapDashboardEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/dashboard");

    // 고객사별 요청 통계를 조회합니다.
    group.MapGet("/company-stats", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
      var stats = await db.Requests
          .Where(r => r.Customer != null && r.Customer.Company != null)
          .GroupBy(r => r.Customer.Company)
          .Select(g => new {
            Id = g.Key.Id,
            CompanyName = g.Key.Name,
            LastPendingDate = g.Where(r => r.Status == ImprovementStatus.Pending)
                              .OrderByDescending(r => r.CreatedAt)
                              .Select(r => (DateTime?)r.CreatedAt)
                              .FirstOrDefault(),
            PendingCount = g.Count(r => r.Status == ImprovementStatus.Pending),
            InProgressCount = g.Count(r => r.Status == ImprovementStatus.InProgress),
            ConsultationCount = g.Count(r => r.Status == ImprovementStatus.Consultation),
            NegotiationCount = g.Count(r => r.Status == ImprovementStatus.Negotiation),
            CompletedCount = g.Count(r => r.Status == ImprovementStatus.Completed),
            RejectedCount = g.Count(r => r.Status == ImprovementStatus.Rejected),
            TotalCount = g.Count()
          })
          .ToListAsync();

      return stats.Select(s => {
        double completionRate = (s.TotalCount - s.RejectedCount) > 0
            ? Math.Round((double)s.CompletedCount / (s.TotalCount - s.RejectedCount) * 100, 1)
            : 0;

        return new CompanyStatsDto {
          Id = s.Id,
          CompanyName = s.CompanyName,
          LastPendingDate = s.LastPendingDate,
          PendingCount = s.PendingCount,
          InProgressCount = s.InProgressCount,
          ConsultationCount = s.ConsultationCount,
          NegotiationCount = s.NegotiationCount,
          CompletedCount = s.CompletedCount,
          RejectedCount = s.RejectedCount,
          CompletionRate = completionRate
        };
      }).ToList();
    }));

    // 현재 로그인한 관리자의 업무 통계를 조회합니다.
    //
    // 담당자 권한이면 연결이 없어도 조회할 수 있다. 다만 "본인 배정" 은 헬프데스크 레코드가
    // 있어야 세는 것이라, 연결이 없으면 그 칸은 0 이 된다. 대신 미배정 건수는 팀이 아니라
    // 전체를 세어 준다 — 팀이 없는 사람에게 0 만 보여 주는 것보다 관리 조회에 쓸모가 있다.
    // 화면이 상황을 알 수 있도록 linked / adminRecord 를 함께 내려보낸다.
    group.MapGet("/admin-stats", async (HttpContext http, AppDbContext db) => {
      var me = http.GetHelpdeskPrincipal();
      if (!me.IsAdmin) {
        return Results.Json(
            new { success = false, message = "담당자 권한이 필요합니다.", data = (object?)null },
            statusCode: StatusCodes.Status403Forbidden);
      }

      // 연결된 담당자 레코드가 있을 때만 '본인' 을 특정할 수 있다.
      var adminId = me.IsLinkedAdmin ? me.HelpdeskUserId : null;

      int pendingCount;
      if (adminId.HasValue) {
        // 1. 관리자가 속한 팀 ID 목록 가져오기
        var teamIds = await db.AdminTeams
                             .Where(at => at.AdminId == adminId.Value)
                             .Select(at => at.TeamId)
                             .ToListAsync();

        // 2. 해당 팀들이 관리하는 업체(Company) ID 목록 가져오기 (N:N 관계인 TeamCompanies 활용)
        var managedCompanyIds = await db.TeamCompanies
                                        .Where(tc => teamIds.Contains(tc.TeamId))
                                        .Select(tc => tc.CompanyId)
                                        .Distinct()
                                        .ToListAsync();

        // 3. 해당 업체들의 미배정(Pending) 요청 건수 집계
        pendingCount = await db.Requests
                                   .CountAsync(r => r.Status == ImprovementStatus.Pending &&
                                                   r.Customer != null &&
                                                   managedCompanyIds.Contains(r.Customer.CompanyId));
      }
      else {
        // 팀을 알 수 없다. 전체 미배정 건수를 준다.
        pendingCount = await db.Requests.CountAsync(r => r.Status == ImprovementStatus.Pending);
      }

      // 4. 본인 배정된 요청들 집계 (진행, 완료 등). 연결이 없으면 셀 대상이 없다.
      var grouped = adminId.HasValue
          ? await db.Requests
                    .Where(r => r.AdminId == adminId.Value)
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync()
          : [];

      // 매핑
      var map = grouped.ToDictionary(g => g.Status, g => g.Count);
      var inProgressCount = map.TryGetValue(ImprovementStatus.InProgress, out var v1) ? v1 : 0;
      var completedCount = map.TryGetValue(ImprovementStatus.Completed, out var v2) ? v2 : 0;
      var rejectedCount = map.TryGetValue(ImprovementStatus.Rejected, out var v3) ? v3 : 0;
      var consultationCount = map.TryGetValue(ImprovementStatus.Consultation, out var v4) ? v4 : 0;
      var negotiationCount = map.TryGetValue(ImprovementStatus.Negotiation, out var v5) ? v5 : 0;
      var userCompletedCount = map.TryGetValue(ImprovementStatus.UserCompleted, out var v6) ? v6 : 0;

      var stats = new AdminStatsDto {
        PendingCount = pendingCount, // 팀 관리 업체들의 미배정 건수
        InProgressCount = inProgressCount,
        CompletedCount = completedCount,
        UserCompletedCount = userCompletedCount,
        RejectedCount = rejectedCount,
        ConsultationCount = consultationCount,
        NegotiationCount = negotiationCount,
        TotalRequests = await db.Requests.CountAsync()
      };

      // 연결 여부를 함께 알려 준다. 화면은 "본인 배정 0" 이 실제로 0 인지,
      // 아니면 연결이 없어 셀 수 없었던 것인지 구분해야 한다.
      return Results.Ok(new {
        success = true,
        data = stats,
        linked = adminId.HasValue,
        pendingScope = adminId.HasValue ? "team" : "all"
      });

    }).RequireAuthorization();

    // 모든 관리자의 업무 통계 및 순위를 조회합니다.
    group.MapGet("/all-admin-stats", async (AppDbContext db) => {
      var totalRequests = await db.Requests.CountAsync();

      // 모든 관리자와 그들의 통계를 가져옵니다.
      var admins = await db.Admins
          .Include(a => a.AdminTeams)
          .ToListAsync();

      var teamCompanies = await db.TeamCompanies.ToListAsync();

      var adminStats = new List<AllAdminStatsDto>();

      foreach (var admin in admins) {
        var adminId = admin.Id;

        // 1. 관리자가 속한 팀 ID 목록
        var teamIds = admin.AdminTeams.Select(at => at.TeamId).ToList();

        // 2. 해당 팀들이 관리하는 업체 ID 목록
        var managedCompanyIds = teamCompanies
            .Where(tc => teamIds.Contains(tc.TeamId))
            .Select(tc => tc.CompanyId)
            .Distinct()
            .ToList();

        // 3. 대기 건수 (해당 업체들의 Pending 건수)
        var pendingCount = await db.Requests
            .CountAsync(r => r.Status == ImprovementStatus.Pending && 
                             r.Customer != null && 
                             managedCompanyIds.Contains(r.Customer.CompanyId));

        var grouped = await db.Requests
            .Where(r => r.AdminId == adminId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var map = grouped.ToDictionary(g => g.Status, g => g.Count);

        var inProgressCount = map.GetValueOrDefault(ImprovementStatus.InProgress, 0);
        var completedCount = map.GetValueOrDefault(ImprovementStatus.Completed, 0) + map.GetValueOrDefault(ImprovementStatus.UserCompleted, 0);
        var rejectedCount = map.GetValueOrDefault(ImprovementStatus.Rejected, 0);
        var consultationCount = map.GetValueOrDefault(ImprovementStatus.Consultation, 0);
        var negotiationCount = map.GetValueOrDefault(ImprovementStatus.Negotiation, 0);

        var totalHandled = inProgressCount + completedCount + rejectedCount + consultationCount + negotiationCount;

        adminStats.Add(new AllAdminStatsDto {
          AdminId = adminId,
          AdminName = admin.UserName,
          AdminPhoto = admin.Photo,
          PendingCount = pendingCount,
          InProgressCount = inProgressCount,
          CompletedCount = completedCount,
          RejectedCount = rejectedCount,
          ConsultationCount = consultationCount,
          NegotiationCount = negotiationCount,
          TotalHandled = totalHandled,
          AcceptanceRate = totalRequests > 0 ? Math.Round((double)totalHandled / totalRequests * 100, 1) : 0,
          CompletionRate = totalHandled > 0 ? Math.Round((double)completedCount / totalHandled * 100, 1) : 0
        });
      }

      var sortedStats = adminStats
          .OrderByDescending(s => s.AcceptanceRate)
          .ThenByDescending(s => s.CompletionRate)
          .ToList();

      return Results.Ok(new { success = true, data = sortedStats });
    }).RequireAuthorization();

    // 모든 관리자의 기여도 통계 (전체 기간 및 선택 월)
    group.MapGet("/admin-contribution-stats", async (AppDbContext db, int year, int month) => {
      var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
      var endDate = startDate.AddMonths(1);

      // 1. 전체 기간 완료 건수 합계
      var totalLifetimeResolved = await db.Requests
          .CountAsync(r => r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted);

      // 2. 선택 월 완료 건수 합계
      var totalMonthlyResolved = await db.Requests
          .CountAsync(r => (r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted) &&
                           ((r.CompletededAt >= startDate && r.CompletededAt < endDate) || 
                            (r.UserCompletededAt >= startDate && r.UserCompletededAt < endDate)));

      var adminStats = await db.Admins
          .Select(admin => new {
            admin.Id,
            admin.UserName,
            // 전체 기간 완료 건수
            LifetimeCompleted = db.Requests.Count(r => r.AdminId == admin.Id && 
                                                      (r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted)),
            // 선택 월 완료 건수
            MonthlyCompleted = db.Requests.Count(r => r.AdminId == admin.Id && 
                                                     ((r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted)) &&
                                                     ((r.CompletededAt >= startDate && r.CompletededAt < endDate) || 
                                                      (r.UserCompletededAt >= startDate && r.UserCompletededAt < endDate))),
            // 현재 진행중 건수 (월 무관 실시간)
            InProgressCount = db.Requests.Count(r => r.AdminId == admin.Id && r.Status == ImprovementStatus.InProgress)
          })
          .ToListAsync();

      var result = adminStats.Select(s => new {
        AdminId = s.Id,
        AdminName = s.UserName,
        LifetimeCompleted = s.LifetimeCompleted,
        MonthlyCompleted = s.MonthlyCompleted,
        InProgressCount = s.InProgressCount,
        LifetimeShare = totalLifetimeResolved > 0 ? Math.Round((double)s.LifetimeCompleted / totalLifetimeResolved * 100, 1) : 0,
        MonthlyShare = totalMonthlyResolved > 0 ? Math.Round((double)s.MonthlyCompleted / totalMonthlyResolved * 100, 1) : 0,
        ResolutionPower = (s.InProgressCount + s.MonthlyCompleted) > 0 
            ? Math.Round((double)s.MonthlyCompleted / (s.InProgressCount + s.MonthlyCompleted) * 100, 1) 
            : 0
      }).OrderByDescending(r => r.MonthlyShare).ToList();

      return Results.Ok(new { success = true, data = result });
    }).RequireAuthorization();

    // 담당자별 지난 12개월간 주단위 해결 비중 추이
    group.MapGet("/admin-contribution-trend", async (AppDbContext db) => {
      var today = DateTime.UtcNow.Date;
      // 최근 12개월(약 52주) 전 월요일부터 시작
      int daysToMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
      var currentWeekMonday = today.AddDays(-daysToMonday);
      var startDate = currentWeekMonday.AddDays(-7 * 51); // 52주 데이터

      // 1. 해당 기간의 모든 완료된 요청 조회
      var requests = await db.Requests
          .Where(r => (r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted) &&
                      ((r.CompletededAt >= startDate) || (r.UserCompletededAt >= startDate)))
          .Select(r => new {
            r.AdminId,
            Date = r.CompletededAt ?? r.UserCompletededAt
          })
          .ToListAsync();

      // 2. 관리자 목록 조회
      var admins = await db.Admins.Select(a => new { a.Id, a.UserName }).ToListAsync();

      // 3. 주별/관리자별 집계 (최근 52주)
      var result = new List<object>();
      for (int i = 0; i < 52; i++) {
        var weekStart = startDate.AddDays(i * 7);
        var weekEnd = weekStart.AddDays(7);
        var label = $"{weekStart:MM/dd}"; // "03/23" 형식의 라벨
        
        var weeklyTotal = requests.Count(r => r.Date >= weekStart && r.Date < weekEnd);
        
        var adminData = admins.Select(a => {
          var adminCount = requests.Count(r => r.AdminId == a.Id && r.Date >= weekStart && r.Date < weekEnd);
          return new {
            AdminName = a.UserName,
            Count = adminCount,
            Share = weeklyTotal > 0 ? Math.Round((double)adminCount / weeklyTotal * 100, 1) : 0
          };
        }).ToList();

        result.Add(new {
          Week = label,
          Total = weeklyTotal,
          Admins = adminData
        });
      }

      return Results.Ok(new { success = true, data = result });
    }).RequireAuthorization();

    // 현재 로그인한 고객(사용자)이 속한 회사의 요청 통계를 조회합니다.
    //
    // ⚠ 전에는 uid 를 login_type 확인 없이 고객 ID 로 썼다. uid 는 담당자에게도 붙는 값이라,
    //    담당자 #4 가 부르면 **고객 #4** 의 회사 통계가 나왔다(운영 데이터에서 재현됨).
    //    서로 다른 사람의 자료다. 그래서 고객으로 연결된 계정만 받는다.
    group.MapGet("/my-company-stats", async (HttpContext http, AppDbContext db) => {
      var me = http.GetHelpdeskPrincipal();
      if (!me.IsCustomer || !me.HelpdeskUserId.HasValue) {
        return Results.Json(
            new { success = false, message = "고객으로 연결된 계정만 조회할 수 있습니다.", data = (object?)null },
            statusCode: StatusCodes.Status403Forbidden);
      }

      var customer = await db.Customers.FindAsync(me.HelpdeskUserId.Value);
      if (customer == null || customer.CompanyId == null) {
        return Results.NotFound("Customer or company not found.");
      }

      var companyId = customer.CompanyId;

      var stats = await db.Requests
          .Where(r => r.Customer.CompanyId == companyId)
          .GroupBy(r => r.Status)
          .Select(g => new { Status = g.Key, Count = g.Count() })
          .ToListAsync();

      var result = new MyCompanyStatsDto {
        PendingCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.Pending)?.Count ?? 0,
        InProgressCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.InProgress)?.Count ?? 0,
        CompletedCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.Completed)?.Count ?? 0,
        UserCompletedCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.UserCompleted)?.Count ?? 0,
        RejectedCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.Rejected)?.Count ?? 0,
        ConsultationCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.Consultation)?.Count ?? 0,
        NegotiationCount = stats.FirstOrDefault(s => s.Status == ImprovementStatus.Negotiation)?.Count ?? 0
      };
      result.TotalCount = result.PendingCount + result.InProgressCount + result.CompletedCount + result.RejectedCount + result.ConsultationCount + result.NegotiationCount;

      return Results.Ok(new { success = true, data = result });
    }).RequireAuthorization();

    // 현재 로그인한 고객(사용자)이 속한 회사의 지난 12개월간 월별 요청 통계를 조회합니다.
    // my-company-stats 와 같은 문제가 있었다 — 위 주석 참고.
    group.MapGet("/my-monthly-stats", async (HttpContext http, AppDbContext db) => {
      var me = http.GetHelpdeskPrincipal();
      if (!me.IsCustomer || !me.HelpdeskUserId.HasValue) {
        return Results.Json(
            new { success = false, message = "고객으로 연결된 계정만 조회할 수 있습니다.", data = (object?)null },
            statusCode: StatusCodes.Status403Forbidden);
      }

      var customer = await db.Customers.FindAsync(me.HelpdeskUserId.Value);
      if (customer == null || customer.CompanyId == null) {
        return Results.NotFound("Customer or company not found.");
      }

      var companyId = customer.CompanyId;
      var today = DateTime.UtcNow;
      var twelveMonthsAgo = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

      var monthlyData = await db.Requests
          .Where(r => r.Customer.CompanyId == companyId && r.RequestedAt >= twelveMonthsAgo)
          .GroupBy(r => new { r.RequestedAt.Year, r.RequestedAt.Month })
          .Select(g => new {
            Year = g.Key.Year,
            Month = g.Key.Month,
            TotalCount = g.Count(),
            CompletedCount = g.Count(r => r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted)
          })
          .ToListAsync();

      var result = new List<MonthlyStatsDto>();
      for (int i = 0; i < 12; i++) {
        var targetMonth = twelveMonthsAgo.AddMonths(i);
        var data = monthlyData.FirstOrDefault(d => d.Year == targetMonth.Year && d.Month == targetMonth.Month);
        
        result.Add(new MonthlyStatsDto {
          Month = targetMonth.ToString("yyyy-MM"),
          TotalCount = data?.TotalCount ?? 0,
          CompletedCount = data?.CompletedCount ?? 0
        });
      }

      return Results.Ok(new { success = true, data = result });
    }).RequireAuthorization();

    // 모든 요청의 상태별 개수를 조회합니다.
    group.MapGet("/requests/status-count", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Requests.GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync()));

    // 각 고객사별 요청 수를 조회합니다.
    group.MapGet("/companies/requests", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Companies
            .Select(c => new {
              c.Id,
              c.Name,
              RequestCount = db.Requests.Count(r => r.Customer.CompanyId == c.Id)
            }).ToListAsync()));

    // 각 팀별 할당된 요청 수를 조회합니다.
    group.MapGet("/teams/workload", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Teams
            .Select(t => new {
              t.Id,
              t.Name,
              AssignedRequests = db.Requests.Count(r => r.Admin != null && r.Admin.AdminTeams.Any(at => at.TeamId == t.Id))
            }).ToListAsync()));

    // 각 관리자별 할당된 요청 및 완료한 요청 수를 조회합니다.
    group.MapGet("/admins/workload", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Admins
            .Select(a => new {
              a.Id,
              a.UserName,
              AssignedRequests = db.Requests.Count(r => r.AdminId == a.Id),
              CompletedRequests = db.Requests.Count(r => r.AdminId == a.Id && r.Status == ImprovementStatus.Completed)
            }).ToListAsync()));

    // 각 요청별 덧글 수를 조회합니다.
    group.MapGet("/requests/comments", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Requests
            .Select(r => new {
              r.Id,
              r.Title,
              CommentCount = db.Comments.Count(c => c.RequestId == r.Id)
            }).ToListAsync()));

    // 최근 N개의 요청을 조회합니다.
    group.MapGet("/requests/recent", (AppDbContext db, int topN) => ApiResponseBuilder.CreateAsync(
        () => db.Requests.OrderByDescending(r => r.CreatedAt).Take(topN).ToListAsync()));

    // 엔티티 타입별 첨부파일 수를 조회합니다.
    group.MapGet("/attachments/entity", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
        () => db.Attachments.GroupBy(a => a.EntityType)
            .Select(g => new { EntityType = g.Key, Count = g.Count() })
            .ToListAsync()));

    // 특정 프로젝트의 WBS 통계를 조회합니다.
    group.MapGet("/project-stats/{projectId}", async (AppDbContext db, int projectId) => {
      return await ApiResponseBuilder.CreateAsync(async () => {
        var project = await db.Projects
                            .Include(p => p.Team)
                            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) {
          // Throwing an exception that will be caught by ApiResponseBuilder
          throw new Exception($"Project with ID {projectId} not found.");
        }

        var wbsStats = await db.Wbs
            .Where(w => w.ProjectId == projectId)
            .GroupBy(w => 1) // Group all wbs for the project
            .Select(g => new {
              TotalWbsCount = g.Count(),
              InProgressWbsCount = g.Count(w => w.Progress > 0 && w.Progress < 100),
              CompletedWbsCount = g.Count(w => w.Progress == 100),
              PendingWbsCount = g.Count(w => w.Progress == 0),
              OverallProgress = (double)g.Average(w => w.Progress)
            })
            .FirstOrDefaultAsync();

        var stats = new ProjectDashboardStatsDto {
          ProjectName = project.Name,
          TeamName = project.Team?.Name,
          StartDate = project.ProjectStart,
          EndDate = project.ProjectEnd,
          TotalWbsCount = wbsStats?.TotalWbsCount ?? 0,
          InProgressWbsCount = wbsStats?.InProgressWbsCount ?? 0,
          CompletedWbsCount = wbsStats?.CompletedWbsCount ?? 0,
          PendingWbsCount = wbsStats?.PendingWbsCount ?? 0,
          OverallProgress = wbsStats != null ? Math.Round(wbsStats.OverallProgress, 2) : 0
        };

        return stats;
      });
    });

    // 푸시 알림 발송 성공률 통계를 조회합니다.
    group.MapGet("/push-stats", (AppDbContext db, [FromQuery] int days = 7) => ApiResponseBuilder.CreateAsync(async () => {
      var since = DateTime.UtcNow.AddDays(-days);

      var stats = await db.PushNotificationLogs
          .Where(log => log.CreatedAt >= since)
          .GroupBy(log => 1) // 모든 로그를 단일 그룹으로 집계
          .Select(g => new {
            TotalAttempts = g.Count(),
            SuccessCount = g.Count(l => l.IsSuccess),
            FailureCount = g.Count(l => !l.IsSuccess)
          })
          .FirstOrDefaultAsync();

      var totalAttempts = stats?.TotalAttempts ?? 0;
      var successCount = stats?.SuccessCount ?? 0;
      var failureCount = stats?.FailureCount ?? 0;
      double successRate = totalAttempts > 0 ? Math.Round((double)successCount / totalAttempts * 100, 2) : 0;

      return new { totalAttempts, successCount, failureCount, successRate, days };

    })).RequireAuthorization();

    // 가장 많이 발생한 푸시 알림 실패 원인을 조회합니다.
    group.MapGet("/push-failure-reasons", (AppDbContext db, [FromQuery] int days = 7, [FromQuery] int topN = 10) => ApiResponseBuilder.CreateAsync(async () => {
      var since = DateTime.UtcNow.AddDays(-days);

      var failureStats = await db.PushNotificationLogs
          .Where(log => !log.IsSuccess && log.CreatedAt >= since && log.FailureReason != null)
          .GroupBy(log => log.FailureReason)
          .Select(g => new {
            FailureReason = g.Key,
            Count = g.Count()
          })
          .OrderByDescending(s => s.Count)
          .Take(topN)
          .ToListAsync();

      return failureStats;
    })).RequireAuthorization();

    // 일별 또는 주별 푸시 알림 성공률 추이를 조회합니다.
    group.MapGet("/push-success-trend", (AppDbContext db, [FromQuery] string interval = "daily", [FromQuery] int days = 30) => ApiResponseBuilder.CreateAsync(async () => {
      var endDate = DateTime.UtcNow.Date.AddDays(1); // 오늘 날짜의 끝까지 포함
      var startDate = endDate.AddDays(-days);

      var query = db.PushNotificationLogs
          .Where(log => log.CreatedAt >= startDate && log.CreatedAt < endDate);

      if (interval.Equals("weekly", StringComparison.OrdinalIgnoreCase)) {
        // 주별 통계
        var dailyStatsForWeekly = await query
            .GroupBy(log => log.CreatedAt.Date)
            .Select(g => new {
              Date = g.Key,
              TotalAttempts = g.Count(),
              SuccessCount = g.Count(l => l.IsSuccess)
            })
            .ToListAsync();

        // C#에서 주 단위로 재그룹화
        var weeklyStats = dailyStatsForWeekly
            .GroupBy(s => System.Globalization.ISOWeek.GetYear(s.Date) * 100 + System.Globalization.ISOWeek.GetWeekOfYear(s.Date))
            .Select(g => new { Date = g.Min(s => s.Date), TotalAttempts = g.Sum(s => s.TotalAttempts), SuccessCount = g.Sum(s => s.SuccessCount) })
            .OrderBy(s => s.Date);

        return weeklyStats.Select(s => new {
          Date = s.Date.ToString("yyyy-MM-dd"),
          s.TotalAttempts,
          s.SuccessCount,
          SuccessRate = s.TotalAttempts > 0 ? Math.Round((double)s.SuccessCount / s.TotalAttempts * 100, 2) : 0
        });
      }
      else {
        // 일별 통계 (기본값)
        var dailyStats = await query
            .GroupBy(log => log.CreatedAt.Date)
            .Select(g => new {
              Date = g.Key,
              TotalAttempts = g.Count(),
              SuccessCount = g.Count(l => l.IsSuccess),
            })
            .OrderBy(s => s.Date)
            .ToListAsync();

        return dailyStats.Select(s => new {
          Date = s.Date.ToString("yyyy-MM-dd"),
          s.TotalAttempts,
          s.SuccessCount,
          SuccessRate = s.TotalAttempts > 0 ? Math.Round((double)s.SuccessCount / s.TotalAttempts * 100, 2) : 0
        });
      }
    })).RequireAuthorization();

    // 푸시 알림 참여 통계 (전송, 도달, 읽음)
    group.MapGet("/push-engagement-stats", (AppDbContext db, [FromQuery] int days = 7) => ApiResponseBuilder.CreateAsync(async () => {
      var since = DateTime.UtcNow.AddDays(-days);

      var query = db.PushMessageRecipients.Where(r => r.CreatedAt >= since);

      var totalRecipients = await query.CountAsync();
      var totalDelivered = await query.CountAsync(r => r.IsDelivered);
      var totalRead = await query.CountAsync(r => r.IsRead);

      double deliveryRate = totalRecipients > 0 ? Math.Round((double)totalDelivered / totalRecipients * 100, 2) : 0;
      double readRate = totalDelivered > 0 ? Math.Round((double)totalRead / totalDelivered * 100, 2) : 0; // 읽음 / 도달
      double openRate = totalRecipients > 0 ? Math.Round((double)totalRead / totalRecipients * 100, 2) : 0; // 읽음 / 전체

      return new {
        totalRecipients,
        totalDelivered,
        totalRead,
        deliveryRate,
        readRate,
        openRate,
        days
      };
    })).RequireAuthorization();

    // 가장 성과가 좋은(읽음 비율이 높은) 푸시 메시지 목록
    group.MapGet("/top-performing-messages", (AppDbContext db, [FromQuery] int topN = 10) => ApiResponseBuilder.CreateAsync(async () => {
      var messages = await db.PushMessages
          .Select(m => new {
            MessageId = m.Id,
            m.Title,
            m.Body,
            m.Url,
            m.CreatedAt,
            RecipientCount = m.Recipients.Count(),
            DeliveredCount = m.Recipients.Count(r => r.IsDelivered),
            ReadCount = m.Recipients.Count(r => r.IsRead)
          })
          .Where(m => m.DeliveredCount > 0) // 모수가 0인 경우는 제외
          .OrderByDescending(m => (double)m.ReadCount / m.DeliveredCount)
          .ThenByDescending(m => m.CreatedAt)
          .Take(topN)
          .ToListAsync();

      return messages.Select(m => new {
        m.MessageId,
        m.Title,
        m.Body,
        m.Url,
        m.CreatedAt,
        m.RecipientCount,
        m.DeliveredCount,
        m.ReadCount,
        ReadRate = m.DeliveredCount > 0 ? Math.Round((double)m.ReadCount / m.DeliveredCount * 100, 2) : 0
      });
    })).RequireAuthorization();

    // 사용자별 푸시 알림 참여 통계
    group.MapGet("/user-engagement-stats", (AppDbContext db, [FromQuery] int topN = 20) => ApiResponseBuilder.CreateAsync(async () => {
      var stats = await db.PushMessageRecipients
          .GroupBy(r => new { r.UserId, r.UserType })
          .Select(g => new {
            g.Key.UserId,
            g.Key.UserType,
            TotalReceived = g.Count(),
            TotalRead = g.Count(r => r.IsRead)
          })
          .OrderByDescending(s => (double)s.TotalRead / s.TotalReceived)
          .ThenByDescending(s => s.TotalRead)
          .Take(topN)
          .ToListAsync();

      var adminIds = stats.Where(s => s.UserType.Equals("admin", StringComparison.OrdinalIgnoreCase)).Select(s => s.UserId).ToList();
      var customerIds = stats.Where(s => s.UserType.Equals("customer", StringComparison.OrdinalIgnoreCase)).Select(s => s.UserId).ToList();

      var admins = await db.Admins.Where(a => adminIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => new { a.UserName, a.Photo });
      var customers = await db.Customers.Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.UserName, c.Photo });

      var result = stats.Select(s => {
        string userName = "Unknown";
        string? photo = null;

        if (s.UserType.Equals("admin", StringComparison.OrdinalIgnoreCase) && admins.TryGetValue(s.UserId, out var admin)) {
          userName = admin.UserName;
          photo = admin.Photo;
        }
        else if (s.UserType.Equals("customer", StringComparison.OrdinalIgnoreCase) && customers.TryGetValue(s.UserId, out var customer)) {
          userName = customer.UserName;
          photo = customer.Photo;
        }

        return new { s.UserId, s.UserType, UserName = userName, Photo = photo, s.TotalReceived, s.TotalRead, ReadRate = s.TotalReceived > 0 ? Math.Round((double)s.TotalRead / s.TotalReceived * 100, 2) : 0 };
      });

      return result;
    })).RequireAuthorization();

    // 푸시 알림의 모든 고유한 실패 원인을 조회합니다.
    group.MapGet("/distinct-failure-reasons", async (AppDbContext db) => {
      var reasons = await db.PushNotificationLogs
          .Where(log => log.FailureReason != null)
          .Select(log => log.FailureReason)
          .Distinct()
          .OrderBy(r => r)
          .ToListAsync();
      return Results.Ok(new { success = true, data = reasons });
    }).RequireAuthorization();

    // 푸시 알림 발송 기록을 페이징하여 조회합니다.
    group.MapGet("/push-logs", async (AppDbContext db, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? orderBy = "createdAt desc", [FromQuery] bool? isSuccess = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] string[]? reason = null) => {
      var query = db.PushNotificationLogs.AsQueryable();

      if (isSuccess.HasValue) {
        query = query.Where(log => log.IsSuccess == isSuccess.Value);
      }

      if (reason != null && reason.Length > 0) {
        query = query.Where(log => log.FailureReason != null && reason.Contains(log.FailureReason));
      }

      if (startDate.HasValue) {
        query = query.Where(log => log.CreatedAt >= startDate.Value.ToUniversalTime());
      }

      if (endDate.HasValue) {
        // endDate는 해당 날짜의 끝까지 포함하도록 조정
        query = query.Where(log => log.CreatedAt < endDate.Value.ToUniversalTime().AddDays(1));
      }


      var totalCount = await query.CountAsync();

      if (!string.IsNullOrWhiteSpace(orderBy)) {
        query = query.OrderBy(orderBy);
      }

      var logs = await query
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync();

      var totalPageCount = (int)Math.Ceiling((double)totalCount / pageSize);

      return Results.Ok(new {
        success = true,
        data = logs,
        totalcount = totalCount,
        totalpagecount = totalPageCount
      });
    }).RequireAuthorization();
  }
}
