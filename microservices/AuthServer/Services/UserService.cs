using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 사용자 정보 관련 비즈니스 로직 처리 서비스
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IRoleAssignmentService _roleAssignments;
    private readonly IConfiguration _config;
    private readonly IEmailVerificationService? _emailVerification;
    private readonly AccountMailClient? _mail;
    private readonly SignupNotifyClient? _notify;
    private readonly ILogger<UserService>? _logger;

    /// <summary>
    /// UserService 생성자
    /// </summary>
    public UserService(
        AppDbContext db,
        IRoleAssignmentService roleAssignments,
        IConfiguration config,
        IEmailVerificationService? emailVerification = null,
        AccountMailClient? mail = null,
        SignupNotifyClient? notify = null,
        ILogger<UserService>? logger = null)
    {
        _db = db;
        _roleAssignments = roleAssignments;
        _config = config;
        _emailVerification = emailVerification;
        _mail = mail;
        _notify = notify;
        _logger = logger;
    }

    /// <summary><c>account_profile_details</c> 의 <c>Watermark</c>. 없으면 켜진 것이다.</summary>
    private const string WatermarkDetail = "Watermark";

    /// <summary>
    /// 개발 업무용 확장 속성의 접두사 — <c>Dev.BpId</c> · <c>Dev.NotebookNo</c> ….
    /// </summary>
    /// <remarks>
    /// <para>
    /// [칸을 스물다섯 개 만들지 않는다]
    /// </para>
    ///
    /// <para>
    /// 프로젝트관리의 [개발자 관리] 화면이 쓰던 속성을 계정으로 옮기면서
    /// (<c>docs/projmng-account-merge.md</c>) 표를 손대지 않았다.
    /// <c>account_profile_details</c> 가 이미 <b>열쇠-값</b>이라 넣을 자리가
    /// 있었다 — 속성을 더할 때 마이그레이션이 없다.
    /// </para>
    ///
    /// <para>
    /// [접두사로 가두는 까닭]
    /// </para>
    ///
    /// <para>
    /// 같은 표에 <c>MsaCompany</c>·<c>SecurityEmail</c>·<c>HomePath</c> 처럼
    /// <b>관리 화면이 건드리면 안 되는 것</b>이 섞여 있다. 접두사로 가두면
    /// 화면이 통째로 덮어써도 그것들은 손대지 않는다.
    /// </para>
    /// </remarks>
    private const string DevPrefix = "Dev.";

    /// <summary>그 계정의 <c>Dev.*</c> 속성. 접두사를 뗀 이름으로 돌려준다.</summary>
    private static Dictionary<string, string?> DevAttributesOf(Entities.Account account) =>
        account.ProfileDetails?
            .Where(p => !p.IsDeleted && p.DetailType.StartsWith(DevPrefix, StringComparison.Ordinal))
            .ToDictionary(p => p.DetailType[DevPrefix.Length..], p => (string?)p.Content)
        ?? [];

    /// <summary>
    /// <c>Dev.*</c> 속성을 통째로 맞춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 보내온 사전이 <b>그 계정의 <c>Dev.*</c> 전부</b>다 — 빠진 열쇠는 지운다.
    /// 화면이 칸을 비워 저장하는 것이 「지운다」는 뜻이어야 하기 때문이다.
    /// </para>
    ///
    /// <para>
    /// <c>null</c> 을 보내면(사전 자체가 없으면) <b>건드리지 않는다</b> —
    /// 이 속성을 모르는 다른 화면·옛 클라이언트가 저장해도 값이 사라지지 않게.
    /// 사진·생년월일·워터마크와 같은 규칙이다.
    /// </para>
    /// </remarks>
    private void SyncDevAttributes(Entities.Account account, Dictionary<string, string?>? values)
    {
        if (values is null) return;

        var wanted = values
            .Where(v => !string.IsNullOrWhiteSpace(v.Key))
            .ToDictionary(v => DevPrefix + v.Key.Trim(), v => v.Value?.Trim());

        var existing = account.ProfileDetails?
            .Where(p => p.DetailType.StartsWith(DevPrefix, StringComparison.Ordinal))
            .ToList() ?? [];

        foreach (var detail in existing)
        {
            // 값이 비었으면 줄을 남기지 않는다. 빈 글자를 담아 두면
            // 「안 적었다」와 「비워 두기로 했다」가 같은 그림이 된다.
            if (!wanted.TryGetValue(detail.DetailType, out var content)
                || string.IsNullOrWhiteSpace(content))
            {
                _db.AccountProfileDetails.Remove(detail);
                continue;
            }

            // 지운 표시가 붙어 있으면 되살린다. 읽는 쪽이 그 표시를 보므로
            // (`DevAttributesOf`) 안 풀면 **저장은 됐는데 안 보인다**가 된다.
            if (detail.Content != content || detail.IsDeleted)
            {
                detail.Content = content;
                detail.IsDeleted = false;
                _db.Entry(detail).State = EntityState.Modified;
            }

            wanted.Remove(detail.DetailType);
        }

        foreach (var (type, content) in wanted)
        {
            if (string.IsNullOrWhiteSpace(content)) continue;

            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = type,
                Content = content,
                IsPrimary = true,
            });
        }
    }

    /// <summary>
    /// 워터마크 설정을 쓴다.
    ///
    /// <para>
    /// <b>켜는 쪽도 값을 적는다.</b> 「없으면 켜짐」이라 지워도 결과는 같지만,
    /// 적어 두면 <b>관리자가 한 번 보고 정했다</b>는 사실이 남는다 — 나중에
    /// 기본값을 바꿀 일이 생겼을 때 「손 안 댄 계정」과 「켜기로 정한 계정」을
    /// 가를 수 있다.
    /// </para>
    /// </summary>
    private void UpsertWatermark(Entities.Account account, bool on)
    {
        var content = on ? "on" : "off";
        var detail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == WatermarkDetail);

        if (detail is not null)
        {
            detail.Content = content;
            _db.Entry(detail).State = EntityState.Modified;
            return;
        }

        _db.AccountProfileDetails.Add(new AccountProfileDetail
        {
            AccountId = account.Id,
            DetailType = WatermarkDetail,
            Content = content,
            IsPrimary = true,
        });
    }

    /// <summary>
    /// 그 계정의 워터마크 설정. <b>「off」 라고 적혀 있을 때만 끈다</b> —
    /// 값이 없거나 알 수 없는 글자면 켜 둔다. 이 표시는 빠지는 쪽이 사고라,
    /// 해석이 안 되는 값을 「꺼짐」으로 읽지 않는다.
    /// </summary>
    private static bool WatermarkOf(Entities.Account account) =>
        !string.Equals(
            account.ProfileDetails?.FirstOrDefault(p => p.DetailType == WatermarkDetail)?.Content,
            "off",
            StringComparison.OrdinalIgnoreCase);

    private static string ProviderDisplayName(string provider) => provider.ToLowerInvariant() switch
    {
        "kakao" => "카카오",
        "naver" => "네이버",
        "google" => "구글",
        var other => other
    };

    /// <summary>
    /// 사용자 정보를 조회하고 DTO로 변환하여 반환
    /// </summary>
    /// <param name="userIdOrKey">사용자 아이디 또는 고유 키</param>
    public async Task<UserInfoDto?> GetUserInfoAsync(string userIdOrKey)
    {
        // 아이디 또는 UserId로 계정 조회
        var account = await _db.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userIdOrKey || a.Id == userIdOrKey);

        if (account == null) return null;

        var introduction = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Introduction")?.Content;
        var phone = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone")?.Content;
        var email = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email")?.Content;
        var homePath = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "HomePath")?.Content;
        var avatar = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar")?.Content;
        var avatarGroupId = account.AvatarGroupId;
        // 이관으로 만들어진 계정만 갖고 있다. 화면이 저쪽 서비스의 자기 레코드를 찾을 때 쓴다.
        var msaSource = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "MsaSource")?.Content;

        // 역할은 실제 배정값을 내려준다.
        // 예전에는 무조건 "super" 한 개를 만들어 보냈다. 화면 접근 제어가 백엔드 메뉴 기준이라
        // 당장 티가 나지 않았을 뿐, 이 값을 보고 판단하는 코드가 생기면 전부 관리자로 보인다.
        // 역할은 세 단계로 걸 수 있다 — 회사 · 부서 · 사람. **셋을 모두 합친다.**
        // 로그인 토큰과 같은 규칙을 써야 화면과 실제 권한이 어긋나지 않는다.
        var effective = await _roleAssignments.ResolveEffectiveRolesAsync(account.Id);
        var roleIds = effective.RoleIds;
        var roleNames = effective.RoleNames;

        var securityPhone = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityPhone")?.Content == "true";
        var securityQuestion = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityQuestion")?.Content == "true";
        var securityEmail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityEmail")?.Content == "true";
        var securityMfa = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityMfa")?.Content == "true";

        // 비밀번호 사용 기간. 화면에 보여 줄 값을 만드는 것이고, 실제 차단은 게이트웨이가 한다.
        var utcNow = DateTime.UtcNow;
        var expiryDays = PasswordPolicy.ExpiryDays(_config);
        var policyOn = PasswordPolicy.IsEnabled(expiryDays);

        var systemMessage = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SystemMessage")?.Content == "true";
        var todoTask = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "TodoTask")?.Content == "true";
        var accountPasswordNotify = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "AccountPasswordNotify")?.Content == "true";

        var emailDetails = account.ProfileDetails?.Where(p => p.DetailType == "Email").Select(p => p.Content).Where(c => c != null).ToList() ?? new List<string>();
        var phoneDetails = account.ProfileDetails?.Where(p => p.DetailType == "Phone").Select(p => p.Content).Where(c => c != null).ToList() ?? new List<string>();

        // 소셜 로그인 연결 정보 조회
        var accountSocials = await _db.AccountSocialLogins
            .Where(l => l.AccountId == account.Id && !l.IsDeleted)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        var providers = accountSocials
            .Select(s => s.Provider)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 소셜 연결의 이메일도 목록에 누락되지 않도록 중복 없이 합침
        foreach (var s in accountSocials)
        {
            if (!string.IsNullOrWhiteSpace(s.Email) && !emailDetails.Contains(s.Email, StringComparer.OrdinalIgnoreCase))
            {
                emailDetails.Add(s.Email);
            }
        }

        // 소셜 결합(Merge) 기록 확인 및 결합된 이메일/전화번호 보완
        var mergeDetails = account.ProfileDetails?.Where(p => p.DetailType == "SocialMerge" || p.DetailType == "MergedSocial").ToList();
        var isMerged = mergeDetails is { Count: > 0 };
        if (mergeDetails != null)
        {
            foreach (var md in mergeDetails)
            {
                if (!string.IsNullOrWhiteSpace(md.Content) && !providers.Contains(md.Content, StringComparer.OrdinalIgnoreCase))
                {
                    providers.Add(md.Content);
                }

                if (!string.IsNullOrWhiteSpace(md.Remark) && md.Remark.Contains('|'))
                {
                    var parts = md.Remark.Split('|');
                    var mEmail = parts.Length > 0 ? parts[0] : null;
                    var mPhone = parts.Length > 1 ? parts[1] : null;
                    if (!string.IsNullOrWhiteSpace(mEmail) && !emailDetails.Contains(mEmail, StringComparer.OrdinalIgnoreCase))
                    {
                        emailDetails.Add(mEmail);
                    }
                    if (!string.IsNullOrWhiteSpace(mPhone) && !phoneDetails.Contains(mPhone, StringComparer.OrdinalIgnoreCase))
                    {
                        phoneDetails.Add(mPhone);
                    }
                }
            }
        }

        var isLinked = providers.Count > 0;
        var providerNames = providers.Select(ProviderDisplayName).ToList();
        var providerText = providerNames.Count > 0 ? string.Join(", ", providerNames) : null;

        // 결합된 소셜 계정 상세 목록 구성
        var mergedSocialAccounts = new List<SocialMergedAccountDto>();
        foreach (var s in accountSocials)
        {
            var matchingMerge = mergeDetails?.FirstOrDefault(m => string.Equals(m.Content, s.Provider, StringComparison.OrdinalIgnoreCase));
            mergedSocialAccounts.Add(new SocialMergedAccountDto
            {
                Provider = s.Provider,
                ProviderName = ProviderDisplayName(s.Provider),
                ProviderUserId = s.ProviderUserId,
                Email = s.Email,
                DisplayName = s.DisplayName,
                LinkedAt = s.CreatedAt,
                LastLoginAt = s.LastLoginAt,
                IsMerged = matchingMerge != null || isMerged,
                MergedLoginId = matchingMerge?.Label,
                Remark = matchingMerge?.Remark
            });
        }

        // AccountSocialLogins에는 없지만 mergeDetails에만 있는 내역이 있으면 보완
        if (mergeDetails != null)
        {
            foreach (var md in mergeDetails)
            {
                if (!string.IsNullOrWhiteSpace(md.Content) && !mergedSocialAccounts.Any(m => string.Equals(m.Provider, md.Content, StringComparison.OrdinalIgnoreCase)))
                {
                    var parts = (md.Remark ?? "").Split('|');
                    var mEmail = parts.Length > 0 ? parts[0] : null;
                    mergedSocialAccounts.Add(new SocialMergedAccountDto
                    {
                        Provider = md.Content,
                        ProviderName = ProviderDisplayName(md.Content),
                        Email = mEmail,
                        DisplayName = md.Label,
                        LinkedAt = md.CreatedAt,
                        IsMerged = true,
                        MergedLoginId = md.Label,
                        Remark = md.Remark
                    });
                }
            }
        }

        // 프론트엔드 요구사항에 맞춰 DTO 구성
        return new UserInfoDto
        {
            Id = account.Id,
            UserId = account.UserId,
            Username = account.UserId,
            RealName = account.UserName,
            CompanyName = account.Company?.Name,
            DeptName = account.Department?.Name,
            Avatar = !string.IsNullOrEmpty(avatar) ? avatar : "https://gw.alipayobjects.com/zos/antfincdn/XAosXuNZyF/BiazfanxmamNRoxxVxka.png",
            AvatarGroupId = avatarGroupId,
            Watermark = WatermarkOf(account),
            Desc = (emailDetails.FirstOrDefault() ?? email) ?? "등록된 설명이 없습니다.",
            HomePath = homePath ?? "/workspace",
            Roles = roleIds,
            RoleNames = roleNames,
            MsaSource = msaSource,
            Introduction = introduction,
            Phone = phoneDetails.FirstOrDefault() ?? phone,
            Phones = phoneDetails,
            Email = emailDetails.FirstOrDefault() ?? email,
            Emails = emailDetails,
            SocialProviders = providers,
            IsSocialLinked = isLinked,
            IsSocialMerged = isMerged,
            SocialProviderText = providerText,
            MergedSocialAccounts = mergedSocialAccounts,
            BirthDate = account.BirthDate,
            BirthDateIsLunar = account.BirthDateIsLunar,
            SecurityPhone = securityPhone,
            SecurityQuestion = securityQuestion,
            SecurityEmail = securityEmail,
            SecurityMfa = securityMfa,
            SystemMessage = systemMessage,
            TodoTask = todoTask,
            AccountPasswordNotify = accountPasswordNotify,

            // 계정 이력 (읽기 전용)
            CreatedAt = account.CreatedAt,
            LastLoginAt = account.LastLoginAt,
            LastLoginIp = account.LastLoginIp,
            PasswordChangedAt = account.PasswordChangedAt,
            PasswordExpiresAt = policyOn && account.PasswordChangedAt is not null
                ? PasswordPolicy.ExpiresAt(account.PasswordChangedAt.Value, expiryDays)
                : null,
            PasswordExpiryDays = policyOn ? expiryDays : null,
            PasswordDaysRemaining = PasswordPolicy.DaysRemaining(account.PasswordChangedAt, expiryDays, utcNow),
            PasswordExpired = PasswordPolicy.IsExpired(account.PasswordChangedAt, expiryDays, utcNow)
        };
    }

    /// <summary>
    /// 전체 계정 목록을 조회합니다.
    /// </summary>
    public async Task<List<AccountDto>> GetAccountsAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .ToListAsync();

        // 모든 역할 매핑 정보 조회
        var roleAccounts = await _db.RoleAccounts
            .Include(ra => ra.Role)
            .ToListAsync();

        // 소셜 로그인 연결 정보 조회
        var socialLogins = await _db.AccountSocialLogins
            .Where(l => !l.IsDeleted)
            .ToListAsync();

        var socialMap = socialLogins
            .GroupBy(l => l.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList()
            );

        // accountId 별로 역할 그룹화
        var roleMap = roleAccounts
            .Where(ra => ra.Role != null)
            .GroupBy(ra => ra.AccountId)
            .ToDictionary(
                g => g.Key,
                g => new {
                    RoleIds = g.Select(ra => ra.RoleId).ToList(),
                    RoleNames = g.Select(ra => ra.Role!.Name).ToList()
                }
            );

        return accounts.Select(a => {
            var emailDetails = a.ProfileDetails?.Where(p => p.DetailType == "Email").Select(p => p.Content).Where(c => c != null).ToList() ?? new List<string>();
            var phoneDetails = a.ProfileDetails?.Where(p => p.DetailType == "Phone").Select(p => p.Content).Where(c => c != null).ToList() ?? new List<string>();

            socialMap.TryGetValue(a.Id, out var accountSocials);
            var providers = accountSocials?
                .Select(s => s.Provider)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            // 소셜 연결의 이메일도 목록에 누락되지 않도록 중복 없이 합침
            if (accountSocials != null)
            {
                foreach (var s in accountSocials)
                {
                    if (!string.IsNullOrWhiteSpace(s.Email) && !emailDetails.Contains(s.Email, StringComparer.OrdinalIgnoreCase))
                    {
                        emailDetails.Add(s.Email);
                    }
                }
            }

            // 소셜 결합(Merge) 기록 확인 및 결합된 이메일/전화번호 보완
            var mergeDetails = a.ProfileDetails?.Where(p => p.DetailType == "SocialMerge" || p.DetailType == "MergedSocial").ToList();
            var isMerged = mergeDetails is { Count: > 0 };
            if (mergeDetails != null)
            {
                foreach (var md in mergeDetails)
                {
                    if (!string.IsNullOrWhiteSpace(md.Content) && !providers.Contains(md.Content, StringComparer.OrdinalIgnoreCase))
                    {
                        providers.Add(md.Content);
                    }

                    if (!string.IsNullOrWhiteSpace(md.Remark) && md.Remark.Contains('|'))
                    {
                        var parts = md.Remark.Split('|');
                        var mEmail = parts.Length > 0 ? parts[0] : null;
                        var mPhone = parts.Length > 1 ? parts[1] : null;
                        if (!string.IsNullOrWhiteSpace(mEmail) && !emailDetails.Contains(mEmail, StringComparer.OrdinalIgnoreCase))
                        {
                            emailDetails.Add(mEmail);
                        }
                        if (!string.IsNullOrWhiteSpace(mPhone) && !phoneDetails.Contains(mPhone, StringComparer.OrdinalIgnoreCase))
                        {
                            phoneDetails.Add(mPhone);
                        }
                    }
                }
            }

            var isLinked = providers.Count > 0;
            var providerNames = providers.Select(ProviderDisplayName).ToList();
            var providerText = providerNames.Count > 0 ? string.Join(", ", providerNames) : null;

            var emailDetail = emailDetails.FirstOrDefault();
            var phoneDetail = phoneDetails.FirstOrDefault();
            var statusDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");
            var avatarDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar");

            roleMap.TryGetValue(a.Id, out var rolesInfo);

            return new AccountDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail,
                Emails = emailDetails!,
                Phone = phoneDetail,
                Phones = phoneDetails!,
                Status = statusDetail?.Content ?? "ACTIVE",

                // 소셜 연동 및 결합 정보
                SocialProviders = providers,
                IsSocialLinked = isLinked,
                IsSocialMerged = isMerged,
                SocialProviderText = providerText,

                // 값이 없으면 켜진 것으로 본다. 설정을 안 건드린 계정이
                // 조용히 꺼지면 워터마크가 있는 이유가 사라진다.
                Watermark = WatermarkOf(a),
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,

                // 「들어온 때」와 「마지막으로 쓴 때」. 둘 다 계정 표의 칸이라
                // 조인이 없다 — 기록 표를 줄마다 집계하면 예순 줄짜리 목록이
                // 수만 줄을 훑게 된다(DTO 머리말).
                LastLoginAt = a.LastLoginAt,
                LastLoginIp = a.LastLoginIp,
                LastSeenAt = a.LastSeenAt,

                RoleIds = rolesInfo?.RoleIds ?? new List<string>(),
                RoleNames = rolesInfo?.RoleNames ?? new List<string>(),

                // DTO 에는 예전부터 있었는데 여기서 채우지 않아 늘 null 이었다.
                // `ProfileDetails` 는 이미 Include 로 읽고 있으므로 추가 조회가 없다.
                Avatar = avatarDetail?.Content,
                AvatarGroupId = a.AvatarGroupId,

                // 생일 — 정본이 계정이라 계정 관리 화면이 여기서 읽고 고친다.
                BirthDate = a.BirthDate,
                BirthDateIsLunar = a.BirthDateIsLunar,
                BirthdayCelebrated = a.BirthdayCelebrated,

                // 개발 업무용 확장 속성(사번 · 장비 · 계정 발급 현황 …).
                // `ProfileDetails` 는 이미 Include 로 읽고 있어 추가 조회가 없다.
                DevAttributes = DevAttributesOf(a),
            };
        }).ToList();
    }

    /// <summary>
    /// 신규 계정을 생성합니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [비밀번호를 평문으로 넣던 자리다]
    /// </para>
    ///
    /// <para>
    /// 한동안 <c>Password = "1234"</c> 였다. <see cref="PasswordHasher.Verify"/> 가
    /// 해시 형식이 아닌 저장값을 평문으로 보고 비교하는(의도된 하위호환) 덕분에
    /// 로그인은 됐고, 로그인에 성공하면 그때 해시로 승격됐다. 문제는
    /// <b>한 번도 로그인하지 않은 계정</b>이다 — 그런 계정은 DB 에 비밀번호가
    /// 그대로 보이는 채로 남는다. 만들어만 두고 아직 안 쓰는 계정이라 오래 남는다.
    /// </para>
    ///
    /// <para>
    /// 지금은 <see cref="InitialPassword"/> 가 정한 값을 해시해 넣고, 발급한
    /// 평문은 <see cref="AccountDto.InitialPassword"/> 로 <b>이 응답에만</b> 실어
    /// 보낸다. 그리고 만료 시계를 이미 지난 시각으로 두어 첫 로그인에서 바꾸게 한다.
    /// </para>
    /// </remarks>
    public async Task<AccountDto> CreateAccountAsync(CreateAccountDto dto)
    {
        string? validDeptId = null;
        string? companyId = null;
        if (!string.IsNullOrEmpty(dto.DeptId))
        {
            var dept = await _db.Departments.FindAsync(dto.DeptId);
            if (dept != null)
            {
                validDeptId = dto.DeptId;
                companyId = dept.CompanyId;
            }
        }

        // 발급한 평문은 여기서만 손에 있다. 저장은 해시로 하고, 이 값은
        // 응답에 한 번 실어 보낸 뒤 버린다 — 로그에도 남기지 않는다.
        var initialPassword = InitialPassword.Issue(_config);

        var account = new Account
        {
            UserId = dto.LoginId,
            UserName = dto.UserName,
            RealName = dto.UserName,
            Password = PasswordHasher.Hash(initialPassword),

            // 첫 로그인에서 곧바로 바꾸게 한다. 90일 정책이 그대로 그 일을 하므로
            // 「첫 로그인인가」를 따로 두지 않는다 — PasswordPolicy 참고.
            PasswordChangedAt = PasswordPolicy.AlreadyExpiredAt(
                PasswordPolicy.ExpiryDays(_config), DateTime.UtcNow),

            DepartmentId = validDeptId,
            CompanyId = companyId,
            BirthDate = dto.BirthDate,
            BirthDateIsLunar = dto.BirthDateIsLunar,
            BirthdayCelebrated = dto.BirthdayCelebrated
        };

        _db.Accounts.Add(account);

        // 등록할 때 끄고 만들 수 있다. 켜는 것은 기본값이라 적지 않는다 —
        // 값이 없는 것이 곧 켜진 것이다.
        if (dto.Watermark is false)
        {
            UpsertWatermark(account, false);
        }

        // 개발 업무용 확장 속성. 등록할 때 함께 적을 수 있다 — 안 적으면
        // (`null`) 아무 줄도 만들지 않는다.
        SyncDevAttributes(account, dto.DevAttributes);

        var emailsToSave = dto.Emails?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Email) && !emailsToSave.Contains(dto.Email))
            emailsToSave.Insert(0, dto.Email);
            
        foreach (var em in emailsToSave)
        {
            if (!string.IsNullOrWhiteSpace(em))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Email",
                    Content = em,
                    IsPrimary = em == emailsToSave.First()
                });
            }
        }

        var phonesToSave = dto.Phones?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Phone) && !phonesToSave.Contains(dto.Phone))
            phonesToSave.Insert(0, dto.Phone);
            
        foreach (var ph in phonesToSave)
        {
            if (!string.IsNullOrWhiteSpace(ph))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Phone",
                    Content = ph,
                    IsPrimary = ph == phonesToSave.First()
                });
            }
        }

        _db.AccountProfileDetails.Add(new AccountProfileDetail
        {
            AccountId = account.Id,
            DetailType = "Status",
            Content = dto.Status ?? "ACTIVE",
            IsPrimary = true
        });

        _db.AccountProfileDetails.Add(new AccountProfileDetail
        {
            AccountId = account.Id,
            DetailType = "HomePath",
            Content = "/workspace",
            IsPrimary = true
        });

        // 역할 매핑 추가
        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            foreach (var roleId in dto.RoleIds)
            {
                _db.RoleAccounts.Add(new RoleAccount
                {
                    AccountId = account.Id,
                    RoleId = roleId
                });
            }
        }

        await _db.SaveChangesAsync();

        string? deptName = null;
        if (!string.IsNullOrEmpty(dto.DeptId))
        {
            var dept = await _db.Departments.FindAsync(dto.DeptId);
            deptName = dept?.Name;
        }

        // 반환할 역할명 조회
        var roleNames = new List<string>();
        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            roleNames = await _db.Roles
                .Where(r => dto.RoleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToListAsync();
        }

        return new AccountDto
        {
            Id = account.Id,
            LoginId = account.UserId,
            UserName = account.UserName,
            Email = emailsToSave.FirstOrDefault(),
            Emails = emailsToSave,
            Phone = phonesToSave.FirstOrDefault(),
            Phones = phonesToSave,
            Status = dto.Status ?? "ACTIVE",
            DeptId = account.DepartmentId,
            DeptName = deptName,
            CreatedAt = account.CreatedAt,
            RoleIds = dto.RoleIds ?? new List<string>(),
            RoleNames = roleNames,
            BirthDate = account.BirthDate,
            BirthDateIsLunar = account.BirthDateIsLunar,
            BirthdayCelebrated = account.BirthdayCelebrated,

            // 방금 넣은 것을 그대로 돌려준다 — `SyncDevAttributes` 가 빈 값을
            // 걸러 내므로 DB 에 들어간 것과 한 칸씩 다를 수 있다. 등록 응답은
            // 발급 비밀번호를 보여 주는 자리라 목록을 다시 읽는다.
            DevAttributes = dto.DevAttributes ?? [],

            // 발급한 평문을 돌려주는 유일한 자리. 다음 조회부터는 null 이다.
            InitialPassword = initialPassword
        };
    }

    /// <summary>
    /// 기존 계정 정보를 수정합니다.
    /// </summary>
    public async Task<bool> UpdateAccountAsync(string id, UpdateAccountDto dto)
    {
        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null) return false;

        account.UserName = dto.UserName;
        account.RealName = dto.UserName;

        // 생일 — 정본이 계정이라 계정 관리 화면의 저장이 곧 생일 수정이다.
        account.BirthDate = dto.BirthDate;
        account.BirthDateIsLunar = dto.BirthDateIsLunar;
        account.BirthdayCelebrated = dto.BirthdayCelebrated;

        // 워터마크. `null` 은 「건드리지 않음」이라 그 값을 보내지 않는
        // 호출(다른 화면·옛 클라이언트)이 설정을 지우지 않는다.
        if (dto.Watermark is { } watermark)
        {
            UpsertWatermark(account, watermark);
        }

        // 개발 업무용 확장 속성. `null` 이면 건드리지 않는다(머리말).
        SyncDevAttributes(account, dto.DevAttributes);

        // 부서가 있으면 그 부서의 회사를 소속으로 삼는다. 부서 없이 회사만
        // 고른 계정은 회사 소속을 유지한다.
        if (!string.IsNullOrEmpty(dto.DeptId))
        {
            var dept = await _db.Departments.FindAsync(dto.DeptId);
            if (dept != null)
            {
                account.DepartmentId = dto.DeptId;
                account.CompanyId = dept.CompanyId;
            }
            else
            {
                account.DepartmentId = null;
                account.CompanyId = null;
            }
        }
        else if (!string.IsNullOrEmpty(dto.CompanyId))
        {
            var companyExists = await _db.Companies
                .AnyAsync(c => c.Id == dto.CompanyId);
            account.DepartmentId = null;
            account.CompanyId = companyExists ? dto.CompanyId : null;
        }
        else
        {
            account.DepartmentId = null;
            account.CompanyId = null;
        }

        // Email 업데이트
        var existingEmails = account.ProfileDetails?.Where(p => p.DetailType == "Email").ToList() ?? new List<AccountProfileDetail>();
        foreach (var e in existingEmails) _db.AccountProfileDetails.Remove(e);
        
        var emailsToSave = dto.Emails?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Email) && !emailsToSave.Contains(dto.Email))
            emailsToSave.Insert(0, dto.Email);
            
        foreach (var em in emailsToSave)
        {
            if (!string.IsNullOrWhiteSpace(em))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Email",
                    Content = em,
                    IsPrimary = em == emailsToSave.First()
                });
            }
        }

        // Phone 업데이트
        var existingPhones = account.ProfileDetails?.Where(p => p.DetailType == "Phone").ToList() ?? new List<AccountProfileDetail>();
        foreach (var p in existingPhones) _db.AccountProfileDetails.Remove(p);
        
        var phonesToSave = dto.Phones?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Phone) && !phonesToSave.Contains(dto.Phone))
            phonesToSave.Insert(0, dto.Phone);
            
        foreach (var ph in phonesToSave)
        {
            if (!string.IsNullOrWhiteSpace(ph))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Phone",
                    Content = ph,
                    IsPrimary = ph == phonesToSave.First()
                });
            }
        }

        // Status 업데이트
        var statusDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");
        if (statusDetail != null)
        {
            statusDetail.Content = dto.Status ?? "ACTIVE";
            _db.Entry(statusDetail).State = EntityState.Modified;
        }
        else
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Status",
                Content = dto.Status ?? "ACTIVE",
                IsPrimary = true
            });
        }

        // 역할 매핑 업데이트
        var existingRoleAccounts = await _db.RoleAccounts
            .Where(ra => ra.AccountId == id)
            .ToListAsync();
        
        _db.RoleAccounts.RemoveRange(existingRoleAccounts);

        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            foreach (var roleId in dto.RoleIds)
            {
                _db.RoleAccounts.Add(new RoleAccount
                {
                    AccountId = id,
                    RoleId = roleId
                });
            }
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 계정을 삭제합니다.
    /// </summary>
    public async Task<bool> DeleteAccountAsync(string id)
    {
        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null) return false;

        if (account.ProfileDetails != null)
        {
            _db.AccountProfileDetails.RemoveRange(account.ProfileDetails);
        }

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 관리자가 그 계정의 비밀번호를 초기화한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [등록과 같은 길을 쓴다]
    /// </para>
    ///
    /// <para>
    /// 발급(<see cref="InitialPassword.Issue"/>) · 해시 저장 · 만료 시계를
    /// <see cref="CreateAccountAsync"/> 와 똑같이 한다. 「관리자가 건네주는
    /// 비밀번호」라는 점에서 둘은 같은 일이고, 갈라 적으면 한쪽만 고쳐져
    /// <b>초기화한 계정만 비밀번호를 안 바꿔도 되는</b> 식으로 어긋난다.
    /// </para>
    ///
    /// <para>
    /// [발급한 평문은 여기서만 손에 있다]
    /// </para>
    ///
    /// <para>
    /// 저장은 해시뿐이라 돌려준 뒤에는 서버에도 없다. 로그에도 적지 않는다 —
    /// 로그는 사람이 많이 보고 오래 남는다.
    /// </para>
    ///
    /// <para>
    /// [비밀번호 찾기 링크는 건드리지 않는다]
    /// </para>
    ///
    /// <para>
    /// 아직 안 쓴 링크가 남아 있어도 그대로 둔다. 그 링크를 쓰면 본인이 정한
    /// 값이 되고 — 그것도 「본인이 비밀번호를 바꾼 것」이라 문제가 없다.
    /// </para>
    /// </remarks>
    public async Task<IssuedPasswordDto?> ResetAccountPasswordAsync(string id)
    {
        // 화면은 계정 식별자로 부르지만 로그인 아이디로도 찾을 수 있게 둔다 —
        // 다른 서비스가 손에 든 것이 그쪽일 때가 있다(ChangePasswordAsync 와 같다).
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == id || a.UserId == id);

        if (account == null) return null;

        var issued = InitialPassword.Issue(_config);

        account.Password = PasswordHasher.Hash(issued);

        // 받은 사람이 첫 로그인에서 곧바로 바꾸게 한다. 관리자 손을 거쳐
        // 전달되는 값이라 애초에 만료된 것으로 두는 편이 사실에 가깝다.
        account.PasswordChangedAt = PasswordPolicy.AlreadyExpiredAt(
            PasswordPolicy.ExpiryDays(_config), DateTime.UtcNow);

        await _db.SaveChangesAsync();

        return new IssuedPasswordDto
        {
            LoginId = account.UserId,

            // 이름 칸은 비어 있을 수 있다(`Account.UserName` 이 nullable).
            // 창에 「누구의 것인지」로 뜨는 값이라 null 을 그대로 넘기지 않는다.
            UserName = account.UserName ?? string.Empty,
            Password = issued
        };
    }

    /// <summary>
    /// 로그인한 사용자의 프로필 정보를 업데이트합니다.
    /// 이메일·전화번호는 다른 계정이 이미 쓰고 있으면 거부한다 — Error 에 사람이 읽을 이유를 담는다.
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return (false, "계정을 찾을 수 없습니다.");

        // ── 이메일 목록 정리 및 중복 검사 ──
        List<string>? emailsToSave = null;
        if (dto.Emails != null)
        {
            emailsToSave = dto.Emails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!string.IsNullOrWhiteSpace(dto.Email) && !emailsToSave.Contains(dto.Email, StringComparer.OrdinalIgnoreCase))
            {
                emailsToSave.Insert(0, dto.Email);
            }
        }
        else if (dto.Email != null)
        {
            emailsToSave = string.IsNullOrWhiteSpace(dto.Email) ? new List<string>() : new List<string> { dto.Email };
        }

        var currentEmails = account.ProfileDetails?
            .Where(p => p.DetailType == "Email" && !string.IsNullOrWhiteSpace(p.Content))
            .Select(p => p.Content!.Trim().ToLowerInvariant())
            .ToList() ?? new List<string>();

        if (emailsToSave != null)
        {
            foreach (var email in emailsToSave)
            {
                var lower = email.Trim().ToLowerInvariant();
                if (!currentEmails.Contains(lower))
                {
                    var taken = await _db.AccountProfileDetails.AnyAsync(p =>
                        p.DetailType == "Email" &&
                        p.AccountId != account.Id &&
                        p.Content != null &&
                        p.Content.ToLower() == lower);
                    if (taken) return (false, $"이미 다른 사용자가 쓰고 있는 이메일입니다: {email}");

                    // 이메일 변경 시 인증 확인: 기존에 없던 새 이메일은 이메일 인증이 완료되어야 함
                    if (_emailVerification != null && !_emailVerification.IsVerified(account.Id, email, dto.EmailVerificationToken))
                    {
                        return (false, $"이메일 변경은 이메일 인증이 완료되어야 처리할 수 있습니다: {email}");
                    }
                }
            }
        }

        // ── 전화번호 목록 정리 및 중복 검사 ──
        static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

        List<string>? phonesToSave = null;
        if (dto.Phones != null)
        {
            phonesToSave = dto.Phones.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            if (!string.IsNullOrWhiteSpace(dto.Phone) && !phonesToSave.Contains(dto.Phone))
            {
                phonesToSave.Insert(0, dto.Phone);
            }
        }
        else if (dto.Phone != null)
        {
            phonesToSave = string.IsNullOrWhiteSpace(dto.Phone) ? new List<string>() : new List<string> { dto.Phone };
        }

        var currentPhoneDigits = account.ProfileDetails?
            .Where(p => p.DetailType == "Phone" && !string.IsNullOrWhiteSpace(p.Content))
            .Select(p => Digits(p.Content))
            .ToList() ?? new List<string>();

        if (phonesToSave != null)
        {
            foreach (var phone in phonesToSave)
            {
                var pDigits = Digits(phone);
                if (pDigits.Length > 0 && !currentPhoneDigits.Contains(pDigits))
                {
                    var otherPhones = await _db.AccountProfileDetails
                        .Where(p => p.DetailType == "Phone" && p.AccountId != account.Id && p.Content != null)
                        .Select(p => p.Content!)
                        .ToListAsync();
                    if (otherPhones.Any(c => Digits(c) == pDigits))
                    {
                        return (false, $"이미 다른 사용자가 쓰고 있는 전화번호입니다: {phone}");
                    }
                }
            }
        }

        // UserName(RealName) 업데이트
        if (!string.IsNullOrEmpty(dto.RealName))
        {
            account.UserName = dto.RealName;
            account.RealName = dto.RealName;
        }

        // Introduction 업데이트
        var introDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Introduction");
        if (introDetail != null)
        {
            introDetail.Content = dto.Introduction ?? string.Empty;
            _db.Entry(introDetail).State = EntityState.Modified;
        }
        else
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Introduction",
                Content = dto.Introduction ?? string.Empty,
                IsPrimary = true
            });
        }

        // Email 업데이트
        if (emailsToSave != null)
        {
            var existingEmailDetails = account.ProfileDetails?.Where(p => p.DetailType == "Email").ToList() ?? new List<AccountProfileDetail>();
            foreach (var e in existingEmailDetails) _db.AccountProfileDetails.Remove(e);

            for (var i = 0; i < emailsToSave.Count; i++)
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Email",
                    Content = emailsToSave[i],
                    IsPrimary = i == 0
                });
            }

            if (_emailVerification != null)
            {
                foreach (var em in emailsToSave)
                {
                    _emailVerification.ClearVerification(account.Id, em);
                }
            }
        }

        // Phone 업데이트
        if (phonesToSave != null)
        {
            var existingPhoneDetails = account.ProfileDetails?.Where(p => p.DetailType == "Phone").ToList() ?? new List<AccountProfileDetail>();
            foreach (var p in existingPhoneDetails) _db.AccountProfileDetails.Remove(p);

            for (var i = 0; i < phonesToSave.Count; i++)
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Phone",
                    Content = phonesToSave[i],
                    IsPrimary = i == 0
                });
            }
        }

        // Avatar 업데이트
        if (dto.Avatar != null)
        {
            var avatarDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar");
            if (avatarDetail != null)
            {
                avatarDetail.Content = dto.Avatar;
                _db.Entry(avatarDetail).State = EntityState.Modified;
            }
            else
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Avatar",
                    Content = dto.Avatar,
                    IsPrimary = true
                });
            }
        }

        // AvatarGroupId 업데이트
        if (dto.AvatarGroupId != null)
        {
            account.AvatarGroupId = dto.AvatarGroupId;
            _db.Entry(account).State = EntityState.Modified;
        }

        // 생년월일 업데이트 — null 은 '건드리지 않음', 빈 문자열은 '지움'.
        if (dto.BirthDate != null)
        {
            if (string.IsNullOrWhiteSpace(dto.BirthDate))
            {
                account.BirthDate = null;
            }
            else if (DateOnly.TryParse(dto.BirthDate, out var birth))
            {
                account.BirthDate = birth;
            }
            else
            {
                return (false, "생년월일 형식이 올바르지 않습니다. (yyyy-MM-dd)");
            }
            _db.Entry(account).State = EntityState.Modified;
        }
        if (dto.BirthDateIsLunar != null)
        {
            account.BirthDateIsLunar = dto.BirthDateIsLunar.Value;
            _db.Entry(account).State = EntityState.Modified;
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// 로그인한 사용자의 비밀번호를 변경합니다.
    /// </summary>
    public async Task<ChangePasswordResult> ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return ChangePasswordResult.AccountNotFound;

        // 이전 비밀번호 검증 (저장값이 평문인 계정도 그대로 통과한다 — PasswordHasher 참고)
        if (!PasswordHasher.Verify(account.Password, dto.OldPassword))
        {
            return ChangePasswordResult.OldPasswordMismatch;
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return ChangePasswordResult.NewPasswordEmpty;
        }

        // 지금 쓰는 것과 같은 값이면 막는다.
        // 90일마다 바꾸라고 요구하면서 같은 값을 허용하면 정책이 아무 일도 하지 않는다.
        if (PasswordHasher.Verify(account.Password, dto.NewPassword))
        {
            return ChangePasswordResult.SameAsCurrent;
        }

        // 새 비밀번호는 항상 해시로 저장한다.
        account.Password = PasswordHasher.Hash(dto.NewPassword);
        // 만료 시계를 여기서 다시 맞춘다. 이 값이 90일 정책의 기준이다.
        account.PasswordChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ChangePasswordResult.Success;
    }

    /// <summary>
    /// 비밀번호가 맞는지 확인만 한다. 잠금화면이 쓴다(D7).
    /// </summary>
    /// <remarks>
    /// <b>일부러 아무것도 바꾸지 않는다.</b> 로그인에는 있는 곁일(해시 승격 ·
    /// 접속 기록 · 만료 시계)을 여기서 하면, 잠금을 푼 것이 로그인 기록으로
    /// 남아 「접속 기록」 화면이 실제와 달라진다.
    /// </remarks>
    public async Task<bool> VerifyPasswordAsync(string userId, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        // 저장값이 아직 평문인 계정도 그대로 통과한다 — PasswordHasher 참고.
        return account is not null && PasswordHasher.Verify(account.Password, password);
    }

    /// <summary>
    /// 로그인한 사용자의 설정을 업데이트합니다.
    /// </summary>
    public async Task<bool> UpdateSettingAsync(string userId, UpdateSettingDto dto)
    {
        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return false;

        var settingDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == dto.FieldName);
        var valStr = dto.Value ? "true" : "false";

        if (settingDetail != null)
        {
            settingDetail.Content = valStr;
            _db.Entry(settingDetail).State = EntityState.Modified;
        }
        else
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = dto.FieldName,
                Content = valStr,
                IsPrimary = true
            });
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 로그인한 뒤 처음 열리는 화면을 정한다. 계약은 <see cref="IUserService"/> 에 있다.
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateHomePathAsync(string userId, string? homePath)
    {
        var path = (homePath ?? string.Empty).Trim();

        if (path.Length > 0 && !IsSiteRelativePath(path))
        {
            return (false, "홈 화면 경로가 올바르지 않습니다.");
        }

        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return (false, "계정을 찾을 수 없습니다.");

        var detail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "HomePath");

        // 비웠으면 **줄을 지운다.** 「지정 안 함」을 글자로 적어 두면
        // (`/workspace` 같은 것) 나중에 기본이 바뀌어도 그 사람만 옛 화면에
        // 묶인다 — 띠의 「기본값으로」가 열쇠를 지우는 것과 같은 규칙이다.
        if (path.Length == 0)
        {
            if (detail is not null)
            {
                _db.AccountProfileDetails.Remove(detail);
                await _db.SaveChangesAsync();
            }

            return (true, null);
        }

        if (detail is not null)
        {
            detail.Content = path;
            _db.Entry(detail).State = EntityState.Modified;
        }
        else
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "HomePath",
                Content = path,
                IsPrimary = true
            });
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// 이 사이트 안을 가리키는 절대 경로인가.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 로그인 화면의 <c>SafeReturnUrl</c> 과 <b>같은 규칙</b>이다. 다를 이유가
    /// 없다 — 둘 다 「로그인 직후 사용자를 보낼 주소」이고, 그 시점은 사람이
    /// 주소창을 가장 안 보는 때다. <c>//evil.com</c> 은 프로토콜 상대 URL 이라
    /// <c>/</c> 로 시작해도 밖으로 나간다.
    /// </para>
    /// <para>
    /// <b>「그런 화면이 있는가」는 보지 않는다.</b> 라우트는 프론트의
    /// <c>@page</c> 가 갖고 있어서 서버가 알 길이 없다(web/CLAUDE.md 의
    /// 「라우팅 소유권이 뒤집혔다」). 없는 화면을 고르는 길은 고르개 쪽에서
    /// 막는다 — 권한이 있는 메뉴만 목록에 올린다.
    /// </para>
    /// </remarks>
    private static bool IsSiteRelativePath(string path) =>
        path.Length <= 200
        && path[0] == '/'
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Any(char.IsWhiteSpace)
        && !path.Any(char.IsControl);

    /// <inheritdoc cref="IUserService.MergeAccountAsync"/>
    public async Task<(bool Ok, string? Error)> MergeAccountAsync(
        string sourceAccountId, string targetAccountId, string approver, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceAccountId))
        {
            return (false, "결합할 원본 계정을 지정해 주십시오.");
        }

        if (string.IsNullOrWhiteSpace(targetAccountId))
        {
            return (false, "결합할 대상 계정을 입력해 주십시오.");
        }

        var sourceAccount = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == sourceAccountId || a.UserId == sourceAccountId, ct);

        if (sourceAccount is null)
        {
            return (false, "결합할 원본 계정을 찾을 수 없습니다.");
        }

        var targetAccount = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == targetAccountId || a.UserId == targetAccountId, ct);

        if (targetAccount is null)
        {
            return (false, $"결합할 대상 계정 '{targetAccountId}'을(를) 찾을 수 없습니다.");
        }

        if (sourceAccount.Id == targetAccount.Id)
        {
            return (false, "원본 계정과 대상 계정이 동일하여 결합할 수 없습니다.");
        }

        // 1. 소셜 로그인 연결 이전/병합
        var sourceSocials = await _db.AccountSocialLogins
            .Where(l => l.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        var targetSocials = await _db.AccountSocialLogins
            .Where(l => l.AccountId == targetAccount.Id)
            .ToListAsync(ct);

        foreach (var login in sourceSocials)
        {
            var existingSameProvider = targetSocials
                .FirstOrDefault(t => t.Provider.Equals(login.Provider, StringComparison.OrdinalIgnoreCase));

            if (existingSameProvider is not null)
            {
                existingSameProvider.ProviderUserId = login.ProviderUserId;
                existingSameProvider.Email = login.Email ?? existingSameProvider.Email;
                existingSameProvider.DisplayName = login.DisplayName ?? existingSameProvider.DisplayName;
                existingSameProvider.LastLoginAt = login.LastLoginAt ?? existingSameProvider.LastLoginAt;
                existingSameProvider.UpdatedAt = DateTime.UtcNow;
                existingSameProvider.UpdatedBy = approver;

                _db.AccountSocialLogins.Remove(login);
            }
            else
            {
                login.AccountId = targetAccount.Id;
                login.Account = targetAccount;
                login.UpdatedAt = DateTime.UtcNow;
                login.UpdatedBy = approver;
            }
        }

        // 2. WebAuthn 패스키 자격증명 이전
        var webAuthnCreds = await _db.AccountWebAuthnCredentials
            .Where(c => c.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        foreach (var cred in webAuthnCreds)
        {
            cred.AccountId = targetAccount.Id;
            cred.Account = targetAccount;
        }

        // 3. 즐겨찾기(MenuFavorites) 병합
        var sourceFavs = await _db.MenuFavorites
            .Where(f => f.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        var targetFavMenuIds = await _db.MenuFavorites
            .Where(f => f.AccountId == targetAccount.Id)
            .Select(f => f.MenuId)
            .ToListAsync(ct);

        foreach (var fav in sourceFavs)
        {
            if (!targetFavMenuIds.Contains(fav.MenuId))
            {
                fav.AccountId = targetAccount.Id;
                fav.Account = targetAccount;
            }
            else
            {
                _db.MenuFavorites.Remove(fav);
            }
        }

        // 4. 역할(RoleAccounts) 병합
        var sourceRoles = await _db.RoleAccounts
            .Where(ra => ra.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        var targetRoleIds = await _db.RoleAccounts
            .Where(ra => ra.AccountId == targetAccount.Id)
            .Select(ra => ra.RoleId)
            .ToListAsync(ct);

        foreach (var ra in sourceRoles)
        {
            if (!targetRoleIds.Contains(ra.RoleId))
            {
                ra.AccountId = targetAccount.Id;
            }
            else
            {
                _db.RoleAccounts.Remove(ra);
            }
        }

        // 5. 사용자 환경설정(AccountPreferences) 병합
        var sourcePref = await _db.AccountPreferences
            .FirstOrDefaultAsync(p => p.AccountId == sourceAccount.Id, ct);

        if (sourcePref is not null)
        {
            var targetHasPref = await _db.AccountPreferences
                .AnyAsync(p => p.AccountId == targetAccount.Id, ct);

            if (!targetHasPref)
            {
                sourcePref.AccountId = targetAccount.Id;
                sourcePref.Account = targetAccount;
            }
            else
            {
                _db.AccountPreferences.Remove(sourcePref);
            }
        }

        // 6. 로그인 로그(AccountLoginLogs) 이전
        var sourceLogs = await _db.AccountLoginLogs
            .Where(l => l.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        foreach (var log in sourceLogs)
        {
            log.AccountId = targetAccount.Id;
        }

        // 7. 프로필 상세 정보(AccountProfileDetails) 병합
        var sourceEmail = sourceAccount.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email")?.Content;
        var sourcePhone = sourceAccount.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone")?.Content;

        var existingEmails = targetAccount.ProfileDetails?
            .Where(p => p.DetailType == "Email")
            .Select(p => p.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList() ?? [];

        // 원본 계정의 이메일들 이전
        var sourceEmails = sourceAccount.ProfileDetails?
            .Where(p => p.DetailType == "Email")
            .Select(p => p.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList() ?? [];

        foreach (var em in sourceEmails)
        {
            if (!existingEmails.Contains(em, StringComparer.OrdinalIgnoreCase))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = targetAccount.Id,
                    Account = targetAccount,
                    DetailType = "Email",
                    Content = em,
                    IsPrimary = existingEmails.Count == 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = approver,
                });
                existingEmails.Add(em);
            }
        }

        // 소셜 로그인 이메일 병합
        foreach (var login in sourceSocials)
        {
            if (!string.IsNullOrWhiteSpace(login.Email) && !existingEmails.Contains(login.Email, StringComparer.OrdinalIgnoreCase))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = targetAccount.Id,
                    Account = targetAccount,
                    DetailType = "Email",
                    Content = login.Email,
                    IsPrimary = existingEmails.Count == 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = approver,
                });
                existingEmails.Add(login.Email);
            }
        }

        // 전화번호 병합
        var existingPhones = targetAccount.ProfileDetails?
            .Where(p => p.DetailType == "Phone")
            .Select(p => p.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList() ?? [];

        var sourcePhones = sourceAccount.ProfileDetails?
            .Where(p => p.DetailType == "Phone")
            .Select(p => p.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList() ?? [];

        foreach (var ph in sourcePhones)
        {
            if (!existingPhones.Contains(ph, StringComparer.OrdinalIgnoreCase))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = targetAccount.Id,
                    Account = targetAccount,
                    DetailType = "Phone",
                    Content = ph,
                    IsPrimary = existingPhones.Count == 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = approver,
                });
                existingPhones.Add(ph);
            }
        }

        // 아바타 승계
        if (string.IsNullOrWhiteSpace(targetAccount.AvatarGroupId) && !string.IsNullOrWhiteSpace(sourceAccount.AvatarGroupId))
        {
            targetAccount.AvatarGroupId = sourceAccount.AvatarGroupId;
            var sourceAvatar = sourceAccount.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar")?.Content;
            if (!string.IsNullOrWhiteSpace(sourceAvatar) && targetAccount.ProfileDetails?.All(p => p.DetailType != "Avatar") == true)
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = targetAccount.Id,
                    Account = targetAccount,
                    DetailType = "Avatar",
                    Content = sourceAvatar,
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = approver,
                });
            }
        }

        // 개발 업무용 확장 속성(Dev.) 병합: 대상 계정에 없는 속성 보완
        var sourceDevDetails = sourceAccount.ProfileDetails?
            .Where(p => p.DetailType.StartsWith("Dev.", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        foreach (var dev in sourceDevDetails)
        {
            var targetHasDev = targetAccount.ProfileDetails?.Any(p =>
                string.Equals(p.DetailType, dev.DetailType, StringComparison.OrdinalIgnoreCase)) == true;

            if (!targetHasDev && !string.IsNullOrWhiteSpace(dev.Content))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = targetAccount.Id,
                    Account = targetAccount,
                    DetailType = dev.DetailType,
                    Content = dev.Content,
                    Label = dev.Label,
                    Remark = dev.Remark,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = approver,
                });
            }
        }

        // 기존 결합 이력 이전
        var existingSourceMerges = sourceAccount.ProfileDetails?
            .Where(p => p.DetailType == "SocialMerge" || p.DetailType == "MergedSocial")
            .ToList() ?? [];

        foreach (var sm in existingSourceMerges)
        {
            sm.AccountId = targetAccount.Id;
            sm.Account = targetAccount;
        }

        // 이번 결합 이력(SocialMerge) 기록
        var mergeInfo = $"{sourceEmail}|{sourcePhone}|{approver}|{DateTime.UtcNow:O}";
        if (sourceSocials.Count > 0)
        {
            foreach (var login in sourceSocials)
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = targetAccount.Id,
                    Account = targetAccount,
                    DetailType = "SocialMerge",
                    Content = login.Provider,
                    Label = sourceAccount.UserId,
                    Remark = mergeInfo,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = approver,
                });
            }
        }
        else
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = targetAccount.Id,
                Account = targetAccount,
                DetailType = "SocialMerge",
                Content = "account",
                Label = sourceAccount.UserId,
                Remark = mergeInfo,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = approver,
            });
        }

        // 1단계 저장: 대상 계정 변경사항 및 이전된 레코드 저장
        await _db.SaveChangesAsync(ct);

        // 2단계 저장: 원본 계정 프로필 상세 및 원본 계정 엔티티 삭제
        if (sourceAccount.ProfileDetails is { Count: > 0 })
        {
            _db.AccountProfileDetails.RemoveRange(sourceAccount.ProfileDetails);
        }

        _db.Accounts.Remove(sourceAccount);
        await _db.SaveChangesAsync(ct);

        _logger?.LogInformation(
            "계정 결합 완료: {SourceLoginId}({SourceId}) -> {TargetLoginId}({TargetId}) (처리자 {Approver})",
            sourceAccount.UserId, sourceAccount.Id, targetAccount.UserId, targetAccount.Id, approver);

        // 알림 발송: 앱 푸시 알림 및 이메일 알림
        if (_notify is not null)
        {
            try
            {
                await _notify.NotifyOwnersAsync(
                    [targetAccount.UserId, sourceAccount.UserId],
                    "[JSini 포털] 계정 결합 완료",
                    $"{System.Net.WebUtility.HtmlEncode(targetAccount.UserName ?? targetAccount.UserId)} 님의 계정에 '{sourceAccount.UserId}' 계정이 결합되었습니다.",
                    "/admin/system/account",
                    targetAccount.UserId,
                    "AUTH_ACCOUNT",
                    ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "계정 결합 앱 푸시 알림 발송 실패");
            }
        }

        if (_mail is not null)
        {
            var mailTargets = existingEmails.Concat(sourceEmails)
                .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var mailAddr in mailTargets)
            {
                try
                {
                    await _mail.SendAsync(
                        mailAddr,
                        "[JSini 포털] 계정이 결합되었습니다",
                        $@"<h2>계정 결합 안내</h2>
<p>{System.Net.WebUtility.HtmlEncode(targetAccount.UserName ?? targetAccount.UserId)} 님의 계정(<b>{System.Net.WebUtility.HtmlEncode(targetAccount.UserId)}</b>)에 기존 계정(<b>{System.Net.WebUtility.HtmlEncode(sourceAccount.UserId)}</b>)이 정상적으로 결합되었습니다.</p>
<p>소셜 연동, 연락처 정보 등이 대상 계정으로 통합되었습니다.</p>",
                        "AUTH_ACCOUNT",
                        NotificationEvents.AccountMail,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "계정 결합 안내 메일 발송 실패: {Email}", mailAddr);
                }
            }
        }

        return (true, null);
    }
}
