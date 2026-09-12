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
///
/// <para>
/// [프로시저 이름을 막던 목록이 여기 있었다]
/// </para>
/// <para>
/// <c>sp_proj_login</c> — 아이디·비밀번호를 받아 사용자 행을 돌려주던 이식 전
/// 자체 로그인이다. 프로시저 이름을 <b>클라이언트가 정하는 통로</b>가 있어서
/// 라우트를 지우는 것만으로는 막히지 않았고, 그래서 이름으로 막았다.
/// </para>
/// <para>
/// <b>그 통로가 없어졌다</b>(2026-09-12 — <c>/api/Proj</c> · <c>/api/Sys</c>).
/// 지금 <see cref="RequestDto.ProcName"/> 에 오는 것은 프로시저 이름이 아니라
/// <c>projmng.devsqlresp</c> 에 <b>등록된 질의 이름</b>(<c>tablelist</c> …)이거나
/// 파일 훑기 이름(<c>md_*</c>)이다. 막을 이름이 남지 않아 목록을 걷어냈다.
/// </para>
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
  ///
  /// <para>
  /// 포털 로그인 아이디를 그대로 <c>req_ss_user_id</c> 로 쓰면 이관 계정은 아무것도 못 찾는다.
  /// 이관 당시 아이디 충돌을 피하려고 접두어를 붙였기 때문이다 —
  /// <c>projmng.dev_user.user_id = 'jskim'</c> 인데 포털 계정은 <c>pm_jskim</c> 이다.
  /// 그래서 저장 프로시저가 남기는 감사 값이 존재하지 않는 사용자를 가리키고,
  /// '내 프로젝트 정보' 같은 화면은 빈 채로 뜬다. 실제로 9명 중 <c>quristyle</c> 한 명만 맞는다.
  /// </para>
  ///
  /// <para>
  /// 게이트웨이가 실어 보내는 <c>X-User-Msa-Source</c>(<c>projmng:dev_user:jskim</c>)는
  /// 이관 스크립트가 남긴 기록이라 추정이 아니다. 다만 이 값을 쓰기 시작하면
  /// <b>감사 컬럼에 쌓이는 값이 달라지므로</b> 기본은 꺼 두고 설정으로 켠다
  /// (<c>Identity:UseMsaSource</c>).
  /// </para>
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

      dto.Start = DateTime.Now;
    }
  }

  /// <inheritdoc />
  public void OnActionExecuted(ActionExecutedContext context) { }
}
