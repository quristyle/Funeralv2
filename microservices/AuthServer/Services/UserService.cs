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

    /// <summary>
    /// UserService 생성자
    /// </summary>
    public UserService(AppDbContext db)
    {
        _db = db;
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

        // 역할은 실제 배정값을 내려준다.
        // 예전에는 무조건 "super" 한 개를 만들어 보냈다. 화면 접근 제어가 백엔드 메뉴 기준이라
        // 당장 티가 나지 않았을 뿐, 이 값을 보고 판단하는 코드가 생기면 전부 관리자로 보인다.
        var roles = await _db.RoleAccounts
            .Where(ra => ra.AccountId == account.Id)
            .Join(_db.Roles.Where(r => r.Status == 1), ra => ra.RoleId, r => r.Id,
                  (ra, r) => new { r.Id, r.Name })
            .Distinct()
            .ToListAsync();

        var roleIds = roles.Select(r => r.Id).ToList();
        // 표시용 이름. 이름이 비어 있으면 식별자로 대신한다.
        var roleNames = roles.Select(r => string.IsNullOrWhiteSpace(r.Name) ? r.Id : r.Name).ToList();

        var securityPhone = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityPhone")?.Content == "true";
        var securityQuestion = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityQuestion")?.Content == "true";
        var securityEmail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityEmail")?.Content == "true";
        var securityMfa = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "SecurityMfa")?.Content == "true";

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
            Introduction = introduction,
            Phone = phone,
            Email = email,
            SecurityPhone = securityPhone,
            SecurityQuestion = securityQuestion,
            SecurityEmail = securityEmail,
            SecurityMfa = securityMfa,
            SystemMessage = systemMessage,
            TodoTask = todoTask,
            AccountPasswordNotify = accountPasswordNotify
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
                RoleNames = rolesInfo?.RoleNames ?? new List<string>()
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
            CompanyId = companyId
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
            RoleNames = roleNames
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
    /// </summary>
    public async Task<bool> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        System.Console.WriteLine($"[UpdateProfile Debug] UserId: {userId}, Avatar: {dto.Avatar}, AvatarGroupId: {dto.AvatarGroupId}");

        var account = await _db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return false;

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

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 로그인한 사용자의 비밀번호를 변경합니다.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId || a.Id == userId);

        if (account == null) return false;

        // 이전 비밀번호 검증 (현 보안 구조상 평문 비교)
        if (!PasswordHasher.Verify(account.Password, dto.OldPassword))
        {
            return false;
        }

        // 새 비밀번호는 항상 해시로 저장한다.
        account.Password = PasswordHasher.Hash(dto.NewPassword);
        await _db.SaveChangesAsync();
        return true;
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
