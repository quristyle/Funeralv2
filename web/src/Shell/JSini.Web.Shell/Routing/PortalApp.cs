using Microsoft.Extensions.Configuration;

namespace JSini.Web.Shell.Routing;

/// <summary>셸이 "이 업무 모듈은 반드시 붙어 있어야 한다"고 선언해 둔 한 줄. 모듈은 어셈블리를 훑어 찾는데 훑기는 없는 것을 알아채지 못한다. 그래서 기대 목록을 설정에 적어 기동 때 훑은 결과와 대조한다(아키텍처 테스트도 빌드 때 같은 대조를 한다).</summary>
public sealed class PortalApp
{
    /// <summary>설정 구역 이름.</summary>
    public const string SectionName = "PortalApps";

    /// <summary>모듈 식별자 (<c>funeral</c>). <c>IPortalModule.Key</c> 와 같아야 한다.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름 (<c>장례식장</c>). 진단 화면에만 쓴다.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>이 모듈이 가져가는 경로 접두사(<c>/funeral</c>). 모듈 쪽 RoutePrefix 와 같아야 한다.</summary>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>설정에서 기대 목록을 읽는다.</summary>
    public static IReadOnlyList<PortalApp> Read(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<List<PortalApp>>() ?? [];
}
