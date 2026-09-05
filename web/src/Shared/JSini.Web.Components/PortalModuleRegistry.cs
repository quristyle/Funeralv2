using System.Reflection;
using JSini.Web.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components;

/// <summary>
/// 출력 폴더를 훑어 업무 MFE 모듈(<see cref="IPortalModule"/> 구현)을 찾아 등록한다.
///
/// [셸이 모듈을 이름으로 알지 않게 하는 자리]
///
/// 셸 코드에는 <c>using JSini.Web.Funeral</c> 같은 줄이 한 곳도 없다. 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없어야 하기 때문이다. 대신 여기서 어셈블리를
/// 훑는다. csproj 의 ProjectReference 는 DLL 을 출력 폴더로 모으는 역할만 한다.
///
/// [훑기의 약점과 그 대책]
///
/// 훑기는 <b>없는 것을 알아채지 못한다.</b> 참조가 빠져 DLL 이 안 실리면
/// 조용히 0개가 되고, 셸은 멀쩡히 뜨고 로그인도 되고 첫 화면도 나온다 —
/// 업무 메뉴를 누를 때만 404 다. 실제로 그 상태로 한동안 굴러갔다.
///
/// 그래서 셸이 <c>appsettings</c> 의 <c>PortalApps</c> 로 기대 목록을 적어 두고
/// 기동 때 대조한다. 아키텍처 테스트도 같은 대조를 빌드 때 한다.
/// </summary>
public sealed class PortalModuleRegistry
{
    /// <summary>
    /// 모듈이 아닌 공용 어셈블리. 이름으로 거른다.
    ///
    /// "IPortalModule 구현이 없으면 자동으로 넘어가니 목록이 필요 없다" 고 볼
    /// 수도 있지만, 그러면 공용 어셈블리마다 <c>GetTypes()</c> 를 부르게 되고
    /// 그건 타입 로드 예외가 나기 쉬운 호출이다. 애초에 열지 않는 편이 낫다.
    /// </summary>
    private static readonly string[] NotModules =
    [
        "JSini.Web.Shell",
        "JSini.Web.Abstractions",
        "JSini.Web.Components",
        "JSini.Web.Http",
        "JSini.Web.Models",
    ];

    private readonly List<IPortalModule> _modules = [];
    private readonly List<Assembly> _assemblies = [];

    /// <summary>찾은 모듈들. <see cref="IPortalModule.Key"/> 순으로 정렬돼 있다.</summary>
    public IReadOnlyList<IPortalModule> Modules => _modules;

    /// <summary>
    /// 모듈들의 어셈블리. 셸의 <c>Router</c> 가 <c>AdditionalAssemblies</c> 로
    /// 흡수해서 <c>@page</c> 를 한 라우터에 합친다. <b>이 목록이 비면 업무
    /// 화면이 전부 404 다.</b>
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>
    /// 출력 폴더의 <c>JSini.Web.*.dll</c> 을 훑어 모듈을 찾고,
    /// 각 모듈의 <see cref="IPortalModule.ConfigureServices"/> 를 부른다.
    /// </summary>
    /// <param name="services">서비스 모음</param>
    /// <param name="configuration">설정</param>
    /// <param name="searchDirectory">훑을 폴더. 기본은 실행 폴더.</param>
    /// <param name="logger">
    /// 찾은 것과 못 찾은 것을 남긴다. <b>반드시 넘기는 편이 좋다</b> —
    /// 이 단계는 기동 초기라 DI 가 아직 없어서, 여기서 남기지 않으면
    /// 모듈이 왜 안 붙었는지 알 방법이 없다.
    /// </param>
    public static PortalModuleRegistry DiscoverAndRegister(
        IServiceCollection services,
        IConfiguration configuration,
        string? searchDirectory = null,
        ILogger? logger = null)
    {
        var registry = new PortalModuleRegistry();
        var baseDir = searchDirectory ?? AppContext.BaseDirectory;

        var moduleFiles = Directory
            .EnumerateFiles(baseDir, "JSini.Web.*.dll")
            .Where(f => !NotModules.Contains(Path.GetFileNameWithoutExtension(f))
                        && !Path.GetFileNameWithoutExtension(f).EndsWith(".Tests", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        foreach (var file in moduleFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);

                var moduleTypes = assembly.GetTypes()
                    .Where(t => t is { IsAbstract: false, IsInterface: false }
                                && typeof(IPortalModule).IsAssignableFrom(t));

                foreach (var moduleType in moduleTypes)
                {
                    if (Activator.CreateInstance(moduleType) is not IPortalModule module)
                    {
                        continue;
                    }

                    registry._modules.Add(module);

                    if (!registry._assemblies.Contains(module.Assembly))
                    {
                        registry._assemblies.Add(module.Assembly);
                    }

                    module.ConfigureServices(services, configuration);

                    logger?.LogInformation(
                        "업무 MFE 모듈: {Key} ({Name}) → {Prefix}",
                        module.Key, module.DisplayName, module.RoutePrefix);
                }
            }
            catch (Exception ex)
            {
                // 모듈 하나가 깨졌다고 포털 전체를 세우지는 않는다 — 나머지
                // 업무는 계속 돌아야 한다. 대신 조용히 넘어가지도 않는다.
                logger?.LogError(ex, "업무 MFE 모듈을 싣지 못했다: {File}", Path.GetFileName(file));
            }
        }

        registry._modules.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        services.AddSingleton(registry);
        services.AddSingleton<IReadOnlyList<IPortalModule>>(registry.Modules);

        return registry;
    }
}
