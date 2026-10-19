using JSini.Web.Abstractions;
using JSini.Web.Http;

namespace JSini.Web.Components.Menu;

/// <summary>메뉴 즐겨찾기. 게이트웨이의 auth/menu/favorites 로 나간다. 백엔드에 표와 API 가 남아 있어 쓰던 사람의 즐겨찾기가 DB 에 그대로 있다.
/// 즐겨찾기는 메뉴 path 로 담긴다 — 판정은 DB 경로끼리 한다(MenuNode.Path). RouteAliases 로 옮긴 링크 주소를 쓰면 이미 담아 둔 것이 안 담긴 것으로 보인다. scoped — 사람마다 다른 목록이고 게이트웨이 토큰을 물고 있다.</summary>
public sealed class MenuFavorites(GatewayClient gateway, ILogger<MenuFavorites> logger)
{
    private const string Path = "auth/menu/favorites";

    /// <summary>담아 둔 것들. 서버가 준 순서를 지킨다.</summary>
    public IReadOnlyList<MenuFavorite> Items { get; private set; } = [];

    /// <summary>한 번이라도 읽었는가. 안 읽었으면 별 표시를 그리지 않는다.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>목록이 바뀌었다. 사이드바가 다시 그린다.</summary>
    public event Action? Changed;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        try
        {
            Apply(await gateway.GetListAsync<MenuFavorite>(Path, ct));
            return;
        }
        catch (ApiException ex)
        {
            // 즐겨찾기를 못 읽어도 포털은 돌아야 한다. 사이드바에서 묶음 하나가
            // 빠질 뿐이다.
            logger.LogWarning(ex, "즐겨찾기를 읽지 못했다.");
            Items = [];
        }

        Changed?.Invoke();
    }

    /// <summary>이미 받아 둔 목록을 채운다. 부트스트랩이 쓰는 길이라 게이트웨이를 다시 부르지 않는다.</summary>
    public void Apply(IReadOnlyList<MenuFavorite> items)
    {
        Items = items;
        IsLoaded = true;
        Changed?.Invoke();
    }

    /// <summary>이 경로가 담겨 있는가. <b>DB 경로로 묻는다</b>(위 주석 참고).</summary>
    public bool Contains(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Items.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>담거나 뺀다. 이미 담겨 있으면 뺀다.</summary>
    /// <returns>바꾼 뒤의 상태. 담겼으면 <c>true</c>.</returns>
    public async Task<bool> ToggleAsync(string path, CancellationToken ct = default)
    {
        var wasFavorite = Contains(path);

        try
        {
            // 서버가 바뀐 목록을 통째로 돌려준다. 우리가 손으로 더하고 빼지
            // 않는다 — 정렬 순서를 서버가 정하므로 손으로 맞추면 어긋난다.
            Items = wasFavorite
                ? await gateway.DeleteListAsync<MenuFavorite>(
                    $"{Path}?path={Uri.EscapeDataString(path)}", ct)
                : await gateway.PostListAsync<MenuFavorite>(Path, new { path }, ct);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "즐겨찾기를 바꾸지 못했다: {Path}", path);
            Changed?.Invoke();
            return wasFavorite;
        }

        Changed?.Invoke();
        return !wasFavorite;
    }

    // 고정탭 관리 화면이 쓰는 셋은 실패를 삼키지 않는다 — 여러 건을 놓는 자리라 저장된 줄 알았는데 아니었다가 남으면 안 된다. 헤더의 별은 다시 누르면 그만이라 삼킨다. 화면의 DataPage.RunAsync 가 ApiException 을 받아 이유를 띄운다.

    /// <summary>담는다. 이미 담겨 있으면 서버가 그대로 둔다.</summary>
    public async Task AddAsync(string path, CancellationToken ct = default)
    {
        Items = await gateway.PostListAsync<MenuFavorite>(Path, new { path }, ct);
        Changed?.Invoke();
    }

    /// <summary>뺀다. 담겨 있지 않아도 오류가 아니다.</summary>
    public async Task RemoveAsync(string path, CancellationToken ct = default)
    {
        Items = await gateway.DeleteListAsync<MenuFavorite>(
            $"{Path}?path={Uri.EscapeDataString(path)}", ct);
        Changed?.Invoke();
    }

    /// <summary>순서를 다시 매긴다. 원하는 순서대로의 경로 목록을 통째로 보낸다 — 「이것을 저기로」로 보내면 중간에 실패했을 때 서버와 화면의 순서가 서로 달라진다. 통째로 보내면 성공이면 같고 실패면 안 바뀐다. 경로는 DB 에 저장된 그대로다 — RouteAliases 로 옮긴 값을 보내면 서버가 못 찾는다.</summary>
    public async Task ReorderAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        Items = await gateway.PutListAsync<MenuFavorite>(
            $"{Path}/order", new { paths = paths.ToList() }, ct);
        Changed?.Invoke();
    }
}

/// <summary>담아 둔 메뉴 하나. AuthServer 의 <c>MenuFavoriteDto</c> 와 짝이다.</summary>
public sealed class MenuFavorite
{
    public string MenuId { get; set; } = string.Empty;

    /// <summary>메뉴 경로. <b>DB 에 저장된 그대로</b>다 — 옛 경로일 수 있다.</summary>
    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }

    /// <summary>사이드바에 보일 이름. 제목이 없으면 메뉴 이름.</summary>
    public string Label => string.IsNullOrWhiteSpace(Title) ? Name : Title;

    /// <summary>실제로 걸 링크. 옛 경로를 새 경로로 옮긴다.</summary>
    public string Href => RouteAliases.Resolve(Path);
}
