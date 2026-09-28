using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// 요청 한 줄을 <b>접수한 사람(담당자)</b>을 정한다. 가리킬 줄이 없으면 만든다.
/// </summary>
/// <remarks>
/// <para>
/// <c>improvementrequest.adminid</c> 는 <c>admin</c> 을 가리키는 외래키다.
/// 그래서 「접수」를 누른 사람을 접수자로 적으려면 그 사람을 가리키는
/// <c>admin</c> 줄이 <b>먼저</b> 있어야 한다.
/// </para>
///
/// <para>
/// 그런데 운영 헬프데스크는 자료를 옮기지 않은 새 DB 를 쓰기 때문에
/// (<c>web/docs/decisions-needed.md</c> D14) <b>담당자가 0명</b>이다.
/// 접수자를 고르게 두면 고를 것이 없고, 없는 번호를 그대로 넣으면
/// <c>SaveChangesAsync</c> 가 <c>DbUpdateException</c> 을 던져
/// 「An error occurred while saving the entity changes.」 한 줄만 화면에 나간다.
/// 요청자(고객)에서 겪은 것과 <b>같은 일</b>이라 같은 길로 푼다 —
/// <see cref="IRequesterProvisioner"/> 참고.
/// </para>
///
/// <para>
/// 인증·권한은 포털이 단독으로 맡는다(<see cref="HelpdeskPrincipal"/>).
/// 헬프데스크의 <c>admin</c> 줄은 <b>업무 자료가 가리킬 대상</b>일 뿐이므로,
/// 담당자 권한이 있는 사람이 처음 무언가를 접수할 때 포털 신원에서 만들어 준다.
/// </para>
///
/// <para>
/// <b><c>auth_user_links</c> 에는 손대지 않는다.</b> 요청자 쪽
/// (<see cref="RequesterProvisioner"/>)은 연결을 남기지만 여기서 그러면
/// 고객으로 이어 둔 계정의 연결을 담당자로 덮어쓴다 — 그 사람이 쓴 글과
/// 댓글이 가리키던 고객 번호가 <b>신원 해석에서 사라진다.</b> 접수자는
/// 로그인 아이디와 이름으로 다시 찾으면 되므로 연결이 필요 없다.
/// </para>
/// </remarks>
public interface IAssigneeProvisioner {
  /// <summary>
  /// 지금 요청을 보낸 사람을 가리키는 담당자 번호를 정한다.
  /// 담당자 권한이 없거나 포털 신원이 없으면 <c>null</c>.
  /// </summary>
  /// <param name="me">지금 요청을 보낸 사람</param>
  /// <param name="auditUser">만들어지는 줄에 남길 작성자</param>
  /// <param name="ct">취소 토큰</param>
  Task<int?> ResolveAsync(HelpdeskPrincipal me, string auditUser, CancellationToken ct = default);
}

/// <inheritdoc />
public class AssigneeProvisioner : IAssigneeProvisioner {
  private readonly AppDbContext _db;
  private readonly ILogger<AssigneeProvisioner> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public AssigneeProvisioner(AppDbContext db, ILogger<AssigneeProvisioner> logger) {
    _db = db;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task<int?> ResolveAsync(
      HelpdeskPrincipal me, string auditUser, CancellationToken ct = default) {

    // 1. 담당자로 이어 둔 계정은 그 줄이 곧 자기 자신이다.
    if (me.IsLinkedAdmin && me.HelpdeskUserId is { } linked
        && await _db.Admins.AnyAsync(a => a.Id == linked && !a.IsDeleted, ct)) {
      return linked;
    }

    // 2. 담당자 권한이 없는 사람은 접수자가 될 수 없다. 여기서 줄을 만들면
    //    **고객이 담당자 표에 들어간다** — 배정 알림도 그 사람에게 간다.
    if (!me.IsAdmin) return null;

    var loginId = me.JsiniUserId;

    // 포털 신원이 없는 요청(헬프데스크 자체 토큰 등)은 만들 근거가 없다.
    if (string.IsNullOrWhiteSpace(loginId)) return null;

    var userName = string.IsNullOrWhiteSpace(me.DisplayName) ? loginId : me.DisplayName!;

    // 3. 찾는 열쇠는 **로그인 아이디와 이름이 둘 다 같은** 줄이다. 아이디만
    //    보면 안 된다 — 포털 `admin` 과 헬프데스크 `admin` 은 서로 다른
    //    사람이라(`AccountLinkOptions.MatchByLoginId`) 그런 줄을 집어 오면
    //    **남의 이름으로 접수된다.**
    var mine = await _db.Admins
        .FirstOrDefaultAsync(a => a.LoginId == loginId && a.UserName == userName && !a.IsDeleted, ct);

    if (mine is not null) return mine.Id;

    mine = new Admin {
      LoginId = loginId,
      UserName = userName,
      Email = me.Email ?? string.Empty,
      // 비밀번호로는 못 들어온다. 로그인은 포털에서만 한다. 그래서
      // 「최초 로그인시 비밀번호 변경」도 걸지 않는다 — 걸어 두면
      // 헬프데스크 자체 로그인 화면이 영영 못 넘어가는 줄이 하나 생긴다.
      PasswordHash = string.Empty,
      MustChangePassword = false,
      CreatedBy = auditUser,
    };

    _db.Admins.Add(mine);
    await _db.SaveChangesAsync(ct);

    _logger.LogInformation(
        "포털 계정 {LoginId} 의 담당자(접수자) 줄을 새로 만들었습니다 — admin#{Id}.", loginId, mine.Id);

    return mine.Id;
  }
}
