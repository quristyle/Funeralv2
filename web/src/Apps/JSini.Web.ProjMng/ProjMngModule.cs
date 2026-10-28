using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.ProjMng;

/// <summary>프로젝트관리 모듈이 셸에 자기를 알리는 자리. 셸은 이름을 모르고 IPortalModule 구현을 훑어 등록할 뿐이라 넣고 빼는 데 셸을 고칠 일이 없다.</summary>
public sealed class ProjMngModule : IPortalModule
{
    public string Key => "projmng";

    public string DisplayName => "프로젝트관리";

    public string RoutePrefix => "/projmng";

    /// <summary>업무 전용 스타일. 셸이 head 에 실어 준다 — 빠지면 pm-* 클래스가 정의 없이 쓰여도 오류 없이 조용히 틀어진다.</summary>
    public string? StyleSheet => "_content/JSini.Web.ProjMng/projmng.css";


    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProjMngClient>();

        // 자기 자료만 아는 업무 클라이언트. 화면이 타입 있는 목록을 받아 CommGrd 에 붙인다.
        services.AddScoped<ProjectClient>();
        services.AddScoped<RagCategoryClient>();
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

        // WBS 대시보드 — 이름은 비슷하지만 다른 표다(WbsClient 는 프로젝트별 공정표).
        services.AddScoped<WbsBoardNames>();
        services.AddScoped<WbsBoardClient>();
        services.AddScoped<WbsBoardTaskClient>();
        services.AddScoped<WbsDevUserClient>();
        services.AddScoped<WbsDocsClient>();

        // EAI 인터페이스 카탈로그.
        services.AddScoped<InterfaceClient>();

        // 저장소를 안 걸어 둔 프로젝트는 빈 채로 뜬다 — 안 쓰는 프로젝트가 정상이다.
        services.AddScoped<GitStatusClient>();

        // AI 작업 지시(docs/ai-task-runner.md). 게이트웨이 경로는 projmng/ai-tasks 다.
        services.AddScoped<AiTaskClient>();
        services.AddScoped<AiTargetClient>();

        // 지시문 돕기(제목 짓기·요약 얹기). 묻는 길은 AI 쳇과 같은 AiChatClient 이고, 이 화면의 AI 고르개(실행할 CLI)와는 다른 길이다.
        services.AddScoped<AiTaskAssist>();

        // AI 일정 메모 — 본문에서 날짜·제목·요약을 짓는다. 같은 AiChatClient 길이다.
        services.AddScoped<ScheduleNoteAssist>();

        // AI 작업 현황. 읽기뿐이다 — 모델 한도는 두 군데를 본다(그 클래스 머리말).
        services.AddScoped<AiDashboardClient>();

        // 지시자 이름과 얼굴은 계정 쪽에 한 번 더 묻는다 — 한 번 물은 것은 들고 있는다.

        // 대상의 git 상태. 서버가 세는 값이 아니라 호스트 실행기가 적어 둔 스냅샷이다.
        services.AddScoped<AiTargetStatusClient>();

        // 작성 중인 지시문을 적어 두는 서비스. 화면은 탭을 옮길 때마다 다시 태어나므로 화면 밖에 둔다.
        services.AddScoped<Components.Shared.AiTaskDraftStore>();

        // 이어서 지시 창의 적다 만 글. 본문 한 벌과 담는 모양이 달라 따로 둔다 — 섞으면 되살릴 때 서로를 덮는다.
        services.AddScoped<Components.Shared.AiContinueDraftStore>();

        // 「빠른 지시」에서 마지막에 고른 것(대상 · AI · 올리기 · 메일).
        // 같은 이유로 화면 밖에 둔다 — 탭을 옮겨도 기억이 남아야 한다.
        services.AddScoped<Components.Shared.AiAskPrefs>();

        // 로그 번역을 화면 밖에 둔다 — 탭을 건너가면 버려지는데 짚어 모은 번역은 살아 있어야 한다.
        services.AddScoped<Components.Shared.AiLogXlatStore>();
        services.AddScoped<BizOptions>();

        // 빠른 지시 알맹이를 헤더 서랍에도 싣는다(책상 화면만). 셸은 모듈 타입을 모르므로 여기서 타입을 건넨다 — 머리말은 QuickAskContent.
        services.AddSingleton(new QuickAskContent(typeof(Components.Shared.AiAskPanel)));

        // AI 일정 메모 공통 팝업 알맹이 등록 (머리말은 ScheduleNotePopupContent).
        services.AddSingleton(new ScheduleNotePopupContent(typeof(Components.Shared.ScheduleNotePopup)));

        // 이 앱 전용 서비스만 등록한다. 게이트웨이 클라이언트·권한·알림은 셸이 이미 올렸다.
    }
}
