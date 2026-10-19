using System.Reflection;
using JSini.Web.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components;

/// <summary>
/// 출력 폴더를 훑어 업무 MFE 모듈(IPortalModule 구현)을 찾아 등록한다. 셸 코드는 모듈을 이름으로 알지 않는다. 모듈을 넣고 빼는 데 셸을 고칠 일이 없어야 하기 때문이다.
/// 훑기는 없는 것을 알아채지 못한다. 참조가 빠져 DLL 이 안 실리면 조용히 0개가 된다. 그래서 셸이 appsettings 의 PortalApps 로 기대 목록을 적어 기동 때 대조하고, 아키텍처 테스트도 빌드 때 같은 대조를 한다.
/// </summary>
public sealed class PortalModuleRegistry
{
    /// <summary>모듈이 아닌 공용 어셈블리는 이름으로 거른다. 공용 어셈블리마다 GetTypes() 를 부르면 타입 로드 예외가 나기 쉬워 애초에 열지 않는다.</summary>
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

    /// <summary>찾은 모듈들. Key 순으로 정렬돼 있다.</summary>
    public IReadOnlyList<IPortalModule> Modules => _modules;

    /// <summary>모듈들의 어셈블리. 셸의 Router 가 AdditionalAssemblies 로 흡수해 @page 를 한 라우터에 합친다. 이 목록이 비면 업무 화면이 전부 404 다.</summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>출력 폴더의 JSini.Web.*.dll 을 훑어 모듈을 찾고, 각 모듈의 ConfigureServices 를 부른다.</summary>
    /// <param name="services">서비스 모음</param>
    /// <param name="configuration">설정</param>
    /// <param name="searchDirectory">훑을 폴더. 기본은 실행 폴더.</param>
    /// <param name="logger">찾은 것과 못 찾은 것을 남긴다. 기동 초기라 DI 가 아직 없어서, 여기서 남기지 않으면 모듈이 왜 안 붙었는지 알 방법이 없다.</param>
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
