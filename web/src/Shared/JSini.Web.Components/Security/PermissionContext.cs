using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using JSini.Web.Models.Menu;
using JSini.Web.Http;

namespace JSini.Web.Components.Security;

/// <summary>
/// 권한 판정의 <b>유일한</b> 자리.
///
/// 사이드바 거르기와 라우트 진입 가드와 화면 안 버튼이 모두 여기에 묻는다.
/// 판정이 두 군데가 되면 "목록에 보이는데 누르면 403" 이 생긴다 — Vue 에서
/// <c>canViewMenu()</c> 하나로 모았던 이유고, 여기서도 그대로 지킨다.
///
/// scoped 다. 사용자마다 권한이 다르므로 회로 하나에 하나씩 있어야 한다.
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
        // 권한표를 받기 전에는 아무것도 거르지 않는다.
        //
        // 못 받은 상태를 "권한 없음" 으로 다루면 로그인 직후 사이드바가 한 번
        // 비었다가 채워진다. 통과시키면 걸러지지 않은 목록이 잠깐 보인다.
        // 둘 다 눈에 띄지만, 실제 통제는 서버가 하므로 후자가 덜 위험하다.
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
            var list = await gateway.GetListAsync<MenuPermissionDto>(
                "auth/menu/permissions", cancellationToken);

            _byPath = list
                .Where(p => !string.IsNullOrWhiteSpace(p.Path))
                // 같은 경로가 두 줄 오면 뒤엣것을 쓴다. 서버가 역할을 OR 로 합쳐
                // 내려주므로 실제로는 겹치지 않지만, 겹쳤을 때 예외로 죽는 것보다
                // 낫다 — 권한 화면 하나 때문에 포털 전체가 안 뜨면 곤란하다.
                .ToDictionary(p => Normalize(p.Path), p => p, StringComparer.Ordinal);

            IsLoaded = true;
            logger.LogInformation("권한표를 읽었다: {Count}건", _byPath.Count);
        }
        catch (ApiException ex)
        {
            // 여기서 다시 던지면 로그인 직후 화면이 통째로 깨진다.
            // 권한표가 없으면 아무것도 거르지 않고(IsLoaded = false) 넘어가되,
            // 실제 통제는 서버가 계속 하므로 안전 범위 안이다.
            logger.LogWarning(ex, "권한표를 읽지 못했다. 이번에는 거르지 않는다.");
        }
    }

    /// <summary>
    /// 경로를 권한표의 열쇠 모양으로 맞춘다.
    ///
    /// 앞뒤 공백을 떼고, 소문자로 내리고, 끝의 <c>/</c> 를 지운다
    /// (경로가 <c>/</c> 하나뿐일 때는 남긴다). Vue 의 <c>normalize()</c> 와
    /// <b>같은 규칙</b>이어야 한다 — 다르면 DB 에 있는 권한이 조용히 안 걸린다.
    ///
    /// <para>
    /// [Blazor 라우트로 물어도 찾을 수 있어야 한다 — 실제로 밟았다]
    /// </para>
    ///
    /// <para>
    /// 열쇠는 DB 의 <c>path</c>(<c>/system/account</c>)인데 <c>PermissionView</c>
    /// 는 <b>지금 열려 있는 주소</b>(<c>/admin/system/account</c>)로 묻는다.
    /// 옛 경로를 쓰는 69개 화면에서 그 둘이 달라 아무것도 못 찾았고, 못 찾으면
    /// 「권한 없음」이라 <b>등록·수정·삭제·엑셀 단추가 말없이 전부 사라졌다.</b>
    /// 사이드바는 <c>MenuNode.Path</c> 로 물어서 멀쩡했기 때문에
    /// 「메뉴는 보이는데 화면 안 단추만 없다」로 나타났다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>여기서</b> 되돌린다. 판정이 이 한 곳뿐이므로 사이드바·진입
    /// 가드·화면 단추가 모두 같은 열쇠를 쓰게 된다. 들어오는 표(DB 경로)에는
    /// 아무 일도 하지 않는다 — 되돌릴 것이 없다(멱등).
    /// </para>
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
