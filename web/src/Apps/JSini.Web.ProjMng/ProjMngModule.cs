using JSini.Web.Abstractions;
using JSini.Web.ProjMng.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.ProjMng;

/// <summary>
/// 프로젝트관리 모듈이 셸에 자기를 알리는 자리.
///
/// 셸은 이 클래스를 이름으로 알지 못한다 — 어셈블리를 훑어
/// <see cref="IPortalModule"/> 구현을 찾아 등록할 뿐이다. 그래서 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없다.
/// </summary>
public sealed class ProjMngModule : IPortalModule
{
    public string Key => "projmng";

    public string DisplayName => "프로젝트관리";

    public string RoutePrefix => "/projmng";

    /// <summary>
    /// 이 업무 전용 스타일. 셸이 <c>&lt;head&gt;</c> 에 실어 준다.
    ///
    /// 한동안 이것이 없어서 화면들이 쓰는 <c>pm-*</c> 클래스 몇 개가
    /// 정의되지 않은 채였다. 화면은 뜨고 글자도 보이므로 눈에 잘 안 띈다.
    /// </summary>
    public string? StyleSheet => "_content/JSini.Web.ProjMng/projmng.css";


    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProjMngClient>();

        // 프로시저를 걷어내며 생기는 업무 클라이언트들. 범용 통로와 달리
        // **자기 자료만** 안다 — 화면이 타입 있는 목록을 받고 `CommGrd` 를 쓴다.
        services.AddScoped<ProjectClient>();
        services.AddScoped<ProjectUserClient>();
        services.AddScoped<DevCommonCodeClient>();
        services.AddScoped<AiModelCodes>();
        services.AddScoped<SourceInfoClient>();
        services.AddScoped<ProjectDbClient>();
        services.AddScoped<HomeTodoClient>();
        services.AddScoped<WbsClient>();
        services.AddScoped<DbLogicClient>();
        services.AddScoped<ActivityInfoClient>();
        services.AddScoped<ProjectPropClient>();
        services.AddScoped<CommonCodes>();

        // AI 작업 지시 — docs/ai-task-runner.md.
        // 게이트웨이 경로는 `projmng/ai-tasks` 다. 백엔드가 ProjMngServer 라
        // 다른 열한 개와 접두사가 같다.
        services.AddScoped<AiTaskClient>();
        services.AddScoped<AiTargetClient>();

        // 지시자의 이름과 얼굴. 업무 자료에는 로그인 아이디만 있어서
        // 계정 쪽(`auth/user/faces`)에 한 번 더 묻는다 — 한 번 물어본 것은
        // 들고 있으므로 목록을 다시 읽어도 왕복이 늘지 않는다.
        services.AddScoped<UserFaceClient>();

        // 작성 중인 지시문을 브라우저에 적어 두는 곳. 화면이 아니라 서비스인
        // 이유는 화면이 다시 만들어질 때마다(탭을 옮길 때마다) 새로 태어나는
        // 것이 화면이기 때문이다 — 적어 둔 곳은 그것보다 오래 남아야 한다.
        services.AddScoped<Components.Shared.AiTaskDraftStore>();

        // 「빠른 지시」에서 마지막에 고른 것(대상 · AI · 올리기 · 메일).
        // 같은 이유로 화면 밖에 둔다 — 탭을 옮겨도 기억이 남아야 한다.
        services.AddScoped<Components.Shared.AiAskPrefs>();
        services.AddScoped<BizOptions>();

        // 이 앱 전용 서비스만 여기 등록한다.
        // 게이트웨이 클라이언트·권한·알림은 셸이 이미 올려 두었다.
    }
}
