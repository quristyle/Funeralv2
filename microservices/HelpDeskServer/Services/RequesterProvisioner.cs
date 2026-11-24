using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>요청 한 줄이 가리킬 <b>요청자(고객)</b>를 정한다. 없으면 만든다. improvementrequest.customerid 는 customer 를 가리키는 NOT NULL 외래키다.</summary>
public interface IRequesterProvisioner {
  /// <summary>이 요청의 주인이 될 고객 번호를 정한다. 정할 수 없으면 <c>null</c>.</summary>
  /// <param name="me">지금 요청을 보낸 사람</param>
  /// <param name="pickedCustomerId">담당자가 「요청자」에서 고른 고객 번호. 안 골랐으면 0</param>
  /// <param name="auditUser">만들어지는 줄에 남길 작성자</param>
  /// <param name="ct">취소 토큰</param>
  Task<int?> ResolveAsync(HelpdeskPrincipal me, int pickedCustomerId, string auditUser, CancellationToken ct = default);

  /// <summary>연결된 자기 고객 줄의 회사를 지금 포털 회사로 맞춘다. 글을 쓰기 전에도 회사 걸름·같은 회사 알림이 조직도를 따라가게 한다.</summary>
  Task SyncSelfCompanyAsync(HelpdeskPrincipal me, string auditUser, CancellationToken ct = default);
}

/// <inheritdoc />
public class RequesterProvisioner : IRequesterProvisioner {
  private readonly AppDbContext _db;
  private readonly IFuneralAccountLinkService _linkService;
  private readonly ILogger<RequesterProvisioner> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public RequesterProvisioner(
      AppDbContext db,
      IFuneralAccountLinkService linkService,
      ILogger<RequesterProvisioner> logger) {
    _db = db;
    _linkService = linkService;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task<int?> ResolveAsync(
      HelpdeskPrincipal me, int pickedCustomerId, string auditUser, CancellationToken ct = default) {

    // 1. 고객으로 연결된 계정은 언제나 자기 자신이다. 남의 이름으로 쓸 수 없다.
    if (me.IsCustomer && me.HelpdeskUserId is { } linked
        && await _db.Customers.FirstOrDefaultAsync(c => c.Id == linked, ct) is { } self) {
      await SyncCompanyAsync(self, me, auditUser, ct);
      return linked;
    }

    // 2. 담당자가 「요청자」에서 고른 사람. 대신 등록하는 길이다.
    //
    //    실재를 확인하고 쓴다. 없는 번호를 그대로 넣으면 SaveChanges 가
    //    DbUpdateException 을 던지고, 그 문장이 화면에 그대로 나간다.
    if (!me.IsCustomer && pickedCustomerId > 0
        && await _db.Customers.AnyAsync(c => c.Id == pickedCustomerId, ct)) {
      return pickedCustomerId;
    }

    // 3. 그 밖에는 **글을 쓴 사람 자신**이 요청자다. 가리킬 줄이 없으면 만든다.
    return await EnsureSelfAsync(me, auditUser, ct);
  }

  /// <inheritdoc />
  public async Task SyncSelfCompanyAsync(HelpdeskPrincipal me, string auditUser, CancellationToken ct = default) {
    if (!me.HasCompany || string.IsNullOrWhiteSpace(me.JsiniUserId)) return;
    if (!string.Equals(me.LinkedUserType, "customer", StringComparison.OrdinalIgnoreCase) || me.HelpdeskUserId is not { } id) return;

    if (await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) is { } self) {
      await SyncCompanyAsync(self, me, auditUser, ct);
    }
  }

  /// <summary>포털 계정 자신을 가리키는 고객 줄을 찾고, 없으면 만든다. 찾는 열쇠는 로그인 아이디와 이름이 둘 다</summary>
  private async Task<int?> EnsureSelfAsync(HelpdeskPrincipal me, string auditUser, CancellationToken ct) {
    var loginId = me.JsiniUserId;

    // 포털 신원이 없는 요청(헬프데스크 자체 토큰 등)은 만들 근거가 없다.
    if (string.IsNullOrWhiteSpace(loginId)) return null;

    var userName = string.IsNullOrWhiteSpace(me.DisplayName) ? loginId : me.DisplayName!;

    var mine = await _db.Customers
        .FirstOrDefaultAsync(c => c.LoginId == loginId && c.UserName == userName && !c.IsDeleted, ct);

    if (mine is null) {
      mine = new Customer {
        LoginId = loginId,
        UserName = userName,
        Email = me.Email ?? string.Empty,
        // 비밀번호로는 못 들어온다. 로그인은 포털에서만 한다.
        PasswordHash = string.Empty,
        Status = CustomerStatus.Approved,
        // 회사는 포털이 정본이다. 토큰이 실어 준 포털 회사 아이디를 그대로 적는다.
        //
        // `customer.companyid` 는 헬프데스크 회사 표를 가리키는 NOT NULL 외래키였다. 회사 표를 쓰지 않는 지금은
        // **처음부터 포털 회사로 들어간다** — 옮겨 주는 일이 필요 없다.
        CompanyId = me.CompanyId,
        CreatedBy = auditUser,
      };

      _db.Customers.Add(mine);
      await _db.SaveChangesAsync(ct);

      _logger.LogInformation(
          "포털 계정 {LoginId} 의 요청자(고객) 줄을 새로 만들었습니다 — customer#{Id}.", loginId, mine.Id);
    }

    else {
      await SyncCompanyAsync(mine, me, auditUser, ct);
    }

    await LinkAsync(loginId, mine.Id, auditUser, ct);
    return mine.Id;
  }

  /// <summary>
  /// 고객 줄의 회사를 지금 토큰의 포털 회사로 맞춘다. 비어 있던 줄(회사를 나중에 맨 계정)도, 조직도에서 다른 회사로 옮겨 간 사람도 같다 —
  /// 회사 걸름과 같은 회사 알림이 이 칸을 보므로 옛 회사에 남아 있으면 새 회사 사람에게 안 보이고 옛 회사 사람에게 보인다.
  /// 이미 올린 글은 제 칸(improvementrequest.companyid)에 쓸 때의 회사를 들고 있어 함께 옮겨 가지 않는다.
  /// 토큰에 회사가 없으면(무소속으로 뺀 사람·포털 신원이 없는 요청) 근거가 없으므로 그대로 둔다.
  /// </summary>
  private async Task SyncCompanyAsync(Customer customer, HelpdeskPrincipal me, string auditUser, CancellationToken ct) {
    if (!me.HasCompany || string.Equals(customer.CompanyId, me.CompanyId, StringComparison.OrdinalIgnoreCase)) return;

    var before = customer.CompanyId;
    customer.CompanyId = me.CompanyId;
    customer.ModifiedBy = auditUser;
    customer.ModifiedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);

    _logger.LogInformation(
        "customer#{Id} 의 회사를 포털 회사에 맞췄습니다 — {Before} → {After}.", customer.Id, before ?? "(없음)", me.CompanyId);
  }

  /// <summary>다음부터는 신원 해석이 이 줄을 바로 찾도록 연결을 남긴다. 이미 연결이 있으면 손대지 않는다.</summary>
  private async Task LinkAsync(string loginId, int customerId, string auditUser, CancellationToken ct) {
    if (await _db.AuthUserLinks.AnyAsync(l => l.AuthUserId == loginId, ct)) return;

    _db.AuthUserLinks.Add(new AuthUserLink {
      AuthUserId = loginId,
      UserType = "customer",
      HelpdeskUserId = customerId,
      CreatedAt = DateTime.UtcNow,
      CreatedBy = auditUser,
    });

    await _db.SaveChangesAsync(ct);
    _linkService.InvalidateCache(loginId);
  }
}
