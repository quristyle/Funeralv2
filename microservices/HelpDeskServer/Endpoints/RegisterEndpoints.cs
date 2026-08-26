using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Identity;
using HelpDeskServer.Services;
using HelpDeskServer.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using HelpDeskServer.Dtos;



namespace HelpDeskServer.Endpoints;

/// <summary>
/// 사용자 등록 및 로그인 관련 엔드포인트
/// </summary>
public static class RegisterEndpoints {
  //public record CustomerCreateDto([Required] string LoginId, [Required] string UserName, [Required] string Email, [Required] string Password, int CompanyId, string? Sex, string? Photo, string? CreatedBy, string? MenuContext);

  /// <summary>
  /// 사용자 등록 및 로그인 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapRegistEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/users");


    var passwordService = new PasswordService();

    // 회원가입(/singup) 엔드포인트는 제거했다 (결정 Q4).
    //
    // 비밀번호를 받아 헬프데스크 고객 계정을 만드는 자체 가입 경로였다.
    // 계정과 인증은 JSini 관리 포털(AuthServer)이 단독으로 맡는다.
    // 헬프데스크 고객 '조직 데이터' 는 조직 관리 › 고객 화면에서 등록한다
    // (POST /api/customers — 비밀번호를 받지 않는다).

    // ── 헬프데스크 자체 로그인 ────────────────────────────
    //
    // 인증은 JSini 포털(AuthServer)이 단독으로 맡는다. 이 경로는 이식 전 JinReception 이
    // 쓰던 자체 로그인이라 기본으로 닫아 둔다. `LocalLogin:Enabled=true` 로만 다시 열린다.
    //
    // 닫아 두는 이유가 하나 더 있다. 아래 비밀번호 검증에 `backdoor` 라는 만능 비밀번호가
    // 들어 있었다(제거함). 그 문자열만 알면 어떤 계정으로든 헬프데스크 토큰을 받을 수 있었다.
    // 게이트웨이가 이제 익명 접근을 막지만, 포털 토큰을 가진 사용자라면 이 경로로
    // 헬프데스크 관리자 토큰을 만들 수 있었다.
    group.MapPost("/login", (AppDbContext db, IConfiguration config, LoginRequest req) => {
      // ApiResponseBuilder는 성공/실패만 다루므로, 인증 실패는 별도 처리합니다.
      return ApiResponseBuilder.CreateAsync(async () => {

        if (!config.GetValue("LocalLogin:Enabled", false)) {
          throw new InvalidOperationException(
              "헬프데스크 자체 로그인은 사용하지 않습니다. JSini 포털 계정으로 로그인하세요.");
        }

        string user_uid = "";
        string user_name = "";
        string login_id = "";
        string login_type = "";
        string Photo = "";
        string email = "";
        string affiliation = ""; // 소속
        string company_id = ""; // 소속 회사 ID
        string team_id = ""; // 소속 팀 ID
        bool mustChangePassword = false;
        bool isAdmin = false;
        bool isManager = false;
        bool isCustomer = false;


        // 1. 사용자 또는 관리자 계정 찾기
        // Use IgnoreQueryFilters to check IsDeleted status specifically
        var customer = await db.Customers.IgnoreQueryFilters().Include(u => u.Company).FirstOrDefaultAsync(u => u.LoginId == req.LoginId);
        // Ignore global filter for login to check IsDeleted status if needed, 
        // but since we want to handle IsDeleted specifically, we query it.
        var admin = await db.Admins.IgnoreQueryFilters().Include(a => a.AdminTeams).ThenInclude(at => at.Team).FirstOrDefaultAsync(a => a.LoginId == req.LoginId);

        if (customer == null && admin == null) {
          return null; // 사용자가 존재하지 않음
        }

        if (admin != null && admin.IsDeleted) {
          throw new InvalidOperationException("휴면 사용자입니다. 관리자에게 문의하세요.");
        }

        if (customer != null && customer.IsDeleted) {
          throw new InvalidOperationException("휴면 사용자입니다. 관리자에게 문의하세요.");
        }

        // 2. 계정 잠금 상태 확인 및 비밀번호 검증
        bool isAuthenticated = false;
        if (customer != null) {
          if (customer.LockoutEnd.HasValue && customer.LockoutEnd.Value > DateTime.UtcNow) {
            var remainingTime = (customer.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes;
            throw new InvalidOperationException($"계정이 잠겼습니다. 약 {Math.Ceiling(remainingTime)}분 후에 다시 시도해주세요.");
          }

          if (passwordService.VerifyPassword(customer, req.Password)) {
            isAuthenticated = true;
            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;
          }
          else {
            customer.FailedLoginAttempts++;
            if (customer.FailedLoginAttempts >= 5) customer.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
          }
          await db.SaveChangesAsync();
        }
        else if (admin != null) // customer가 없거나 비밀번호가 틀렸을 경우 admin 확인
        {
          if (admin.LockoutEnd.HasValue && admin.LockoutEnd.Value > DateTime.UtcNow) {
            var remainingTime = (admin.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes;
            throw new InvalidOperationException($"계정이 잠겼습니다. 약 {Math.Ceiling(remainingTime)}분 후에 다시 시도해주세요.");
          }

          if (passwordService.VerifyPassword(admin, req.Password)) {
            isAuthenticated = true;
            admin.FailedLoginAttempts = 0;
            admin.LockoutEnd = null;
          }
          else {
            admin.FailedLoginAttempts++;
            if (admin.FailedLoginAttempts >= 5) admin.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
          }
          await db.SaveChangesAsync();
        }

        if (!isAuthenticated) {
          return null; // 인증 실패
        }

        // 3. 인증 성공 후 사용자 정보 설정
        if (customer != null && passwordService.VerifyPassword(customer, req.Password)) // customer 우선
        {
          user_name = customer.UserName;
          login_id = customer.LoginId;
          login_type = "customer";
          Photo = customer.Photo;
          email = customer.Email;
          user_uid = customer.Id.ToString();
          affiliation = customer.Company?.Name ?? "";

          company_id = customer.Company?.Id.ToString() ?? ""; // 소속 회사 ID
          team_id = ""; // 소속 팀 ID

          isAdmin = false;
          isManager = false;
          isCustomer = true;


        }
        else if (admin != null) {
          user_name = admin.UserName;
          login_id = admin.LoginId;
          login_type = "admin";
          Photo = admin.Photo;
          email = admin.Email;
          user_uid = admin.Id.ToString();
          mustChangePassword = admin.MustChangePassword;

          var firstTeam = admin.AdminTeams?.FirstOrDefault()?.Team;
          affiliation = firstTeam?.Name ?? "";


          company_id = ""; // 소속 회사 ID
          team_id = firstTeam?.Id.ToString() ?? ""; // 소속 팀 ID

          isAdmin = true;
          isManager = true;
          isCustomer = false;


        }

        // JWT 토큰 생성
        // 자체 로그인 토큰을 발급하는 자리다. 폴백 키로 서명하면 저장소를 본 사람이
        // 그 토큰을 위조할 수 있다 (결정 D1-B).
        var jwtKey = JSini.Shared.Infrastructure.JwtKeyGuard.Require(
            config, "Jwt:Key", "HelpDeskServer");
        var jwtIssuer = config["Jwt:Issuer"] ?? "HelpDeskServer";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
                    new Claim(JwtRegisteredClaimNames.Sub, login_id),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("uid", user_uid),
                    new Claim("login_type", login_type)
        };

        if (!string.IsNullOrEmpty(company_id)) claims.Add(new Claim("company_id", company_id));

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(72),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new {
          token = tokenString,
          user = new {
            user_uid,
            user_name,
            login_id,
            login_type,
            Photo,
            email,
            affiliation, // 소속
            mustChangePassword,

            company_id,
            team_id,

            isAdmin,
            isManager,
            isCustomer
          }
        };
      }, "Login successful.");
    });


    group.MapGet("/info", (AppDbContext db, HttpContext http) => ApiResponseBuilder.CreateAsync<object?>(async () => {
      var loginType = http.User.Claims.FirstOrDefault(c => c.Type == "login_type")?.Value;
      var token_uid = http.User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;

      // 로그인한 사람이 누구인지는 JSini 계정이 정한다.
      // 헬프데스크 레코드는 기존 데이터를 가리키는 내부 ID 를 위해 함께 읽을 뿐이다.
      var jsini = http.GetJsiniUser();
      var me = http.GetHelpdeskPrincipal();

      // 헬프데스크 레코드가 없어도 "누가 로그인했는지" 는 알려 준다.
      //
      // 전에는 여기서 null 을 돌려주었다(HTTP 404). 화면은 사용자 정보를 아예 받지 못해
      // 이름도 권한도 모르는 상태로 떴다. 포털 계정 46개 중 연결된 것은 하나뿐이었으므로
      // 사실상 한 사람 말고는 전부 이 길로 떨어졌다.
      if (!int.TryParse(token_uid, out var uid)) {
        if (jsini is null) return null;   // 신원 자체가 없다 (있을 수 없는 경우)

        return new {
          Id = (int?)null,
          UserName = jsini.UserName,
          LoginId = jsini.UserId,
          Email = jsini.Email,
          loginType = me.IsAdmin ? "admin" : null,
          linked = false,
          isAdmin = me.IsAdmin,
          adminByRole = me.IsAdmin,
          jsiniUserId = jsini.UserId,
          jsiniUserName = jsini.UserName,
          jsiniEmail = jsini.Email,
          jsiniRoles = jsini.Roles
        };
      }

      if (loginType == "admin") {
        var adm = await db.Admins.Include(a => a.AdminTeams).ThenInclude(at => at.Team).FirstOrDefaultAsync(a => a.Id == uid);
        if (adm == null) return null;

        string? thumb = null;
        var photo = adm.Photo ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(photo) && !photo.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
          try {
            // photo가 URL 또는 경로일 수 있음. 마지막 파일명 추출 후 확장자를 _thumb.jpg로 변경
            var fileName = Path.GetFileName(photo);
            var idx = fileName.LastIndexOf('.');
            if (idx >= 0) {
              var thumbName = fileName.Substring(0, idx) + "_thumb.jpg";
              var basePath = photo.Substring(0, Math.Max(0, photo.LastIndexOf('/') + 1));
              thumb = basePath + thumbName;
            }
          }
          catch {
            thumb = null;
          }
        }

        var firstTeam = adm.AdminTeams?.FirstOrDefault()?.Team;
        return new {
          adm.Id,
          // 이름·이메일은 JSini 계정을 정본으로 본다. 헬프데스크 레코드 값은 helpdesk* 로 함께 준다.
          UserName = jsini?.UserName ?? adm.UserName,
          adm.LoginId,
          adm.Photo,
          thumb,
          Email = jsini?.Email ?? adm.Email,
          helpdeskUserName = adm.UserName,
          helpdeskEmail = adm.Email,
          TeamId = firstTeam?.Id,
          teamName = firstTeam?.Name,
          loginType,
          linked = true,
          isAdmin = me.IsAdmin,
          adminByRole = me.IsAdmin && !me.IsLinkedAdmin,
          jsiniUserId = jsini?.UserId,
          jsiniUserName = jsini?.UserName,
          jsiniEmail = jsini?.Email,
          jsiniRoles = jsini?.Roles ?? new List<string>()
        };
      }
      else {
        var cus = await db.Customers.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == uid);
        if (cus == null) return null;

        string? thumb = null;
        var photo = cus.Photo ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(photo) && !photo.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
          try {
            var fileName = Path.GetFileName(photo);
            var idx = fileName.LastIndexOf('.');
            if (idx >= 0) {
              var thumbName = fileName.Substring(0, idx) + "_thumb.jpg";
              var basePath = photo.Substring(0, Math.Max(0, photo.LastIndexOf('/') + 1));
              thumb = basePath + thumbName;
            }
          }
          catch {
            thumb = null;
          }
        }

        return new {
          cus.Id,
          UserName = jsini?.UserName ?? cus.UserName,
          cus.LoginId,
          cus.Sex,
          cus.Photo,
          thumb,
          Email = jsini?.Email ?? cus.Email,
          helpdeskUserName = cus.UserName,
          helpdeskEmail = cus.Email,
          cus.CompanyId,
          companyName = cus.Company?.Name,
          loginType,
          linked = true,
          isAdmin = me.IsAdmin,
          adminByRole = me.IsAdmin && !me.IsLinkedAdmin,
          jsiniUserId = jsini?.UserId,
          jsiniUserName = jsini?.UserName,
          jsiniEmail = jsini?.Email,
          jsiniRoles = jsini?.Roles ?? new List<string>()
        };
      }

      return null;
    }, "User information retrieved successfully."));

  }
}
