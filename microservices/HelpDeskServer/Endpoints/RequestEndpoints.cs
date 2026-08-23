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
using HelpDeskServer.Services;
using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;
using HelpDeskServer.Utilities;

namespace HelpDeskServer.Endpoints;

/// <summary> 요청(ImprovementRequest) 엔드포인트 </summary>
public static class RequestEndpoints {
  /// <summary>
  /// 개선 요청 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapRequestEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/requests");

    // 요청 목록 (필터링 포함)
    group.MapGet("/", (AppDbContext db, ImprovementStatus? status) => ApiResponseBuilder.CreateAsync(async () => {

      /*
            // base64 이미지 파일보관을 위해

            var requests = await db.Requests.ToListAsync();
            bool hasChanges = false;

            // 2. 각  요청글을 순회하며 base64 이미지를 파일로 변환합니다.
            foreach (var request in requests) {
              string originalText = request.MainPhoto;
              try {
                //request.Description = await FileUtil.SaveImageToFile(originalText, request.Id.ToString());

                request.MainPhoto = await FileUtil.GetFirstImageUrl(request.Description);
                if (originalText != request.MainPhoto) {
                  hasChanges = true;
                }

              }
              catch (Exception eeee) {

              }
            }

            // 3. 변경 사항이 있는 경우에만 데이터베이스에 저장합니다.
            if (hasChanges) await db.SaveChangesAsync();

            // 끝.

      */

      var query = db.Requests.Include(r => r.Comments)
          .AsQueryable();
      if (status.HasValue)
        query = query.Where(r => r.Status == status.Value);
      return query.ToListAsync();
    }));


    // 월간 보고서
    group.MapGet("/report/monthly", (AppDbContext db, int year, int month, int? companyId) => ApiResponseBuilder.CreateAsync(async () => {
      var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
      var endDate = startDate.AddMonths(1);

      // 1. 해당 월에 생성된 요청
      var createdQuery = db.Requests
          .Include(r => r.Customer)
          .Where(r => r.RequestedAt >= startDate && r.RequestedAt < endDate);

      if (companyId.HasValue) {
        createdQuery = createdQuery.Where(r => r.Customer != null && r.Customer.CompanyId == companyId.Value);
      }
      var createdRequests = await createdQuery.ToListAsync();

      // 2. 해당 월에 완료된 요청 (생성일 무관)
      var completedQuery = db.Requests
          .Include(r => r.Customer)
          .Where(r => (r.CompletededAt >= startDate && r.CompletededAt < endDate) ||
                      (r.UserCompletededAt >= startDate && r.UserCompletededAt < endDate));

      if (companyId.HasValue) {
        completedQuery = completedQuery.Where(r => r.Customer != null && r.Customer.CompanyId == companyId.Value);
      }
      var completedRequests = await completedQuery.ToListAsync();

      var totalCreated = createdRequests.Count;
      
      // 관리자 완료 (Status 3: Completed)
      var adminCompletedCount = completedRequests.Count(r => r.Status == ImprovementStatus.Completed);
      // 사용자 종료 (Status 7: UserCompleted)
      var userCompletedCount = completedRequests.Count(r => r.Status == ImprovementStatus.UserCompleted);
      
      var totalResolved = completedRequests.Count; // 전체 해결 건수 (완료 + 종료)

      var pendingCount = createdRequests.Count(r => r.Status == ImprovementStatus.Pending);
      var inProgressCount = createdRequests.Count(r => r.Status == ImprovementStatus.InProgress);
      var consultationCount = createdRequests.Count(r => r.Status == ImprovementStatus.Consultation);
      var negotiationCount = createdRequests.Count(r => r.Status == ImprovementStatus.Negotiation);

      double resolutionRate = totalCreated == 0 ? 0 : ((double)totalResolved / totalCreated) * 100;

      var byStatus = createdRequests
          .GroupBy(r => r.Status)
          .ToDictionary(g => g.Key.ToString(), g => g.Count());

      var byType = createdRequests
          .GroupBy(r => r.IpType)
          .ToDictionary(g => g.Key.ToString(), g => g.Count());

      // 처리 시간 통계 계산
      double totalHours = 0;
      int countWithTime = 0;
      var timeDist = new Dictionary<string, int> {
          { "24시간 이내", 0 },
          { "1일~3일", 0 },
          { "3일~7일", 0 },
          { "7일 이상", 0 }
      };

      foreach (var req in completedRequests) {
        var completedAt = req.CompletededAt ?? req.UserCompletededAt;
        if (completedAt.HasValue) {
          var duration = completedAt.Value - req.RequestedAt;
          var hours = duration.TotalHours;
          
          // 마이너스 시간(데이터 오류 등)은 제외
          if(hours < 0) continue; 

          totalHours += hours;
          countWithTime++;

          if (hours <= 24) timeDist["24시간 이내"]++;
          else if (hours <= 72) timeDist["1일~3일"]++;
          else if (hours <= 168) timeDist["3일~7일"]++;
          else timeDist["7일 이상"]++;
        }
      }

      double avgResolutionTime = countWithTime > 0 ? totalHours / countWithTime : 0;


      var daysInMonth = DateTime.DaysInMonth(year, month);
      var dailyStats = new List<DailyRequestStatDto>();

      for (int i = 1; i <= daysInMonth; i++) {
        var cCount = createdRequests.Count(r => r.RequestedAt.Day == i);
        var fCount = completedRequests.Count(r =>
           (r.CompletededAt.HasValue && r.CompletededAt.Value.Day == i) ||
           (r.UserCompletededAt.HasValue && r.UserCompletededAt.Value.Day == i));

        dailyStats.Add(new DailyRequestStatDto(i, cCount, fCount));
      }

      var recentCompleted = completedRequests
          .OrderByDescending(r => r.CompletededAt ?? r.UserCompletededAt)
          .Take(10)
          .Select(r => new MaintenanceRequestSummaryDto(
              r.Id,
              r.Title,
              r.RequestedAt,
              r.CompletededAt ?? r.UserCompletededAt,
              r.Status.ToString(),
              r.IpType.ToString()
          ))
          .ToList();

      return new MaintenanceReportDto(
          year, month,
          totalCreated, adminCompletedCount, userCompletedCount, pendingCount, inProgressCount,
          consultationCount, negotiationCount,
          resolutionRate,
          byStatus, byType, 
          avgResolutionTime, timeDist,
          dailyStats, recentCompleted
      );
    }, "Monthly report generated successfully."));

    // 사용자 협업 보고서
    group.MapGet("/report/collaboration", (AppDbContext db, int year, int month) => ApiResponseBuilder.CreateAsync(async () => {
      var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
      var endDate = startDate.AddMonths(1);

      // 해당 월의 요청들 (생성되거나 완료된 것 포함)
      var requests = await db.Requests
          .Include(r => r.Customer)
          .ThenInclude(c => c.Company)
          .Include(r => r.Comments)
          .Where(r => (r.RequestedAt >= startDate && r.RequestedAt < endDate) ||
                      (r.CompletededAt >= startDate && r.CompletededAt < endDate) ||
                      (r.UserCompletededAt >= startDate && r.UserCompletededAt < endDate))
          .ToListAsync();

      // 1. 평균 사용자 피드백 시간 계산
      // 관리자 댓글 이후 사용자가 댓글을 달 때까지의 시간 차이
      double totalFeedbackHours = 0;
      int feedbackCount = 0;

      foreach (var req in requests) {
        var sortedComments = req.Comments.OrderBy(c => c.CreatedAt).ToList();
        for (int i = 0; i < sortedComments.Count - 1; i++) {
          var current = sortedComments[i];
          var next = sortedComments[i + 1];

          if (current.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase) && 
              !next.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase)) {
            var diff = next.CreatedAt - current.CreatedAt;
            if (diff.TotalHours > 0) {
              totalFeedbackHours += diff.TotalHours;
              feedbackCount++;
            }
          }
        }
      }

      double avgUserFeedbackHours = feedbackCount > 0 ? Math.Round(totalFeedbackHours / feedbackCount, 1) : 0;

      // 2. 우수 협업 사용자 (Top Engaged Users)
      var userStats = requests
          .Where(r => r.Customer != null)
          .GroupBy(r => r.CustomerId)
          .Select(g => {
            var customer = g.First().Customer;
            var interactions = g.Sum(r => r.Comments.Count(c => !c.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase)));
            var confirms = g.Count(r => r.Status == ImprovementStatus.UserCompleted);
            return new {
              Name = customer?.UserName ?? "Unknown",
              Company = customer?.Company?.Name ?? "Unknown",
              Interactions = interactions,
              Confirms = confirms,
              Total = interactions + (confirms * 2) // 가중치 부여
            };
          })
          .OrderByDescending(u => u.Total)
          .Take(5)
          .ToList();

      // 3. 사용자 확정 비율
      var totalResolved = requests.Count(r => r.Status == ImprovementStatus.Completed || r.Status == ImprovementStatus.UserCompleted);
      var confirmedByUser = requests.Count(r => r.Status == ImprovementStatus.UserCompleted);
      double confirmationRate = totalResolved > 0 ? Math.Round(((double)confirmedByUser / totalResolved) * 100, 1) : 0;

      return new {
        avgUserFeedbackHours,
        topEngagedUsers = userStats,
        confirmationRate,
        totalRequestsAnalyzed = requests.Count
      };
    }, "Collaboration report generated successfully."));

    // 품질 및 안정성 보고서
    group.MapGet("/report/quality", (AppDbContext db, int year, int month) => ApiResponseBuilder.CreateAsync(async () => {
      var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
      var endDate = startDate.AddMonths(1);

      // 해당 월의 모든 요청 (품질 분석용)
      var requests = await db.Requests
          .Include(r => r.Comments)
          .Where(r => r.RequestedAt >= startDate && r.RequestedAt < endDate || 
                      r.CompletededAt >= startDate && r.CompletededAt < endDate)
          .ToListAsync();

      // 1. SR(Service Request) vs 결함(Bug) 비율
      // 이제 IpType에 Error(4)와 Bug(5)가 있으므로 이를 명시적으로 사용
      var bugCount = requests.Count(r => r.IpType == ImprovementType.Error || r.IpType == ImprovementType.Bug);
      var srCount = requests.Count - bugCount;

      double srRatio = requests.Count > 0 ? Math.Round((double)srCount / requests.Count * 100, 1) : 100;
      double bugRatio = requests.Count > 0 ? Math.Round((double)bugCount / requests.Count * 100, 1) : 0;


      // 2. 재오픈율 (Re-open Rate)
      // 로직: 완료일(CompletededAt)이 있는데 현재 상태가 다시 InProgress이거나 Pending인 경우를 재오픈으로 간주
      var reopenedRequests = requests.Count(r => r.CompletededAt.HasValue && 
                                               (r.Status == ImprovementStatus.InProgress || r.Status == ImprovementStatus.Pending));
      double reopenRate = requests.Count > 0 ? Math.Round((double)reopenedRequests / requests.Count * 100, 1) : 0;

      // 3. 변경 작업 성공률 (Change Success Rate)
      // 로직: '개선'이나 '추가' 타입의 작업 중 완료된 건을 대상으로, 
      // 완료 후 48시간 이내에 동일 작성자가 '오류' 키워드로 다시 올린 건이 없는 비율 산출 (추정치)
      var changeTasks = requests.Where(r => r.IpType == ImprovementType.Improvement || r.IpType == ImprovementType.Addition).ToList();
      var successfulChanges = changeTasks.Count - reopenedRequests; // 재오픈된 변경작업은 실패로 간주
      
      double changeSuccessRate = changeTasks.Count > 0 
          ? Math.Round((double)Math.Max(0, successfulChanges) / changeTasks.Count * 100, 1) 
          : 100;

      return new {
        srRatio,
        bugRatio,
        reopenRate,
        changeSuccessRate,
        rollbackCount = reopenedRequests, // 재오픈을 롤백/재작업으로 간주
        totalAnalyzed = requests.Count
      };
    }, "Quality report generated successfully."));

    // 실시간 긴급 장애 보고서
    group.MapGet("/report/emergency", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
      // 1. 접수 정보 중 타입이 '긴급/장애'이고 아직 완료되지 않은 건 조회
      var activeEmergencies = await db.Requests
          .Include(r => r.Customer)
          .ThenInclude(c => c.Company)
          .Where(r => r.IpType == ImprovementType.Emergency && 
                      r.Status != ImprovementStatus.Completed && 
                      r.Status != ImprovementStatus.UserCompleted &&
                      r.Status != ImprovementStatus.Delete)
          .OrderByDescending(r => r.RequestedAt)
          .Select(r => new {
            Id = r.Id,
            Title = r.Title,
            Status = r.Status.ToString(),
            Time = r.RequestedAt.ToString("HH:mm"),
            Severity = "Critical" // 긴급/장애는 기본적으로 Critical로 표시
          })
          .ToListAsync();

      return activeEmergencies;
    }, "Emergency incidents retrieved successfully."));


    // 요청 상세
    group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(
        () => db.Requests
        .Include(r => r.Comments)
        .Include(r => r.Customer)
        .Include(r => r.Admin)
        .FirstOrDefaultAsync(r => r.Id == id)
    ));

    // 특정 요청에 대한 덧글 목록 조회
    group.MapGet("/{id}/comments", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
      var comments = await db.Comments
              .Where(c => c.RequestId == id)
              .OrderBy(c => c.CreatedAt)
              .ToListAsync();

      var adminIds = comments.Where(c => c.AuthorType == "admin").Select(c => c.AuthorId).Distinct().ToList();
      var customerIds = comments.Where(c => c.AuthorType != "admin").Select(c => c.AuthorId).Distinct().ToList();

      var admins = await db.Admins
              .Where(a => adminIds.Contains(a.Id))
              .ToDictionaryAsync(a => a.Id);

      var customers = await db.Customers
              .Where(c => customerIds.Contains(c.Id))
              .ToDictionaryAsync(c => c.Id);

      var result = comments.Select(c => {
        object? author = null;
        if (c.AuthorType == "admin" && admins.TryGetValue(c.AuthorId, out var admin)) {
          author = new { admin.Id, admin.UserName, admin.Photo };
        }
        else if (customers.TryGetValue(c.AuthorId, out var customer)) {
          author = new { customer.Id, customer.UserName, customer.Photo };
        }

        return new {
          // IsDel 플래그를 확인하여 삭제된 댓글의 내용을 변경합니다.
          CommentText = c.IsDel ? "삭제된 댓글입니다." : c.CommentText,
          c.Id,
          c.RequestId,
          c.AuthorType,
          c.AuthorId,
          c.ParentCommentId,
          c.CreatedAt,
          c.CreatedBy,
          Author = author
        };
      }).ToList();

      return result;
    }, "Comments retrieved successfully."));


    // 검색
    group.MapPost("/srch", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync(async () => {
      using var reader = new StreamReader(http.Request.Body);
      var body = await reader.ReadToEndAsync();

      IQueryCollection bodyQuery = new QueryCollection();
      if (!string.IsNullOrWhiteSpace(body)) {
        bodyQuery = JsonToQueryHelper.ConvertJsonToQuery(body);
      }


      var finalQuery = QueryCollectionMerger.Merge(http.Request.Query, bodyQuery);

      // Console.WriteLine($"Final query: {finalQuery.ToString()}");


      var queryWithIncludes = db.Requests
              .Include(c => c.Comments)
              .Include(r => r.Customer)
              .Include(r => r.Admin)
              .AsQueryable();

      // 페이징 전 쿼리로 총 개수 계산
      // page와 pageSize를 제거하여 총 개수 계산용 쿼리 생성
      var countQueryDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
      foreach (var kvp in finalQuery) {
        if (!kvp.Key.Equals("page", StringComparison.OrdinalIgnoreCase) &&
                !kvp.Key.Equals("pageSize", StringComparison.OrdinalIgnoreCase)) {
          countQueryDict[kvp.Key] = kvp.Value;
        }
      }
      var countQueryCollection = new QueryCollection(countQueryDict);

      var countQueryWithFilters = queryWithIncludes.ApplyAll(countQueryCollection);
      // var totalCount = await (countQueryWithFilters is IQueryable<object> cq ? cq.CountAsync() : ((IQueryable)countQueryWithFilters).CountAsync());
      var countList = await (countQueryWithFilters is IQueryable<object> cq ? cq.ToDynamicListAsync() : ((IQueryable)countQueryWithFilters).ToDynamicListAsync());
      var totalCount = countList.Count;

      // 페이징이 적용된 쿼리로 실제 데이터 조회
      var resultQuery = queryWithIncludes.ApplyAll(finalQuery);


      //  Console.WriteLine($"result query: {resultQuery.ToString()}");




      var requests = await (resultQuery is IQueryable<object> q ? q.ToDynamicListAsync() : ((IQueryable)resultQuery).ToDynamicListAsync());

      var data = await AddAttachmentDataAsync(requests, db, "ImprovementRequest");

      // pageSize 가져오기
      int pageSize = 0;
      if (finalQuery.TryGetValue("pageSize", out var psVal) && int.TryParse(psVal, out var ps)) {
        pageSize = Math.Max(0, ps);
      }

      // 총 페이지 수 계산
      int totalPageCount = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

      // 응답 객체에 data와 totalpagecount 포함
      return new {
        data = data,
        totalpagecount = totalPageCount,
        totalcount = totalCount
      };

    }, "Request srch successfully.", 201));


    // 요청 생성
    group.MapPost("/", async (IAdminService adminService, HttpRequest httpRequest, AppDbContext db, IRabbitMqConnectionProvider provider, ILoggerFactory loggerFactory, IConfiguration configuration, IPushSubscriptionStore store, IWebPushService sender) => {
      var form = await httpRequest.ReadFormAsync();
      var me = httpRequest.HttpContext.GetHelpdeskPrincipal();

      // 요청의 주인(CustomerId)을 폼 값으로 받고 있었다. 고객이 **남의 회사 이름으로**
      // 요청을 만들 수 있는 상태였다. 담당자는 대신 등록할 일이 있으니 그대로 두고,
      // 고객으로 연결된 계정은 자기 것으로 고정한다.
      var formCustomerId = int.TryParse(form["CustomerId"], out var parsed) ? parsed : 0;
      var customerId = me.IsCustomer && me.HelpdeskUserId.HasValue
          ? me.HelpdeskUserId.Value
          : formCustomerId;

      var requestDto = new RequestCreateDto(
              Title: form["Title"],
              Description: form["Description"],
              CustomerId: customerId,
              // 작성자는 폼 값이 아니라 로그인한 JSini 계정에서 정한다.
              CreatedBy: httpRequest.HttpContext.AuditUser(),
              MenuContext: form["MenuContext"]
         , MainPhoto: string.Empty // form["MainPhoto"]
          );
      var files = form.Files;

      var result = await ApiResponseBuilder.CreateAsync(async () => {
        var request = new ImprovementRequest {
          Title = requestDto.Title,
          //Description = requestDto.Description,
          CustomerId = requestDto.CustomerId,
          Status = ImprovementStatus.Pending,
          CreatedBy = requestDto.CreatedBy,
          MenuContext = requestDto.MenuContext,
          IpType = int.TryParse(form["iptype"], out int iptypeVal) ? (ImprovementType)iptypeVal : 
                   Enum.TryParse<ImprovementType>(form["iptype"], true, out var iptypeEnum) ? iptypeEnum : ImprovementType.Improvement,
          //MainPhoto = requestDto.MainPhoto
        };


        db.Requests.Add(request);
        await db.SaveChangesAsync();

        // 바로 위에서 저장하여 request.Id가 설정된 후에 처리 시작.

        // base64 이미지 파일보관 처리 및 url 변경
        request.Description = await FileUtil.SaveImageToFile(requestDto.Description, request.Id.ToString());
        // 변경된 문서의 첫번째 이미지 URL을 MainPhoto로 설정
        request.MainPhoto = await FileUtil.GetFirstImageUrl(request.Description);

        await db.SaveChangesAsync();




        if (files.Count > 0) {
          var attachments = new List<Attachment>();
          //var storagePath = "/home/lee/jinAttachment";
          //var storagePath = configuration.GetValue<string>("FileStorage:BasePath") ?? "/home/lee/jinAttachment";
          var storagePath = Environment.GetEnvironmentVariable("FileStorage_BasePath") ?? "/home/lee/jinAttachment";

          Directory.CreateDirectory(storagePath);

          foreach (var file in files) {
            if (file.Length > 0) {
              var extension = Path.GetExtension(file.FileName);
              var storedFileName = $"{Guid.NewGuid()}{extension}";
              var filePath = Path.Combine(storagePath, storedFileName);

              await using (var stream = new FileStream(filePath, FileMode.Create)) {
                await file.CopyToAsync(stream);
              }

              var attachment = new Attachment {
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                FilePath = storagePath,
                FileType = file.ContentType,
                FileSize = file.Length,
                EntityType = "ImprovementRequest",
                EntityId = request.Id,
                UploadedAt = DateTime.UtcNow
              };
              attachments.Add(attachment);
            }
          }

          if (attachments.Any()) {
            db.Attachments.AddRange(attachments);
            await db.SaveChangesAsync();
          }
        }





        var logger = loggerFactory.CreateLogger("RequestEndpoints");
        if (provider.IsConnected) {


          var adminSubscriptions = await store.GetAdminSubscriptionsAsync();

          string mailBody = request.Description + "<br/><br/>" +
            $"<a href='https://help.jin114.co.kr/request_detail?id={request.Id}' target='_blank'>접수글 보기</a><br/><br/><br/><br/>";



          var adminEmails = await adminService.GetAdminEmailsForNotificationAsync();
          string mailTos = string.Join(";", adminEmails);

          await EMailUtil.SendEmailJinNets(mailTos, requestDto.Title, mailBody, provider, loggerFactory, configuration);



          // 푸시 알림 전송  
          var customer = await db.Customers.FindAsync(requestDto.CustomerId);
          await PushUtil.SendPushMsg(
            $"신규 - {customer?.UserName}",
            $"{request.Title} : {request.Description}",
            $"/request_detail?id={request.Id}",
            adminSubscriptions,
            sender
            );
        }

        return request;
      }, "Request created successfully.", 201);
      return result;
    })
    .DisableAntiforgery();






    //접수, 반려, 완료 등을 반영한다.
    group.MapPut("/accept/{id}", (IAdminService adminService, AppDbContext db, int id, ImprovementRequest input, IRabbitMqConnectionProvider provider, ILoggerFactory loggerFactory, IConfiguration configuration, IPushSubscriptionStore store, IWebPushService sender) => ApiResponseBuilder.CreateAsync(async () => {
      //return null;
      var req = await db.Requests.FindAsync(id);
      if (req is null) return null;

      req.Status = input.Status;
      if (input.Status == ImprovementStatus.UserCompleted) { // 사용자 완료시 에는 사용자 완료일자 반영

        req.UserCompletededAt = DateTime.UtcNow;
      }
      else if (input.Status == ImprovementStatus.Completed) { // 완료시 에는 완료 일자 반영.

        req.CompletededAt = DateTime.UtcNow;
      }

      if (input.Status != ImprovementStatus.UserCompleted) { // 사용자 완료시 에는 관리자 코드 변경 안함. 
        req.AdminId = input.AdminId;
      }

      await db.SaveChangesAsync();

      // 접수 시 (InProgress) 알림
      if (input.Status == ImprovementStatus.InProgress && input.AdminId.HasValue) {
        var adm = await db.Admins.FindAsync(input.AdminId.Value);
        if (adm != null) {
          var customerSubscriptions = await store.GetSubscriptionsByUserAsync(req.CustomerId, "customer");
          await PushUtil.SendPushMsg($"배정 - {adm.UserName}", $"{req.Title} ", $"/request_detail?id={req.Id}", customerSubscriptions, sender);

          var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
          await PushUtil.SendPushMsg($"배정 - {adm.UserName}", $"{req.Title}", $"/request_detail?id={req.Id}", adminSubscriptions, sender);

        }
      }


      if (input.Status == ImprovementStatus.Completed) { // 관리자 완료시 모든 관리자, 작성 접수자 에게 알림.

        var adm = await db.Admins.FindAsync(input.AdminId);

        var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
        var customerSubscriptions = await store.GetSubscriptionsByUserAsync(req.CustomerId, "customer");

        await PushUtil.SendPushMsg($"완료 - {adm?.UserName}", $"{req.Title}", $"/request_detail?id={req.Id}", adminSubscriptions, sender);
        await PushUtil.SendPushMsg($"완료 - {adm?.UserName}", $"{req.Title}", $"/request_detail?id={req.Id}", customerSubscriptions, sender);


        string mailBody = req.Description + "<br/><br/>" + $" 접수글 [ {req.Title} ] 완료되었습니다.<br/><br/>" +
          $"<a href='https://help.jin114.co.kr/request_detail?id={req.Id}' target='_blank'>완료 글 보기</a><br/><br/><br/><br/>";




        //using var scope = serviceScopeFactory.CreateScope();
        //var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
        var adminEmails = await adminService.GetAdminEmailsForNotificationAsync();
        string mailTos = string.Join(";", adminEmails);
        var customerEmails = await adminService.GetCustomerEmailsForNotificationAsync(req.CustomerId);
        mailTos = mailTos + ";" + string.Join(";", customerEmails);




        await EMailUtil.SendEmailJinNets(mailTos, $"[완료] {req.Title}", mailBody, provider, loggerFactory, configuration);

        //await EMailUtil.SendEmailJinNets(custom_mailTos, $"[완료] {req.Title}", mailBody, provider, loggerFactory, configuration);

      }
      else if (input.Status == ImprovementStatus.UserCompleted) { // 사용자 완료시 관리자 모두에게 알림.

        var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
        await PushUtil.SendPushMsg(
                 $"종료 - {req.Title}",
                  $"{req.Title}",
                 $"/request_detail?id={req.Id}",
                  adminSubscriptions,
                     sender
                     );

      }

      return req;
    }, "Request accept successfully."));




    // 
    group.MapPut("/reset/{id}", (AppDbContext db, int id, ImprovementRequest input) => ApiResponseBuilder.CreateAsync(async () => {
      //return null;
      var req = await db.Requests.FindAsync(id);
      if (req is null) return null;

      req.Status = 0;
      req.AdminId = null;

      await db.SaveChangesAsync();
      return req;
    }, "Request reset successfully."));


    // 수정
    group.MapPut("/{id}", async (HttpRequest httpRequest, AppDbContext db, int id, IConfiguration configuration, ILoggerFactory loggerFactory) => {
      var form = await httpRequest.ReadFormAsync();
      var title = form["title"];
      var description = form["description"];
      var deletedFilesJson = form["deletedFiles"];
      //var MainPhoto = form["MainPhoto"];

      var logger = loggerFactory.CreateLogger("RequestEndpoints");



      logger.LogError("putRes start");



      var result = await ApiResponseBuilder.CreateAsync(async () => {
        var req = await db.Requests.FindAsync(id);
        if (req is null) return null;

        // iptype 파싱 (숫자 또는 문자열 이름 대응)
        if (int.TryParse(form["iptype"], out int iptypeValue)) {
          req.IpType = (ImprovementType)iptypeValue;
        }
        else if (Enum.TryParse<ImprovementType>(form["iptype"], true, out var iptypeEnum)) {
          req.IpType = iptypeEnum;
        }
        else {
          // 기본값 또는 에러 처리 (여기서는 기본값 유지 또는 로깅)
          logger.LogWarning($"Invalid iptype received: {form["iptype"]}");
        }
        req.Title = title;
        //req.Description = description;

        req.Description = await FileUtil.SaveImageToFile(description, req.Id.ToString());

        //req.MainPhoto = MainPhoto;
        req.MainPhoto = await FileUtil.GetFirstImageUrl(req.Description);


        if (!string.IsNullOrEmpty(deletedFilesJson)) {
          var deletedFileIds = JsonSerializer.Deserialize<List<int>>(deletedFilesJson);
          if (deletedFileIds != null && deletedFileIds.Any()) {
            var attachmentsToDelete = await db.Attachments.Where(a => deletedFileIds.Contains(a.Id)).ToListAsync();
            foreach (var attachment in attachmentsToDelete) {
              var filePath = Path.Combine(attachment.FilePath, attachment.StoredFileName);
              if (File.Exists(filePath)) {
                File.Delete(filePath);
              }
            }
            db.Attachments.RemoveRange(attachmentsToDelete);
          }
        }

        var files = form.Files;
        if (files.Count > 0) {

          logger.LogError("File Find start");

          //var storagePath = configuration.GetValue<string>("FileStorage:BasePath") ?? "/home/lee/jinAttachment";
          var storagePath = Environment.GetEnvironmentVariable("FileStorage_BasePath") ?? "/home/lee/jinAttachment";
          Directory.CreateDirectory(storagePath);

          foreach (var file in files) {
            if (file.Length > 0) {
              var extension = Path.GetExtension(file.FileName);
              var storedFileName = $"{Guid.NewGuid()}{extension}";
              var filePath = Path.Combine(storagePath, storedFileName);

              await using (var stream = new FileStream(filePath, FileMode.Create)) {
                await file.CopyToAsync(stream);
              }

              var attachment = new Attachment {
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                FilePath = storagePath,
                FileType = file.ContentType,
                FileSize = file.Length,
                EntityType = "ImprovementRequest",
                EntityId = req.Id,
                UploadedAt = DateTime.UtcNow
              };
              db.Attachments.Add(attachment);
            }
          }
        }

        await db.SaveChangesAsync();
        return req;
      }, "Request updated successfully.");
      return result;
    }).DisableAntiforgery();



    // 삭제
    group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {

      if (id == 231) throw new Exception("삭제 불가 요청입니다.");

      var req = await db.Requests.FindAsync(id);
      if (req is null) return null;


      var storageBase = Environment.GetEnvironmentVariable("ImageStorage_BasePath") ?? "/home/lee/JinHelpContents/reqs";
      await FileUtil.DeleteImageFileDir(storageBase, id.ToString());


      db.Requests.Remove(req);
      await db.SaveChangesAsync();

      return new { DeletedId = id };
    }, "Request deleted successfully."));
  }

  /// <summary>
  /// 동적 객체 목록에 첨부파일 정보를 추가합니다.
  /// </summary>
  /// <param name="items">동적 객체 목록</param>
  /// <param name="db">데이터베이스 컨텍스트</param>
  /// <param name="entityType">엔티티 타입 문자열</param>
  /// <returns>첨부파일 정보가 추가된 동적 객체 목록</returns>
  private static async Task<List<dynamic>> AddAttachmentDataAsync(List<dynamic> items, AppDbContext db, string entityType) {
    if (items == null || !items.Any()) {
      return new List<dynamic>();
    }

    var itemIds = items.Select(x => (int)x.Id).ToList();

    var allAttachments = await db.Attachments
        .Where(a => a.EntityType == entityType && itemIds.Contains(a.EntityId))
        .ToListAsync();

    var attachmentsByEntityId = allAttachments.GroupBy(a => a.EntityId)
                                              .ToDictionary(g => g.Key, g => g.ToList());

    var result = new List<dynamic>();
    foreach (var item in items) {
      var expando = (item as object).ToExpandoWithEnumNames() as IDictionary<string, object>;
      var entityId = (int)expando["id"];

      var attachments = attachmentsByEntityId.GetValueOrDefault(entityId, new List<Attachment>());

      expando["attachmentCount"] = attachments.Count;
      expando["attachments"] = attachments;

      result.Add(expando);
    }

    return result;
  }







}
