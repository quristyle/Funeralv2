using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>요청글의 <b>주인</b>(작성자)과 <b>접수자</b>를 포털 로그인 아이디로 푼다. 요청글은 사람을 헬프데스크 내부 번호로 가리킨다</summary>
public interface IRequestOwnerResolver {
  /// <summary>요청글 <b>주인</b>(작성자)의 포털 로그인 아이디. 못 찾으면 <c>null</c>.</summary>
  Task<string?> OwnerLoginIdAsync(ImprovementRequest request, CancellationToken ct = default);

  /// <summary>요청글 <b>접수자</b>(담당자)의 포털 로그인 아이디. 접수자가 없거나 못 찾으면 <c>null</c>.</summary>
  Task<string?> AssigneeLoginIdAsync(ImprovementRequest request, CancellationToken ct = default);

  /// <summary>댓글 <b>작성자</b>의 포털 로그인 아이디. 못 찾으면 <c>null</c>.</summary>
  Task<string?> CommentAuthorLoginIdAsync(ImprovementComment comment, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class RequestOwnerResolver : IRequestOwnerResolver {
  /// <summary>사람이 아닌 주체. 신원 없이 들어온 요청·자동 처리가 이 이름으로 적힌다.</summary>
  private const string SystemActor = "system";

  private readonly AppDbContext _db;

  /// <summary>서비스를 생성한다.</summary>
  public RequestOwnerResolver(AppDbContext db) => _db = db;

  /// <inheritdoc />
  public async Task<string?> OwnerLoginIdAsync(
      ImprovementRequest request, CancellationToken ct = default) {

    var linked = await LinkedLoginIdAsync("customer", request.CustomerId, ct);
    if (!string.IsNullOrWhiteSpace(linked)) return linked;

    return Person(request.CreatedBy);
  }

  /// <inheritdoc />
  public async Task<string?> AssigneeLoginIdAsync(
      ImprovementRequest request, CancellationToken ct = default) {

    if (request.AdminId is not { } adminId || adminId <= 0) return null;

    var linked = await LinkedLoginIdAsync("admin", adminId, ct);
    if (!string.IsNullOrWhiteSpace(linked)) return linked;

    var loginId = await _db.Admins
        .AsNoTracking()
        .Where(a => a.Id == adminId && !a.IsDeleted)
        .Select(a => a.LoginId)
        .FirstOrDefaultAsync(ct);

    return Person(loginId);
  }

  /// <inheritdoc />
  public async Task<string?> CommentAuthorLoginIdAsync(
      ImprovementComment comment, CancellationToken ct = default) {

    var userType = string.Equals(comment.AuthorType, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "customer";
    var linked = await LinkedLoginIdAsync(userType, comment.AuthorId, ct);
    if (!string.IsNullOrWhiteSpace(linked)) return linked;

    if (userType == "admin" && comment.AuthorId > 0) {
      var loginId = await _db.Admins
          .AsNoTracking()
          .Where(a => a.Id == comment.AuthorId && !a.IsDeleted)
          .Select(a => a.LoginId)
          .FirstOrDefaultAsync(ct);
      var person = Person(loginId);
      if (!string.IsNullOrWhiteSpace(person)) return person;
    }
    else if (userType == "customer" && comment.AuthorId > 0) {
      var loginId = await _db.Customers
          .AsNoTracking()
          .Where(c => c.Id == comment.AuthorId && !c.IsDeleted)
          .Select(c => c.LoginId)
          .FirstOrDefaultAsync(ct);
      var person = Person(loginId);
      if (!string.IsNullOrWhiteSpace(person)) return person;
    }

    return Person(comment.CreatedBy);
  }

  /// <summary>계정 연결표 한 줄을 읽는다.</summary>
  private async Task<string?> LinkedLoginIdAsync(
      string userType, int helpdeskUserId, CancellationToken ct) {

    if (helpdeskUserId <= 0) return null;

    return await _db.AuthUserLinks
        .AsNoTracking()
        .Where(l => l.UserType == userType && l.HelpdeskUserId == helpdeskUserId)
        .Select(l => l.AuthUserId)
        .FirstOrDefaultAsync(ct);
  }

  /// <summary>사람이 아닌 이름(<c>system</c>·빈 값)을 걸러 낸다.</summary>
  private static string? Person(string? loginId)
      => string.IsNullOrWhiteSpace(loginId)
         || string.Equals(loginId, SystemActor, StringComparison.OrdinalIgnoreCase)
          ? null
          : loginId;
}
