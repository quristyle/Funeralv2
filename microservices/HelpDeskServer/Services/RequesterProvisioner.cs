using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// 요청 한 줄이 가리킬 <b>요청자(고객)</b>를 정한다. 없으면 만든다.
/// </summary>
/// <remarks>
/// <para>
/// <c>improvementrequest.customerid</c> 는 <c>customer</c> 를 가리키는
/// <b>NOT NULL 외래키</b>다. 그래서 요청을 쓰려면 그 사람을 가리키는
/// <c>customer</c> 줄이 <b>먼저</b> 있어야 한다.
/// </para>
///
/// <para>
/// 그런데 운영 헬프데스크는 <b>자료가 하나도 없는 새 DB</b> 를 쓴다
/// (<c>web/docs/decisions-needed.md</c> D14 — 「운영도 새 DB(helpdesk)로 옮긴다.
/// 자료이관은 하지 않는다」). 옛 DB(<c>jinrecept.jsini</c>)에 있던 고객 27명은
/// 따라오지 않았다. 그래서 <b>고객이 0명</b>이고, 아무도 요청을 쓸 수 없었다 —
/// 글을 다 쓰고 「등록」을 누르면 이것만 떴다.
/// </para>
///
/// <code>
/// error occurred: An error occurred while saving the entity changes.
/// </code>
///
/// <para>
/// 고객을 먼저 등록하라고 미룰 수도 있지만, 그러면 <b>포털 계정 하나하나를
/// 헬프데스크에 다시 만들어 이어 주는 일</b>을 사람이 해야 한다. 인증·권한은
/// 포털이 단독으로 맡는다(<see cref="HelpdeskPrincipal"/>). 헬프데스크의
/// <c>customer</c> 줄은 <b>업무 자료가 가리킬 대상</b>일 뿐이므로, 처음 글을 쓸 때
/// 포털 신원에서 만들어 주면 된다. 운송관리가 <c>app_user</c> 를 그렇게 만든다.
/// </para>
/// </remarks>
public interface IRequesterProvisioner {
  /// <summary>
  /// 이 요청의 주인이 될 고객 번호를 정한다. 정할 수 없으면 <c>null</c>.
  /// </summary>
  /// <param name="me">지금 요청을 보낸 사람</param>
  /// <param name="pickedCustomerId">담당자가 「요청자」에서 고른 고객 번호. 안 골랐으면 0</param>
  /// <param name="auditUser">만들어지는 줄에 남길 작성자</param>
  /// <param name="ct">취소 토큰</param>
  Task<int?> ResolveAsync(HelpdeskPrincipal me, int pickedCustomerId, string auditUser, CancellationToken ct = default);
}

/// <inheritdoc />
public class RequesterProvisioner : IRequesterProvisioner {
  /// <summary>
  /// 포털 계정으로 만들어 주는 고객이 들어갈 회사 이름.
  /// </summary>
  /// <remarks>
  /// <c>customer.companyid</c> 도 NOT NULL 외래키라 회사가 하나는 있어야 한다.
  /// 포털 토큰이 실어 주는 회사는 <b>이름이 아니라 식별자</b>(<c>jsini</c> ·
  /// GUID)라 그대로 회사 이름으로 쓸 수 없다. 그래서 한 곳으로 모으고, 제
  /// 회사로 옮기는 것은 담당자가 고객 관리에서 한다.
  /// </remarks>
  public const string DefaultCompanyName = "포털 사용자";

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
        && await _db.Customers.AnyAsync(c => c.Id == linked, ct)) {
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

  /// <summary>
  /// 포털 계정 자신을 가리키는 고객 줄을 찾고, 없으면 만든다.
  /// </summary>
  /// <remarks>
  /// 찾는 열쇠는 <b>로그인 아이디와 이름이 둘 다 같은</b> 줄이다. 아이디만 보면
  /// 안 된다 — 포털 <c>admin</c> 과 헬프데스크 <c>admin</c> 은 서로 다른 사람이고
  /// (<see cref="AccountLinkOptions.MatchByLoginId"/>), 그런 줄을 집어 오면
  /// <b>남의 이름으로 요청이 들어간다.</b> 둘 다 같으면 우리가 만든 줄이거나
  /// 같은 사람이다.
  /// </remarks>
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
        CompanyId = await EnsureCompanyAsync(auditUser, ct),
        CreatedBy = auditUser,
      };

      _db.Customers.Add(mine);
      await _db.SaveChangesAsync(ct);

      _logger.LogInformation(
          "포털 계정 {LoginId} 의 요청자(고객) 줄을 새로 만들었습니다 — customer#{Id}.", loginId, mine.Id);
    }

    await LinkAsync(loginId, mine.Id, auditUser, ct);
    return mine.Id;
  }

  /// <summary>
  /// 다음부터는 신원 해석이 이 줄을 바로 찾도록 연결을 남긴다.
  /// </summary>
  /// <remarks>
  /// 이미 연결이 있으면 <b>손대지 않는다.</b> 담당자로 이어 둔 계정
  /// (<c>quristyle → admin#4</c>)의 연결을 고객으로 덮으면 그 사람의 담당자
  /// 권한이 사라진다. 그런 계정은 연결 없이도 위에서 찾은 고객 줄을 쓴다.
  /// </remarks>
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

  /// <summary>포털 계정이 들어갈 회사를 찾고, 없으면 만든다.</summary>
  private async Task<int> EnsureCompanyAsync(string auditUser, CancellationToken ct) {
    var company = await _db.Companies
        .FirstOrDefaultAsync(c => c.Name == DefaultCompanyName, ct);

    if (company is not null) return company.Id;

    company = new CustomerCompany { Name = DefaultCompanyName, CreatedBy = auditUser };
    _db.Companies.Add(company);
    await _db.SaveChangesAsync(ct);

    return company.Id;
  }
}
