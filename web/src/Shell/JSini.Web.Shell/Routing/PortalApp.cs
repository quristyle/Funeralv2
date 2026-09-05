using Microsoft.Extensions.Configuration;

namespace JSini.Web.Shell.Routing;

/// <summary>
/// 셸이 "이 업무 모듈은 반드시 붙어 있어야 한다" 고 선언해 둔 한 줄.
///
/// [설정으로 남겨 둔 이유 — 이것이 유일한 안전망이다]
///
/// 모듈은 어셈블리를 훑어 찾는다(<c>PortalModuleRegistry</c>). 훑기는 편하지만
/// <b>없는 것을 알아채지 못한다</b>. 참조가 빠져 DLL 이 안 실려도 "0개 찾음" 이
/// 정상 동작처럼 지나가고, 증상은 한참 뒤 "메뉴를 눌러도 화면이 안 열린다" 로
/// 나타난다. 실제로 그렇게 한동안 굴러갔다.
///
/// 그래서 기대 목록을 사람이 적어 둔다. 기동 때 훑은 결과와 대조해서 어긋나면
/// 로그를 크게 남기고, 아키텍처 테스트도 같은 대조를 빌드 때 한다.
///
/// <c>Address</c> 칸이 없어진 것에 유의. 모듈이 각자 프로세스이던 시절에는
/// 셸이 YARP 로 넘길 주소가 필요했지만, 지금은 한 프로세스라 넘길 곳이 없다.
/// </summary>
public sealed class PortalApp
{
    /// <summary>설정 구역 이름.</summary>
    public const string SectionName = "PortalApps";

    /// <summary>모듈 식별자 (<c>funeral</c>). <c>IPortalModule.Key</c> 와 같아야 한다.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름 (<c>장례식장</c>). 진단 화면에만 쓴다.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 이 모듈이 가져가는 경로 접두사 (<c>/funeral</c>).
    /// 모듈 쪽 <c>IPortalModule.RoutePrefix</c> 와 같아야 한다.
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>설정에서 기대 목록을 읽는다.</summary>
    public static IReadOnlyList<PortalApp> Read(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<List<PortalApp>>() ?? [];
}
