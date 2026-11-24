using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>요청글·댓글을 바꿀 수 있는 사람인지 가른다. 막을 때는 <see cref="UnauthorizedAccessException"/> 을 던지고 ApiResponseBuilder 가 403 으로 바꾼다.</summary>
public static class HelpdeskAccess {

  /// <summary>관리자가 아니면 막는다.</summary>
  public static void RequireAdmin(this HelpdeskPrincipal me) {
    if (!me.IsAdmin) throw new UnauthorizedAccessException("관리자 권한이 필요합니다.");
  }

  /// <summary>글을 올린 사람인가. 포털 아이디(createdBy)를 먼저 보고, 옛 글은 연결된 고객 번호로 본다.</summary>
  public static bool IsRequestOwner(this HelpdeskPrincipal me, ImprovementRequest req) {
    if (!string.IsNullOrWhiteSpace(me.JsiniUserId) &&
        string.Equals(req.CreatedBy, me.JsiniUserId, StringComparison.OrdinalIgnoreCase)) {
      return true;
    }

    return me.HelpdeskUserId is { } uid && uid == req.CustomerId &&
           string.Equals(me.LinkedUserType, "customer", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>관리자 · 글 주인이 아니면 막는다. <paramref name="allowSameCompany"/> 면 같은 회사 고객도 통과시킨다.</summary>
  public static async Task RequireRequestAccessAsync(
      this HelpdeskPrincipal me, AppDbContext db, ImprovementRequest req, bool allowSameCompany, CancellationToken ct = default) {
    if (me.IsAdmin || me.IsRequestOwner(req)) return;

    // 같은 회사는 글이 쓰일 때의 회사로 가른다 — 쓴 사람이 다른 회사로 옮겨 가도 글은 옛 회사 사람들 것으로 남는다.
    if (allowSameCompany && me.HasCompany
        && string.Equals(req.CompanyId, me.CompanyId, StringComparison.OrdinalIgnoreCase)) {
      return;
    }

    await Task.CompletedTask;

    throw new UnauthorizedAccessException("이 요청을 처리할 권한이 없습니다.");
  }

  /// <summary>댓글을 쓴 사람인가. 작성자 번호는 담당자·고객 체계가 달라 종류까지 맞아야 한다.</summary>
  public static bool IsCommentAuthor(this HelpdeskPrincipal me, ImprovementComment comment) {
    if (!string.IsNullOrWhiteSpace(me.JsiniUserId) &&
        string.Equals(comment.CreatedBy, me.JsiniUserId, StringComparison.OrdinalIgnoreCase)) {
      return true;
    }

    return me.HelpdeskUserId is { } uid && uid > 0 && uid == comment.AuthorId &&
           string.Equals(me.LinkedUserType, comment.AuthorType, StringComparison.OrdinalIgnoreCase);
  }
}
