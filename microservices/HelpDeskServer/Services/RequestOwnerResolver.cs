using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// 요청글의 <b>주인</b>(작성자)과 <b>접수자</b>를 포털 로그인 아이디로 푼다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 따로 푸는 일이 필요한가]
/// </para>
/// <para>
/// 요청글은 사람을 <b>헬프데스크 내부 번호</b>로 가리킨다
/// (<c>improvementrequest.customerid</c> · <c>adminid</c>). 그런데 알림이 나가는
/// 곳(<c>NotificationServer</c>)이 아는 사람은 <b>포털 로그인 아이디</b>뿐이다 —
/// 구독표(<c>scom.push_subscriptions</c>)도 주소표(<c>scom.accounts</c>)도 그
/// 아이디로 묶여 있다. 그 둘을 잇는 정본이 <c>helpdesk.auth_user_links</c> 다.
/// </para>
/// <para>
/// 이 풀이가 <b>한 군데에만</b> 있어야 하는 까닭은, 한쪽이 비면 알림이 오류 없이
/// 조용히 0 명이 되기 때문이다. 댓글 알림(<see cref="CommentNotifier"/>)과 처리
/// 상태 알림(<see cref="RequestStatusNotifier"/>)이 서로 다른 순서로 풀면
/// 「댓글은 오는데 완료는 안 온다」 같은 갈림이 생기고, 그 증상으로는 어느 쪽이
/// 틀렸는지 알 수가 없다.
/// </para>
/// </remarks>
public interface IRequestOwnerResolver {
  /// <summary>
  /// 요청글 <b>주인</b>(작성자)의 포털 로그인 아이디. 못 찾으면 <c>null</c>.
  /// </summary>
  Task<string?> OwnerLoginIdAsync(ImprovementRequest request, CancellationToken ct = default);

  /// <summary>
  /// 요청글 <b>접수자</b>(담당자)의 포털 로그인 아이디. 접수자가 없거나 못 찾으면 <c>null</c>.
  /// </summary>
  Task<string?> AssigneeLoginIdAsync(ImprovementRequest request, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class RequestOwnerResolver : IRequestOwnerResolver {
  /// <summary>사람이 아닌 주체. 신원 없이 들어온 요청·자동 처리가 이 이름으로 적힌다.</summary>
  private const string SystemActor = "system";

  private readonly AppDbContext _db;

  /// <summary>서비스를 생성한다.</summary>
  public RequestOwnerResolver(AppDbContext db) => _db = db;

  /// <inheritdoc />
  /// <remarks>
  /// <para>
  /// <b>계정 연결표를 먼저 본다.</b> 사람이 「계정 연결」 화면에서 직접 이어 준
  /// 것이라 가장 믿을 만하다.
  /// </para>
  /// <para>
  /// <b><c>createdby</c> 는 받침이다.</b> 그 칸에는 등록할 때의 포털 아이디가
  /// 적히지만(<c>RequestEndpoints</c> 의 <c>auditUser</c>), 헬프데스크 자체
  /// 토큰으로 들어온 옛 글에는 내부 아이디나 <c>system</c> 이 적혀 있다 —
  /// 그런 값은 포털 계정이 아니라서 보낼 곳이 되지 못한다.
  /// </para>
  /// </remarks>
  public async Task<string?> OwnerLoginIdAsync(
      ImprovementRequest request, CancellationToken ct = default) {

    var linked = await LinkedLoginIdAsync("customer", request.CustomerId, ct);
    if (!string.IsNullOrWhiteSpace(linked)) return linked;

    return Person(request.CreatedBy);
  }

  /// <inheritdoc />
  /// <remarks>
  /// 받침이 <b><c>admin.loginid</c></b> 다. 담당자 계정은 포털 계정과 같은
  /// 아이디로 만들어 쓰는 일이 많은데(계정을 단일화하며 그렇게 옮겼다), 연결표에
  /// 줄이 없다는 까닭만으로 접수자에게 아무것도 안 보내면 <b>종료 알림이 통째로
  /// 사라진다</b>. 포털에 없는 아이디면 알림 서비스가 「받을 사람 없음」으로
  /// 기록에 남긴다 — 조용히 0 명이 되는 것보다 낫다.
  /// </remarks>
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
