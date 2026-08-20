using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Utilities;
using HelpDeskServer.Services;
using HelpDeskServer.Dtos;
using HelpDeskServer.Helpers;


namespace HelpDeskServer.Endpoints;

/// <summary>
/// 덧글 관련 엔드포인트
/// </summary>
public static class CommentEndpoints {
  /// <summary>
  /// 덧글 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapCommentEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/comments");

    group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
      // 1. 모든 댓글을 가져옵니다.
      var comments = await db.Comments.ToListAsync();

      /* // 댓글 마이그래이션
      bool hasChanges = false;

      // 2. 각 댓글을 순회하며 base64 이미지를 파일로 변환합니다.
      foreach (var comment in comments) {
        string originalText = comment.CommentText;
        comment.CommentText = await FileUtil.SaveImageToFile(originalText, "cmt_" + comment.Id.ToString());
        if (originalText != comment.CommentText) {
          hasChanges = true;
        }
      }

      // 3. 변경 사항이 있는 경우에만 데이터베이스에 저장합니다.
      if (hasChanges) await db.SaveChangesAsync();
*/
      return comments;
    }));

    //     () => db.Comments.ToListAsync()
    // ));

    group.MapGet("/my", (AppDbContext db, HttpContext http, string? content, DateTime? startDate, DateTime? endDate, int? userId, string? userType) => ApiResponseBuilder.CreateAsync(() => {
      var uidClaim = http.User.FindFirst("uid");
      var loginTypeClaim = http.User.FindFirst("login_type");

      if (uidClaim == null || !int.TryParse(uidClaim.Value, out var currentUserId) || loginTypeClaim == null) {
        // This should be handled by RequireAuthorization, but as a safeguard.
        throw new UnauthorizedAccessException("Cannot identify user from token.");
      }

      var targetUserId = currentUserId;
      var targetUserType = loginTypeClaim.Value;

      // If the caller is an admin and specifies a user to search for, override the target.
      if (targetUserType == "admin" && userId.HasValue && !string.IsNullOrEmpty(userType)) {
        targetUserId = userId.Value;
        targetUserType = userType;
      }

      var query = db.Comments
          .Where(c => c.AuthorId == targetUserId && c.AuthorType == targetUserType && !c.IsDel);

      if (!string.IsNullOrEmpty(content)) {
        query = query.Where(c => c.CommentText.Contains(content));
      }

      if (startDate.HasValue) {
        var utcStartDate = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
        query = query.Where(c => c.CreatedAt >= utcStartDate);
      }

      if (endDate.HasValue) {
        // Include the whole day of the end date
        // Npgsql은 UTC DateTime만 지원하므로 명시적으로 지정합니다.
        var nextDay = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
        query = query.Where(c => c.CreatedAt < nextDay);
      }

      /*
        return query
          .Join(db.Requests,
              comment => comment.RequestId,
              request => request.Id,
              (comment, request) => new MyCommentDto {
                CommentId = comment.Id,
                CommentText = comment.CommentText,
                CreatedAt = comment.CreatedAt,
                RequestId = request.Id,
                RequestTitle = request.Title,
                RequestStatus = request.Status.GetDisplayName()
              })
          .OrderByDescending(c => c.CreatedAt)
          .ToListAsync();
      */

      var result = from comment in query
                   join request in db.Requests on comment.RequestId equals request.Id
                   join admin in db.Admins on new { Id = comment.AuthorId, Type = comment.AuthorType } equals new { Id = admin.Id, Type = "admin" } into adminGroup
                   from admin in adminGroup.DefaultIfEmpty()
                   join customer in db.Customers on new { Id = comment.AuthorId, Type = comment.AuthorType } equals new { Id = customer.Id, Type = "customer" } into customerGroup
                   from customer in customerGroup.DefaultIfEmpty()
                   orderby comment.CreatedAt descending
                   select new MyCommentDto {
                     CommentId = comment.Id,
                     CommentText = comment.CommentText,
                     CreatedAt = comment.CreatedAt,
                     RequestId = request.Id,
                     RequestTitle = request.Title,
                     RequestStatus = request.Status.GetDisplayName(),
                     AuthorName = admin != null ? admin.UserName : (customer != null ? customer.UserName : "Unknown"),
                     AuthorPhoto = admin != null ? admin.Photo : (customer != null ? customer.Photo : null),
                     ParentCommentId = comment.ParentCommentId,
                     ReplyCount = db.Comments.Count(c => c.ParentCommentId == comment.Id && !c.IsDel)
                   };

      return result.ToListAsync();
    })).RequireAuthorization(); // Make sure endpoint requires authentication

    group.MapGet("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
      var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id);
      if (comment != null && comment.IsDel) {
        comment.CommentText = "삭제된 댓글입니다.";
      }
      return comment;
    }));

    group.MapPost("/", (AppDbContext db, ImprovementComment comment, ILoggerFactory loggerFactory, IConfiguration configuration, IPushSubscriptionStore store, IWebPushService sender) => ApiResponseBuilder.CreateAsync(async () => {

      db.Comments.Add(comment);
      await db.SaveChangesAsync();

      // DB에 저장하여 Id가 생성된 후, 이미지 파일을 저장하고 경로를 업데이트합니다.
      var updatedCommentText = await FileUtil.SaveImageToFile(comment.CommentText, "cmt_" + comment.Id.ToString());
      if (comment.CommentText != updatedCommentText) {
        comment.CommentText = updatedCommentText;
        await db.SaveChangesAsync();
      }

      //Console.WriteLine($"aaaaaaaaaa");

      // 1. 댓글 작성자 정보 조회
      string authorName = "알 수 없는 사용자";
      if (comment.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase)) {
        var author = await db.Admins.FindAsync(comment.AuthorId);
        if (author != null) authorName = author.UserName;
      }
      else {
        var author = await db.Customers.FindAsync(comment.AuthorId);
        if (author != null) authorName = author.UserName;
      }

      Console.WriteLine($"bbbbbbbbbbbbb : {authorName}");
      // 2. 원본 요청글 정보 조회
      var request = await db.Requests.FindAsync(comment.RequestId);
      if (request == null) return comment; // 요청글이 없으면 알림 발송 중단

      var pushTitle = $"댓글 - {authorName}";
      var pushBody = $"\"{comment.CommentText}\"";
      var pushUrl = $"/request_detail?id={comment.RequestId}#comment-{comment.Id}";

      Console.WriteLine($"cccccccccccccccccc");
      // 3. 알림 수신자 결정 및 발송
      // 요청에 담당자가 배정된 경우
      if (request.AdminId.HasValue) {
        Console.WriteLine($"dddddddddddddddd");
        // 담당자와 댓글 작성자가 다를 경우, 담당자에게 알림 발송
        //if (request.AdminId.Value != comment.AuthorId || !comment.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase))
        //if (request.AdminId.Value != comment.AuthorId || !comment.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase))
        //{
        var assignedAdminSubscriptions = await store.GetSubscriptionsByUserAsync(request.AdminId.Value, "admin");
        if (assignedAdminSubscriptions.Any()) {
          Console.WriteLine($"ggggggggggggg : {authorName}");
          await PushUtil.SendPushMsg(pushTitle, pushBody, pushUrl, assignedAdminSubscriptions, sender);
        }
        //}

        // 댓글 작성자가 관리자가 아닐 경우, 요청자에게도 알림 발송 (자기 댓글 제외)
        //if (request.CustomerId != comment.AuthorId || !comment.AuthorType.Equals("customer", StringComparison.OrdinalIgnoreCase))
        //{
        var customerSubscriptions = await store.GetSubscriptionsByUserAsync(request.CustomerId, "customer");
        if (customerSubscriptions.Any()) {
          Console.WriteLine($"hhhhhhhhhhhhhhhh : {authorName}");
          await PushUtil.SendPushMsg(pushTitle, pushBody, pushUrl, customerSubscriptions, sender);
        }
        //}
      }
      // 요청에 담당자가 배정되지 않은 경우: 모든 관리자에게 알림 발송
      else {
        Console.WriteLine($"eeeeeeeeeeeeeee");
        var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
        await PushUtil.SendPushMsg(pushTitle, pushBody, pushUrl, adminSubscriptions, sender);
      }



      Console.WriteLine($"fffffffffffffffff");


      return comment;
    }, "Comment created successfully.", 201));

    group.MapPut("/{id}", (AppDbContext db, int id, ImprovementComment input, ILoggerFactory loggerFactory, IConfiguration configuration, IPushSubscriptionStore store, IWebPushService sender) => ApiResponseBuilder.CreateAsync(async () => {
      var comment = await db.Comments.FindAsync(id);
      if (comment is null) return null;

      var updatedCommentText = await FileUtil.SaveImageToFile(input.CommentText, "cmt_" + comment.Id.ToString());
      if (comment.CommentText != updatedCommentText) {
        comment.CommentText = updatedCommentText;
      }

      comment.RequestId = input.RequestId;
      // 아래 두 줄은 수정 시 작성자 정보가 바뀌지 않는다면 필요 없을 수 있습니다.
      comment.AuthorType = input.AuthorType;
      comment.AuthorId = input.AuthorId;

      await db.SaveChangesAsync();

      return comment;
    }, "Comment updated successfully."));

    group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
      var comment = await db.Comments.FindAsync(id);
      if (comment is null) return null;

      var storageBase = Environment.GetEnvironmentVariable("ImageStorage_BasePath") ?? "/home/lee/JinHelpContents/reqs";

      await FileUtil.DeleteImageFileDir(storageBase, "cmt_" + id.ToString());

      // 삭제 flag 처리하자.



      comment.IsDel = true;
      comment.DeletedAt = DateTime.UtcNow;


      //db.Comments.Remove(comment);
      await db.SaveChangesAsync();
      return new { DeletedId = id };
    }, "Comment deleted successfully."));
  }
}
