using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>사용자 정보 관련 비즈니스 로직 처리 서비스</summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IRoleAssignmentService _roleAssignments;
    private readonly IConfiguration _config;
    private readonly IEmailVerificationService? _emailVerification;
    private readonly AccountMailClient? _mail;
    private readonly SignupNotifyClient? _notify;
    private readonly ILogger<UserService>? _logger;

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

    /// <summary>개발 업무용 확장 속성 접두사(<c>Dev.</c>). 같은 표의 <c>MsaCompany</c>·<c>HomePath</c> 같은 칸을 접두사로 가둬 화면 저장이 덮어쓰지 못하게 한다.</summary>
    private const string DevPrefix = "Dev.";

    /// <summary>그 계정의 <c>Dev.*</c> 속성. 접두사를 뗀 이름으로 돌려준다.</summary>
    private static Dictionary<string, string?> DevAttributesOf(Entities.Account account) =>
        account.ProfileDetails?
            .Where(p => !p.IsDeleted && p.DetailType.StartsWith(DevPrefix, StringComparison.Ordinal))
            .ToDictionary(p => p.DetailType[DevPrefix.Length..], p => (string?)p.Content)
        ?? [];

    /// <summary>그 계정의 <c>Dev.*</c> 속성을 통째로 맞춘다. 보낸 사전이 null 이면 건드리지 않고, 빠진 열쇠는 지운다.</summary>
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
            // 빈 값은 줄을 남기지 않는다 — 빈 글자를 담으면 「안 적음」과 「비움」이 구분되지 않는다.
            if (!wanted.TryGetValue(detail.DetailType, out var content)
                || string.IsNullOrWhiteSpace(content))
            {
                _db.AccountProfileDetails.Remove(detail);
                continue;
            }

            // 지운 표시가 붙은 것은 되살린다 — 읽는 쪽(DevAttributesOf)이 그 표시를 보므로 안 풀면 저장돼도 안 보인다.
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

    /// <summary>워터마크 설정을 쓴다. 켜는 쪽도 값을 적어 「관리자가 정했다」는 사실을 남긴다.</summary>
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

    /// <summary>그 계정의 워터마크 설정. 「off」일 때만 끈다 — 해석이 안 되는 값은 켜 둔다.</summary>
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

    /// <summary>사용자 정보를 조회하고 DTO로 변환하여 반환</summary>
    /// <param name="userIdOrKey">사용자 아이디 또는 고유 키</param>
    public async Task<UserInfoDto?> GetUserInfoAsync(string userIdOrKey)
    {
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

        // 역할은 실제 배정값이다. 「super」 하나로 흉내 내면 권한 판단에 쓰일 때 전부 관리자로 보인다.
        // 역할은 회사·부서·사람 세 단계를 모두 합친다 — 로그인 토큰과 같은 규칙이어야 화면과 실제 권한이 어긋나지 않는다.
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

        var accountSocials = await _db.AccountSocialLogins
            .Where(l => l.AccountId == account.Id && !l.IsDeleted)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        var providers = accountSocials
            .Select(s => s.Provider)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var s in accountSocials)
        {
            if (!string.IsNullOrWhiteSpace(s.Email) && !emailDetails.Contains(s.Email, StringComparer.OrdinalIgnoreCase))
            {
                emailDetails.Add(s.Email);
            }
        }

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

        return new UserInfoDto
        {
            Id = account.Id,
            UserId = account.UserId,
            Username = account.UserId,
            RealName = account.UserName,
            CompanyId = account.CompanyId ?? account.Company?.Id,
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

    /// <summary>전체 계정 목록을 조회한다.</summary>
    public async Task<List<AccountDto>> GetAccountsAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .ToListAsync();

        var roleAccounts = await _db.RoleAccounts
            .Include(ra => ra.Role)
            .ToListAsync();

        var socialLogins = await _db.AccountSocialLogins
            .Where(l => !l.IsDeleted)
            .ToListAsync();

        var socialMap = socialLogins
            .GroupBy(l => l.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList()
            );

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

                SocialProviders = providers,
                IsSocialLinked = isLinked,
                IsSocialMerged = isMerged,
                SocialProviderText = providerText,

                // 값이 없으면 켜진 것으로 본다 — 설정을 안 건드린 계정이 조용히 꺼지면 안 된다.
                Watermark = WatermarkOf(a),
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,

                // 들어온 때와 마지막 사용 때는 계정 표의 칸이라 조인이 없다 — 기록 표를 줄마다 집계하지 않는다.
                LastLoginAt = a.LastLoginAt,
                LastLoginIp = a.LastLoginIp,
                LastSeenAt = a.LastSeenAt,

                RoleIds = rolesInfo?.RoleIds ?? new List<string>(),
                RoleNames = rolesInfo?.RoleNames ?? new List<string>(),

                // ProfileDetails 는 위에서 Include 로 읽으므로 추가 조회가 없다.
                Avatar = avatarDetail?.Content,
                AvatarGroupId = a.AvatarGroupId,

                // 생일 — 정본이 계정이라 계정 관리 화면이 여기서 읽고 고친다.
                BirthDate = a.BirthDate,
                BirthDateIsLunar = a.BirthDateIsLunar,
                BirthdayCelebrated = a.BirthdayCelebrated,

                // 개발 업무용 확장 속성 — ProfileDetails 를 이미 Include 로 읽으므로 추가 조회가 없다.
                DevAttributes = DevAttributesOf(a),
            };
        }).ToList();
    }

    /// <summary>신규 계정을 만든다. 비밀번호는 InitialPassword 가 정한 값을 해시로 저장하고, 평문은 이 응답에만 실어 보내며, 만료 시계를 지나게 해 첫 로그인에서 바꾸게 한다.</summary>
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

        // 발급한 평문은 응답에 한 번 실어 보낸 뒤 버린다 — 로그에도 남기지 않는다.
        var initialPassword = InitialPassword.Issue(_config);

        var account = new Account
        {
            UserId = dto.LoginId,
            UserName = dto.UserName,
            RealName = dto.UserName,
            Password = PasswordHasher.Hash(initialPassword),

            // 첫 로그인 변경은 90일 정책이 맡는다 — 「첫 로그인」 플래그는 두지 않는다(PasswordPolicy 참고).
            PasswordChangedAt = PasswordPolicy.AlreadyExpiredAt(
                PasswordPolicy.ExpiryDays(_config), DateTime.UtcNow),

            DepartmentId = validDeptId,
            CompanyId = companyId,
            BirthDate = dto.BirthDate,
            BirthDateIsLunar = dto.BirthDateIsLunar,
            BirthdayCelebrated = dto.BirthdayCelebrated
        };

        _db.Accounts.Add(account);

        // 등록할 때 끄고 만들 수 있다. 값이 없는 것이 곧 켜진 것이다.
        if (dto.Watermark is false)
        {
            UpsertWatermark(account, false);
        }

        // 확장 속성은 함께 적을 수 있다 — null 이면 줄을 만들지 않는다.
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

            // 방금 넣은 것을 돌려준다 — 빈 값은 SyncDevAttributes 가 걸러 DB 와 다를 수 있다. 등록 응답은 발급 비밀번호를 보여 주므로 목록을 다시 읽는다.
            DevAttributes = dto.DevAttributes ?? [],

            // 발급한 평문을 돌려주는 유일한 자리. 다음 조회부터는 null 이다.
            InitialPassword = initialPassword
        };
    }

    /// <summary>기존 계정 정보를 수정한다.</summary>
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

        // 워터마크 null 은 건드리지 않음 — 이 값을 모르는 호출이 설정을 지우지 않게 한다.
        if (dto.Watermark is { } watermark)
        {
            UpsertWatermark(account, watermark);
        }

        // 개발 업무용 확장 속성: null 이면 건드리지 않는다.
        SyncDevAttributes(account, dto.DevAttributes);

        // 부서가 있으면 그 부서의 회사를 소속으로 삼는다. 회사만 고른 계정은 회사 소속을 유지한다.
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

    /// <summary>계정을 삭제한다.</summary>
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

    /// <summary>관리자가 그 계정의 비밀번호를 초기화한다. <see cref="CreateAccountAsync"/> 와 같은 방식으로 발급·해시 저장·만료 처리하고, 발급한 평문은 응답에서만 보낸다. 아직 안 쓴 비밀번호 찾기 링크는 건드리지 않는다.</summary>
    public async Task<IssuedPasswordDto?> ResetAccountPasswordAsync(string id)
    {
        // 계정 식별자 외에 로그인 아이디로도 찾는다 — 다른 서비스가 손에 든 것이 그쪽일 때가 있다.
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == id || a.UserId == id);

        if (account == null) return null;

        var issued = InitialPassword.Issue(_config);

        account.Password = PasswordHasher.Hash(issued);

        // 관리자를 거쳐 전달되는 값이라 처음부터 만료된 것으로 둔다 — 받은 사람이 첫 로그인에서 바꾼다.
        account.PasswordChangedAt = PasswordPolicy.AlreadyExpiredAt(
            PasswordPolicy.ExpiryDays(_config), DateTime.UtcNow);

        await _db.SaveChangesAsync();

        return new IssuedPasswordDto
        {
            LoginId = account.UserId,

            // Account.UserName 은 nullable 이라 null 을 그대로 넘기지 않는다.
            UserName = account.UserName ?? string.Empty,
            Password = issued
        };
    }

    /// <summary>로그인한 사용자의 프로필을 업데이트한다. 이메일·전화번호가 다른 계정에서 이미 쓰이면 거부하고, 이유를 Error 에 담는다.</summary>
    public async Task<(bool Success, string? Error)> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return (false, "계정을 찾을 수 없습니다.");

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

                    // 기존에 없던 새 이메일은 이메일 인증을 마쳐야 한다.
                    if (_emailVerification != null && !_emailVerification.IsVerified(account.Id, email, dto.EmailVerificationToken))
                    {
                        return (false, $"이메일 변경은 이메일 인증이 완료되어야 처리할 수 있습니다: {email}");
                    }
                }
            }
        }

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

        if (!string.IsNullOrEmpty(dto.RealName))
        {
            account.UserName = dto.RealName;
            account.RealName = dto.RealName;
        }

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

        if (dto.AvatarGroupId != null)
        {
            account.AvatarGroupId = dto.AvatarGroupId;
            _db.Entry(account).State = EntityState.Modified;
        }

        // 생년월일: null 은 건드리지 않음, 빈 문자열은 지움.
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

    /// <summary>로그인한 사용자의 비밀번호를 변경한다.</summary>
    public async Task<ChangePasswordResult> ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return ChangePasswordResult.AccountNotFound;

        // 이전 비밀번호 검증 — 평문으로 남은 계정도 통과한다(PasswordHasher 참고).
        if (!PasswordHasher.Verify(account.Password, dto.OldPassword))
        {
            return ChangePasswordResult.OldPasswordMismatch;
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return ChangePasswordResult.NewPasswordEmpty;
        }

        // 지금 값과 같으면 막는다 — 90일마다 바꾸게 하면서 같은 값을 허용하면 정책이 무의미해진다.
        if (PasswordHasher.Verify(account.Password, dto.NewPassword))
        {
            return ChangePasswordResult.SameAsCurrent;
        }

        account.Password = PasswordHasher.Hash(dto.NewPassword);
        // 만료 시계를 다시 맞춘다 — 90일 정책의 기준이다.
        account.PasswordChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ChangePasswordResult.Success;
    }

    /// <summary>비밀번호가 맞는지만 확인한다. 잠금화면이 쓴다. 해시 승격·접속 기록·만료 시계를 건드리지 않는다 — 잠금을 푼 것이 로그인 기록으로 남으면 접속 기록 화면이 실제와 달라진다.</summary>
    public async Task<bool> VerifyPasswordAsync(string userId, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        // 평문으로 남은 저장값도 통과한다 — PasswordHasher 참고.
        return account is not null && PasswordHasher.Verify(account.Password, password);
    }

    /// <summary>로그인한 사용자의 설정을 업데이트한다.</summary>
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

    /// <summary>로그인한 뒤 처음 열리는 화면을 정한다. 계약은 <see cref="IUserService"/> 에 있다.</summary>
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

        // 비우면 줄을 지운다 — 「지정 안 함」을 글자로 적어 두면 기본값이 바뀔 때 그 사람만 옛 화면에 묶인다.
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

    /// <summary>이 사이트 안을 가리키는 절대 경로인지 본다. 로그인 화면의 <c>SafeReturnUrl</c> 과 같은 규칙이다 — <c>//evil.com</c> 은 프로토콜 상대 URL 이라 밖으로 나간다. 화면이 있는지는 보지 않는다(라우트는 프론트 <c>@page</c> 가 갖는다).</summary>
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

        var webAuthnCreds = await _db.AccountWebAuthnCredentials
            .Where(c => c.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        foreach (var cred in webAuthnCreds)
        {
            cred.AccountId = targetAccount.Id;
            cred.Account = targetAccount;
        }

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

        var sourceLogs = await _db.AccountLoginLogs
            .Where(l => l.AccountId == sourceAccount.Id)
            .ToListAsync(ct);

        foreach (var log in sourceLogs)
        {
            log.AccountId = targetAccount.Id;
        }

        var sourceEmail = sourceAccount.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email")?.Content;
        var sourcePhone = sourceAccount.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone")?.Content;

        var existingEmails = targetAccount.ProfileDetails?
            .Where(p => p.DetailType == "Email")
            .Select(p => p.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList() ?? [];

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

        var existingSourceMerges = sourceAccount.ProfileDetails?
            .Where(p => p.DetailType == "SocialMerge" || p.DetailType == "MergedSocial")
            .ToList() ?? [];

        foreach (var sm in existingSourceMerges)
        {
            sm.AccountId = targetAccount.Id;
            sm.Account = targetAccount;
        }

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

        await _db.SaveChangesAsync(ct);

        if (sourceAccount.ProfileDetails is { Count: > 0 })
        {
            _db.AccountProfileDetails.RemoveRange(sourceAccount.ProfileDetails);
        }

        _db.Accounts.Remove(sourceAccount);
        await _db.SaveChangesAsync(ct);

        _logger?.LogInformation(
            "계정 결합 완료: {SourceLoginId}({SourceId}) -> {TargetLoginId}({TargetId}) (처리자 {Approver})",
            sourceAccount.UserId, sourceAccount.Id, targetAccount.UserId, targetAccount.Id, approver);

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
