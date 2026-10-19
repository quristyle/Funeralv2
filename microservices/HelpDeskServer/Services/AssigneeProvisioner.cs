using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>요청 한 줄을 <b>접수한 사람(담당자)</b>을 정한다. 가리킬 줄이 없으면 만든다. improvementrequest.adminid 는 admin 을 가리키는 외래키다.</summary>
public interface IAssigneeProvisioner {
  /// <summary>지금 요청을 보낸 사람을 가리키는 담당자 번호를 정한다. 담당자 권한이 없거나 포털 신원이 없으면 <c>null</c>.</summary>
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
