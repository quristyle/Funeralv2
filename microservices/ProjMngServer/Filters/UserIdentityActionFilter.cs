using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjModel;

namespace ProjMngServer.Filters;

/// <summary>
/// 요청 본문의 RequestDto.SSUserId 를 게이트웨이가 붙여 준 X-User-Id 로 덮어쓴다. 본문 값은 브라우저에서 바꿔 보낼 수 있어 감사·권한 판단에 쓰면 안 된다.
/// </summary>
public class UserIdentityActionFilter : IActionFilter {

  private readonly IHostEnvironment _env;
  private readonly ILogger<UserIdentityActionFilter> _logger;
  private readonly bool _useMsaSource;

  /// <summary>필터를 생성한다.</summary>
  public UserIdentityActionFilter(
      IHostEnvironment env, ILogger<UserIdentityActionFilter> logger, IConfiguration configuration) {
    _env = env;
    _logger = logger;
    _useMsaSource = configuration.GetValue("Identity:UseMsaSource", false);
  }

  /// <summary>
  /// 포털 계정이 어느 프로젝트관리 사용자에서 왔는지 읽어낸다.
  /// 포털 로그인 아이디를 그대로 req_ss_user_id 로 쓰면 이관 계정이 찾히지 않는다(이관 때 접두어 pm_ 를 붙였다). 그래서 X-User-Msa-Source(projmng:dev_user:…)로 찾는다 — 감사 칸의 값이 달라지므로 기본은 끄고 Identity:UseMsaSource 로 켠다.
  /// </summary>
  private static string? ProjMngUserIdFrom(string? msaSource) {
    if (string.IsNullOrWhiteSpace(msaSource)) return null;

    var parts = msaSource.Split(':');
    if (parts.Length != 3) return null;
    if (!string.Equals(parts[0], "projmng", StringComparison.OrdinalIgnoreCase)) return null;
    if (!string.Equals(parts[1], "dev_user", StringComparison.OrdinalIgnoreCase)) return null;

    return string.IsNullOrWhiteSpace(parts[2]) ? null : parts[2];
  }

  /// <inheritdoc />
  public void OnActionExecuting(ActionExecutingContext context) {
    var userId = context.HttpContext.Request.Headers["X-User-Id"].ToString();

    if (_useMsaSource) {
      var mapped = ProjMngUserIdFrom(context.HttpContext.Request.Headers["X-User-Msa-Source"].ToString());
      if (!string.IsNullOrEmpty(mapped) && mapped != userId) {
        _logger.LogInformation("[신원] 포털 계정 {PortalId} → 프로젝트관리 사용자 {ProjId}", userId, mapped);
        userId = mapped;
      }
    }

    foreach (var arg in context.ActionArguments.Values) {
      if (arg is not RequestDto dto) continue;

      if (string.IsNullOrEmpty(userId)) {
        // 게이트웨이를 지나지 않은 직접 호출이다.
        // 개발 중에는 본문 값을 그대로 두어 Swagger 로 시험하는 길을 막지 않는다.
        // 그 밖의 환경에서는 본문 값을 믿지 않는다 — 믿으면 아무나 남의 아이디로 감사 기록을 남길 수 있다.
        if (!_env.IsDevelopment() && !string.IsNullOrEmpty(dto.SSUserId)) {
          _logger.LogWarning("[신원] 헤더 없는 요청의 본문 SSUserId 를 무시했습니다: {Body}", dto.SSUserId);
          dto.SSUserId = string.Empty;
        }
      }
      else {
        dto.SSUserId = userId;
      }

      dto.Start = DateTime.UtcNow;
    }
  }

  /// <inheritdoc />
  public void OnActionExecuted(ActionExecutedContext context) { }
}
