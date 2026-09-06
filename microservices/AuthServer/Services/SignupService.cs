using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>가입 신청을 받고, 관리자가 승인·거절한다.</summary>
public interface ISignupService
{
    /// <summary>신청을 받는다. 실패하면 사용자에게 보여 줄 이유가 함께 온다.</summary>
    Task<(bool Ok, string? Error)> RequestAsync(
        SignupRequestDto request, string? requestIp, CancellationToken ct = default);

    /// <summary>승인 대기 목록. 오래된 것이 위로 온다.</summary>
    Task<List<SignupPendingDto>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>승인한다. 이 순간부터 로그인할 수 있다.</summary>
    Task<bool> ApproveAsync(string accountId, string approver, CancellationToken ct = default);

    /// <summary>거절한다. 신청은 사라진다 — 사연은 구현 주석에 있다.</summary>
    Task<bool> RejectAsync(
        string accountId, string approver, string? reason, CancellationToken ct = default);
}

/// <inheritdoc cref="ISignupService"/>
public class SignupService(
    AppDbContext db,
    AccountMailClient mail,
    IConfiguration configuration,
    ILogger<SignupService> logger) : ISignupService
{
    /// <summary>계정 상태를 담아 두는 <c>account_profile_details.detail_type</c>.</summary>
    public const string StatusDetail = "Status";

    /// <summary>신청자가 적은 말을 담아 두는 칸.</summary>
    public const string NoteDetail = "SignupNote";

    /// <summary>승인 대기. 이 상태로는 로그인할 수 없다.</summary>
    public const string StatusPending = "PENDING";

    /// <summary>정상. 계정 관리가 예전부터 쓰던 값이라 그대로 쓴다.</summary>
    public const string StatusActive = "ACTIVE";

    private const string Sender = "AUTH_SIGNUP";

    /// <summary>새 신청이 들어오면 알릴 역할.</summary>
    private string NotifyRole =>
        configuration["Auth:Signup:NotifyRole"] ?? "SYSTEM_ADMINISTRATOR";

    /// <summary>비밀번호 최소 길이. 신청자가 정하는 값이라 여기서만 잰다.</summary>
    private int MinPasswordLength =>
        configuration.GetValue("Auth:Signup:MinPasswordLength", 8);

    /// <summary>
    /// 신청을 계정으로 만들되 <b>상태를 PENDING 으로</b> 둔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [신청서 표를 따로 만들지 않았다]
    /// </para>
    ///
    /// <para>
    /// 계정 상태는 이미 <c>account_profile_details</c> 의 <c>Status</c> 에
    /// 들어 있고 계정 관리 화면이 그 값을 읽고 쓴다. 신청서를 별도 표로 두면
    /// 승인할 때 그 줄을 계정으로 옮겨 적는 코드가 생기고, 그 옮겨 적기가
    /// 어긋나는 날이 온다. <b>처음부터 계정으로 만들고 상태만 다르게</b> 두면
    /// 승인은 값 하나를 바꾸는 일이 된다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>로그인이 상태를 봐야 한다</b>는 것이 이 설계의 전제다
    /// (<c>AuthEndpoints</c>). 그 검사가 빠지면 신청하는 즉시 들어올 수 있다.
    /// </para>
    /// </remarks>
    public async Task<(bool Ok, string? Error)> RequestAsync(
        SignupRequestDto request, string? requestIp, CancellationToken ct = default)
    {
        var loginId = request.LoginId?.Trim() ?? string.Empty;
        var userName = request.UserName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;

        if (loginId.Length == 0 || userName.Length == 0 || email.Length == 0)
        {
            return (false, "아이디 · 이름 · 이메일은 반드시 넣어야 합니다.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
        {
            return (false, "이메일 형식이 올바르지 않습니다.");
        }

        if ((request.Password?.Length ?? 0) < MinPasswordLength)
        {
            return (false, $"비밀번호는 {MinPasswordLength}자 이상이어야 합니다.");
        }

        // 아이디 중복은 알려 준다. 비밀번호 찾기와 달리 이쪽은 **숨길 수가
        // 없다** — 숨기면 신청이 조용히 사라지고 신청자는 영영 기다린다.
        if (await db.Accounts.AnyAsync(a => a.UserId == loginId, ct))
        {
            return (false, "이미 쓰고 있는 아이디입니다. 다른 아이디로 신청해 주십시오.");
        }

        var account = new Account
        {
            UserId = loginId,
            UserName = userName,
            RealName = userName,
            Password = PasswordHasher.Hash(request.Password!),

            // 90일 정책의 기준. 안 채우면 이 계정만 만료 시계가 없다.
            PasswordChangedAt = DateTime.UtcNow,
        };

        db.Accounts.Add(account);

        db.AccountProfileDetails.Add(Detail(account.Id, StatusDetail, StatusPending));
        db.AccountProfileDetails.Add(Detail(account.Id, "Email", email));

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            db.AccountProfileDetails.Add(Detail(account.Id, "Phone", request.Phone.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            db.AccountProfileDetails.Add(Detail(account.Id, NoteDetail, request.Note.Trim()));
        }

        // 계정 관리가 만드는 계정과 같은 자리에 같은 값을 넣어 둔다.
        db.AccountProfileDetails.Add(Detail(account.Id, "HomePath", "/workspace"));

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "가입 신청이 들어왔다: {LoginId} ({Name}, {Ip})", loginId, userName, requestIp);

        // 알림은 곁들이는 일이다. 못 보내도 신청은 이미 저장되었으므로
        // 신청자에게 실패를 돌려주지 않는다 — 승인 화면에는 그대로 보인다.
        await mail.SendToRoleAsync(
            NotifyRole,
            "[JSini 포털] 가입 신청이 들어왔습니다",
            $"""
             <p>새 가입 신청이 있습니다.</p>
             <ul>
               <li>아이디: {Escape(loginId)}</li>
               <li>이름: {Escape(userName)}</li>
               <li>이메일: {Escape(email)}</li>
             </ul>
             <p>포털의 [계정 관리 → 가입 신청] 에서 승인하거나 거절할 수 있습니다.</p>
             """,
            Sender, ct);

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<List<SignupPendingDto>> GetPendingAsync(CancellationToken ct = default)
    {
        // 상태가 PENDING 인 계정만 모은다. 상태 칸이 없는 옛 계정은
        // ACTIVE 로 보므로 여기 걸리지 않는다.
        var pendingIds = await db.AccountProfileDetails
            .Where(d => d.DetailType == StatusDetail && d.Content == StatusPending)
            .Select(d => d.AccountId)
            .ToListAsync(ct);

        if (pendingIds.Count == 0)
        {
            return [];
        }

        var accounts = await db.Accounts
            .Include(a => a.ProfileDetails)
            .Where(a => pendingIds.Contains(a.Id))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return [.. accounts.Select(a => new SignupPendingDto
        {
            Id = a.Id,
            LoginId = a.UserId,
            UserName = a.UserName ?? a.RealName ?? string.Empty,
            Email = Content(a, "Email"),
            Phone = Content(a, "Phone"),
            Note = Content(a, NoteDetail),
            RequestedAt = a.CreatedAt,
        })];
    }

    /// <inheritdoc />
    public async Task<bool> ApproveAsync(string accountId, string approver, CancellationToken ct = default)
    {
        var account = await db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        var status = account?.ProfileDetails?.FirstOrDefault(p => p.DetailType == StatusDetail);

        if (account is null || status is null || status.Content != StatusPending)
        {
            // 대기 중이 아닌 계정을 승인이라는 이름으로 켜지 않는다. 정지시켜
            // 둔 계정이 이 경로로 되살아나면 그것은 승인이 아니라 사고다.
            return false;
        }

        status.Content = StatusActive;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("가입 신청 승인: {LoginId} (승인자 {Approver})", account.UserId, approver);

        var email = Content(account, "Email");
        if (!string.IsNullOrWhiteSpace(email))
        {
            await mail.SendAsync(
                email,
                "[JSini 포털] 가입이 승인되었습니다",
                $"""
                 <p>{Escape(account.UserName ?? account.UserId)} 님, 가입이 승인되었습니다.</p>
                 <p>신청하실 때 정한 아이디({Escape(account.UserId)})와 비밀번호로 로그인하실 수 있습니다.</p>
                 """,
                Sender, ct);
        }

        return true;
    }

    /// <summary>
    /// 거절하면 <b>계정을 지운다.</b>
    /// </summary>
    /// <remarks>
    /// 상태만 <c>REJECTED</c> 로 두면 그 아이디가 영영 묶인다 — 잘못 적어
    /// 거절당한 사람이 같은 아이디로 다시 신청할 수 없다. 게다가 계정 관리
    /// 목록에 쓰지 않는 줄이 쌓인다. 대기 중인 계정은 역할도 즐겨찾기도
    /// 없으므로 지워도 딸려 나갈 것이 없다. 누가 언제 거절했는지는 로그에 남는다.
    /// </remarks>
    public async Task<bool> RejectAsync(
        string accountId, string approver, string? reason, CancellationToken ct = default)
    {
        var account = await db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        var status = account?.ProfileDetails?.FirstOrDefault(p => p.DetailType == StatusDetail);

        if (account is null || status is null || status.Content != StatusPending)
        {
            return false;
        }

        var email = Content(account, "Email");
        var name = account.UserName ?? account.UserId;

        if (account.ProfileDetails is { Count: > 0 })
        {
            db.AccountProfileDetails.RemoveRange(account.ProfileDetails);
        }

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "가입 신청 거절: {LoginId} (처리자 {Approver}, 사유 {Reason})",
            account.UserId, approver, reason ?? "-");

        if (!string.IsNullOrWhiteSpace(email))
        {
            var why = string.IsNullOrWhiteSpace(reason)
                ? string.Empty
                : $"<p>사유: {Escape(reason)}</p>";

            await mail.SendAsync(
                email,
                "[JSini 포털] 가입 신청 결과",
                $"""
                 <p>{Escape(name)} 님, 신청하신 가입이 받아들여지지 않았습니다.</p>
                 {why}
                 <p>문의하실 것이 있으면 담당자에게 연락해 주십시오.</p>
                 """,
                Sender, ct);
        }

        return true;
    }

    private static AccountProfileDetail Detail(string accountId, string type, string content) => new()
    {
        AccountId = accountId,
        DetailType = type,
        Content = content,
        IsPrimary = true,
    };

    private static string? Content(Account account, string type) =>
        account.ProfileDetails?.FirstOrDefault(p => p.DetailType == type)?.Content;

    private static string Escape(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
