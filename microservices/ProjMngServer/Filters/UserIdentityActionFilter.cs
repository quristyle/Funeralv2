using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjModel;

namespace ProjMngServer.Filters;

/// <summary>
/// 요청 본문의 <see cref="RequestDto.SSUserId"/> 를 게이트웨이가 붙여 준 신원으로 덮어쓴다.
///
/// 이식 전(Blazor WASM) 에는 클라이언트가 로그인한 사용자 아이디를 직접 실어 보냈다.
/// 브라우저에서 오는 값이라 얼마든지 바꿔 보낼 수 있었고, 저장 프로시저는 그 값을
/// <c>req_ss_user_id</c> 로 받아 감사·권한 판단에 썼다.
///
/// 포털에 붙은 뒤로는 신원의 출처가 하나다 — ApiGateway 가 JWT 를 검증한 뒤 붙이는
/// <c>X-User-Id</c> 헤더다(ApiGateway/Program.cs 에서 외부에서 들어온 X-User-* 는 먼저 지운다).
/// 그 값은 JSini 로그인 아이디(<c>scom.accounts.user_id</c>)이고, 프로젝트관리의
/// <c>projmng.dev_user.user_id</c> 와 같은 체계다.
/// </summary>
public class UserIdentityActionFilter : IActionFilter {

  /// <summary>
  /// 인증 성격의 프로시저는 이 통로로 부르지 못하게 막는다.
  ///
  /// <c>sp_proj_login</c> 은 아이디·비밀번호를 받아 사용자 행을 돌려주던 이식 전 자체 로그인이다.
  /// 인증은 JSini 포털(AuthServer)이 단독으로 맡으므로 여기서 다시 열어 둘 이유가 없다.
  /// 이 경로는 프로시저 이름을 클라이언트가 정하므로, 라우트를 지우는 것만으로는 막히지 않는다.
  /// </summary>
  private static readonly HashSet<string> BlockedProcedures =
    new(StringComparer.OrdinalIgnoreCase) { "sp_proj_login" };

  private readonly IHostEnvironment _env;
  private readonly ILogger<UserIdentityActionFilter> _logger;

  /// <summary>필터를 생성한다.</summary>
  public UserIdentityActionFilter(IHostEnvironment env, ILogger<UserIdentityActionFilter> logger) {
    _env = env;
    _logger = logger;
  }

  /// <inheritdoc />
  public void OnActionExecuting(ActionExecutingContext context) {
    var userId = context.HttpContext.Request.Headers["X-User-Id"].ToString();

    foreach (var arg in context.ActionArguments.Values) {
      if (arg is not RequestDto dto) continue;

      if (BlockedProcedures.Contains(dto.ProcName ?? string.Empty)) {
        _logger.LogWarning("[신원] 차단된 프로시저 호출: {ProcName} (user={UserId})", dto.ProcName, userId);
        context.Result = new ObjectResult(new {
          code = -403,
          message = "인증은 JSini 포털이 담당합니다. 이 프로시저는 사용하지 않습니다.",
          data = Array.Empty<object>()
        }) { StatusCode = 403 };
        return;
      }

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

      dto.Start = DateTime.Now;
    }
  }

  /// <inheritdoc />
  public void OnActionExecuted(ActionExecutedContext context) { }
}
