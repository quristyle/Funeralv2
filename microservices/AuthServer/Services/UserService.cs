using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 사용자 정보 관련 비즈니스 로직 처리 서비스
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IRoleAssignmentService _roleAssignments;
    private readonly IConfiguration _config;

    /// <summary>
    /// UserService 생성자
    /// </summary>
    public UserService(AppDbContext db, IRoleAssignmentService roleAssignments, IConfiguration config)
    {
        _db = db;
        _roleAssignments = roleAssignments;
        _config = config;
    }

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
            Desc = email ?? "등록된 설명이 없습니다.",
            HomePath = homePath ?? "/workspace",
            Roles = roleIds,
            RoleNames = roleNames,
            MsaSource = msaSource,
            Introduction = introduction,
            Phone = phone,
            Email = email,
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
            var emailDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            var phoneDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
            var statusDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");
            var avatarDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar");

            roleMap.TryGetValue(a.Id, out var rolesInfo);

            return new AccountDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail?.Content,
                Phone = phoneDetail?.Content,
                Status = statusDetail?.Content ?? "ACTIVE",
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,
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
            };
        }).ToList();
    }

    /// <summary>
    /// 신규 계정을 생성합니다.
    /// </summary>
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

        var account = new Account
        {
            UserId = dto.LoginId,
            UserName = dto.UserName,
            RealName = dto.UserName,
            Password = "1234", // 기본 비밀번호 설정
            DepartmentId = validDeptId,
            CompanyId = companyId,
            BirthDate = dto.BirthDate,
            BirthDateIsLunar = dto.BirthDateIsLunar,
            BirthdayCelebrated = dto.BirthdayCelebrated
        };

        _db.Accounts.Add(account);

        if (!string.IsNullOrEmpty(dto.Email))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Email",
                Content = dto.Email,
                IsPrimary = true
            });
        }

        if (!string.IsNullOrEmpty(dto.Phone))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Phone",
                Content = dto.Phone,
                IsPrimary = true
            });
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
            Email = dto.Email,
            Phone = dto.Phone,
            Status = dto.Status ?? "ACTIVE",
            DeptId = account.DepartmentId,
            DeptName = deptName,
            CreatedAt = account.CreatedAt,
            RoleIds = dto.RoleIds ?? new List<string>(),
            RoleNames = roleNames,
            BirthDate = account.BirthDate,
            BirthDateIsLunar = account.BirthDateIsLunar,
            BirthdayCelebrated = account.BirthdayCelebrated
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

        // 부서 ID 검증 및 소속 회사 자동 할당
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
        else
        {
            account.DepartmentId = null;
            account.CompanyId = null;
        }

        // Email 업데이트
        var emailDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
        if (emailDetail != null)
        {
            if (string.IsNullOrEmpty(dto.Email))
            {
                _db.AccountProfileDetails.Remove(emailDetail);
            }
            else
            {
                emailDetail.Content = dto.Email;
                _db.Entry(emailDetail).State = EntityState.Modified;
            }
        }
        else if (!string.IsNullOrEmpty(dto.Email))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Email",
                Content = dto.Email,
                IsPrimary = true
            });
        }

        // Phone 업데이트
        var phoneDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
        if (phoneDetail != null)
        {
            if (string.IsNullOrEmpty(dto.Phone))
            {
                _db.AccountProfileDetails.Remove(phoneDetail);
            }
            else
            {
                phoneDetail.Content = dto.Phone;
                _db.Entry(phoneDetail).State = EntityState.Modified;
            }
        }
        else if (!string.IsNullOrEmpty(dto.Phone))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Phone",
                Content = dto.Phone,
                IsPrimary = true
            });
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
    /// 로그인한 사용자의 프로필 정보를 업데이트합니다.
    /// 이메일·전화번호는 다른 계정이 이미 쓰고 있으면 거부한다 — Error 에 사람이 읽을 이유를 담는다.
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return (false, "계정을 찾을 수 없습니다.");

        // ── 중복 검사: 이메일 ──
        // 연락처는 AccountProfileDetails(DetailType=Email/Phone) 에 있다.
        // 대소문자만 다른 이메일은 같은 주소다.
        //
        // **지금 값 그대로면 검사하지 않는다.** 옛 데이터에는 이미 겹치는 연락처가
        // 있다(포털 quristyle 과 헬프데스크 고객이 같은 이메일 — 15번 문서).
        // 그런 계정이 이름만 고쳐 저장해도 막혀 버리면 안 된다.
        // 검사는 '다른 값으로 바꾸려는 순간'에만 건다.
        var currentEmail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email")?.Content;
        if (!string.IsNullOrWhiteSpace(dto.Email)
            && !string.Equals(dto.Email.Trim(), currentEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var taken = await _db.AccountProfileDetails.AnyAsync(p =>
                p.DetailType == "Email" &&
                p.AccountId != account.Id &&
                p.Content != null &&
                p.Content.ToLower() == email);
            if (taken) return (false, "이미 다른 사용자가 쓰고 있는 이메일입니다.");
        }

        // ── 중복 검사: 전화번호 ──
        // 표기가 제각각이라('010-1234-5678' / '01012345678') 숫자만 남겨 비교한다.
        // SQL 로 옮기기 어려운 비교라 전화 항목만 당겨 와 메모리에서 본다 — 행 수가 적다.
        static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
        var currentPhone = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone")?.Content;
        if (!string.IsNullOrWhiteSpace(dto.Phone)
            && Digits(dto.Phone) != Digits(currentPhone))
        {
            var phoneDigits = Digits(dto.Phone);
            if (phoneDigits.Length > 0)
            {
                var otherPhones = await _db.AccountProfileDetails
                    .Where(p => p.DetailType == "Phone" && p.AccountId != account.Id && p.Content != null)
                    .Select(p => p.Content!)
                    .ToListAsync();
                var taken = otherPhones.Any(c => Digits(c) == phoneDigits);
                if (taken) return (false, "이미 다른 사용자가 쓰고 있는 전화번호입니다.");
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
        if (dto.Email != null)
        {
            var emailDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            if (emailDetail != null)
            {
                emailDetail.Content = dto.Email;
                _db.Entry(emailDetail).State = EntityState.Modified;
            }
            else
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Email",
                    Content = dto.Email,
                    IsPrimary = true
                });
            }
        }

        // Phone 업데이트
        if (dto.Phone != null)
        {
            var phoneDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
            if (phoneDetail != null)
            {
                phoneDetail.Content = dto.Phone;
                _db.Entry(phoneDetail).State = EntityState.Modified;
            }
            else
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Phone",
                    Content = dto.Phone,
                    IsPrimary = true
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
}
