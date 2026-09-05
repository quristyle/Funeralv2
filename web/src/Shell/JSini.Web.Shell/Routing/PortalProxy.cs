using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace JSini.Web.Shell.Routing;

/// <summary>
/// 업무 MFE 목록에서 YARP 라우트·클러스터를 만들어 준다.
///
/// [YARP 설정을 손으로 적지 않는 이유]
///
/// appsettings 에 <c>ReverseProxy</c> 블록을 직접 적으면 앱 하나당 라우트 1개 +
/// 클러스터 1개, 여섯 앱이면 열두 덩어리다. 그리고 <c>PortalApps</c> 목록과
/// 이중으로 관리된다 — 하나를 고치고 다른 하나를 잊으면 그 업무만 조용히 404 다.
/// 목록 하나에서 둘 다 만들면 어긋날 수가 없다.
///
/// 게이트웨이(ApiGateway)가 YARP 를 appsettings 로 쓰는 것과 방식이 다른데,
/// 거기는 경로마다 인증 정책·요율 제한이 달라서 손으로 적을 값이 실제로 많다.
/// 여기는 "접두사를 그대로 넘긴다" 하나뿐이다.
/// </summary>
public static class PortalProxy
{
    /// <summary>
    /// 업무 앱 목록을 읽어 프록시를 등록한다.
    /// </summary>
    public static IServiceCollection AddPortalProxy(
        this IServiceCollection services,
        IReadOnlyList<PortalApp> apps)
    {
        var routes = new List<RouteConfig>(apps.Count);
        var clusters = new List<ClusterConfig>(apps.Count);

        foreach (var app in apps)
        {
            routes.Add(new RouteConfig
            {
                RouteId = $"app-{app.Key}",
                ClusterId = $"cluster-{app.Key}",

                // **접두사를 떼지 않는다.** 업무 앱이 UsePathBase 로 그 접두사
                // 아래에서 살기 때문이다. 여기서 떼면 앱이 자기 base href 를
                // 잘못 계산해서 정적자원과 Blazor 회로 주소가 어긋난다.
                Match = new RouteMatch { Path = $"{app.RoutePrefix}/{{**catch-all}}" },
            });

            clusters.Add(new ClusterConfig
            {
                ClusterId = $"cluster-{app.Key}",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    [app.Key] = new() { Address = app.Address },
                },
            });
        }

        // YARP 는 웹소켓을 그대로 통과시킨다. Blazor Server 회로(_blazor)가
        // 그 위에 있으므로 이게 없으면 화면은 뜨는데 버튼이 안 눌린다.
        services.AddReverseProxy().LoadFromMemory(routes, clusters);

        return services;
    }
}
