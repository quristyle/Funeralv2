using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using JSini.Web.Models.Menu;
using JSini.Web.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 권한 판정의 유일한 자리. 사이드바 거르기·진입 가드·화면 안 단추가 모두 여기에 묻는다. 판정이 두 곳이면 목록에 보이는데 누르면 403 이 생긴다. scoped 다(사용자마다 권한이 다르다).
/// </summary>
public sealed class PermissionContext(
    GatewayClient gateway,
    ILogger<PermissionContext> logger) : IPermissionContext
{
    private Dictionary<string, MenuPermissionDto> _byPath = [];

    public bool IsLoaded { get; private set; }

    public bool CanView(string path) => Can(path, MenuAction.View);

    public bool Can(string path, MenuAction action)
    {
        // 권한표를 받기 전에는 아무것도 거르지 않는다. 「권한 없음」으로 다루면 로그인 직후 사이드바가 비었다 채워지므로 통과시킨다. 실제 통제는 서버가 한다.
        if (!IsLoaded)
        {
            return true;
        }

        return _byPath.TryGetValue(Normalize(path), out var permission)
            && permission.Allows(action);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Apply(await gateway.GetListAsync<MenuPermissionDto>(
                "auth/menu/permissions", cancellationToken));
        }
        catch (ApiException ex)
        {
            // 여기서 다시 던지면 로그인 직후 화면이 통째로 깨진다. 권한표가 없으면 아무것도 거르지 않는다(IsLoaded = false).
            logger.LogWarning(ex, "권한표를 읽지 못했다. 이번에는 거르지 않는다.");
        }
    }

    /// <summary>이미 받아 둔 권한표를 채운다. 부트스트랩(PortalBootstrap)이 쓰는 길이다. 게이트웨이를 다시 부르지 않는다.</summary>
    public void Apply(IReadOnlyList<MenuPermissionDto> list)
    {
        _byPath = list
            .Where(p => !string.IsNullOrWhiteSpace(p.Path))
            // 같은 경로가 두 줄 오면 뒤엣것을 쓴다. 겹치면 예외로 죽기보다 낫다(권한 화면 하나 때문에 포털 전체가 안 뜨면 곤란하다).
            .ToDictionary(p => Normalize(p.Path), p => p, StringComparer.Ordinal);

        IsLoaded = true;
        logger.LogInformation("권한표를 읽었다: {Count}건", _byPath.Count);
    }

    /// <summary>
    /// 경로를 권한표의 열쇠 모양으로 맞춘다(앞뒤 공백 제거 · 소문자 · 끝의 / 제거, 단 / 하나는 남긴다). Vue 의 normalize() 와 같은 규칙이어야 한다.
    /// PermissionView 는 지금 열린 주소(/admin/system/account)로 묻는데 권한표는 DB 경로(/system/account)라, 여기서 둘을 맞춰 찾게 되돌린다. 판정이 이 한 곳이라 사이드바·가드·단추가 같은 열쇠를 쓴다. 들어오는 표에는 손대지 않는다.
    /// </summary>
    private static string Normalize(string path)
    {
        var trimmed = path.Trim().ToLowerInvariant();

        var cut = trimmed.Length > 1 && trimmed.EndsWith('/')
            ? trimmed[..^1]
            : trimmed;

        return RouteAliases.ToMenuPath(cut);
    }
}
