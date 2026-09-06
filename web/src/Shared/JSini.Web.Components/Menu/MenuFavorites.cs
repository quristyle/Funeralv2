using JSini.Web.Abstractions;
using JSini.Web.Http;

namespace JSini.Web.Components.Menu;

/// <summary>
/// 메뉴 즐겨찾기. 게이트웨이의 <c>/auth/menu/favorites</c> 로 나간다.
///
/// [백엔드에 남아 있던 기능이다]
///
/// vben 포털에는 탭 오른쪽 메뉴에 "즐겨찾기 추가" 가 있었고 사이드바 맨 위에
/// 즐겨찾기 묶음이 붙었다. 프론트를 갈아엎으면서 그 화면이 사라졌지만
/// <b>표도 API 도 그대로 살아 있다</b> — 쓰던 사람의 즐겨찾기가 DB 에 남아 있다.
///
/// [경로가 열쇠다]
///
/// 즐겨찾기는 메뉴 <c>path</c> 로 담긴다. 그래서 DB 의 옛 경로(<c>/room_status</c>)로
/// 쌓여 있고, 사이드바가 링크로 거는 것은 새 경로(<c>/funeral/room-status</c>)다.
/// 판정은 <b>DB 경로끼리</b> 해야 한다 — <c>MenuNode.Path</c> 로 묻고
/// <c>RouteAliases</c> 로 옮긴 값은 쓰지 않는다. 섞으면 이미 담아 둔 즐겨찾기가
/// 화면에서 안 담긴 것으로 보인다.
///
/// scoped 다. 사람마다 다른 목록이고 게이트웨이 토큰을 물고 있다.
/// </summary>
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
            Items = await gateway.GetListAsync<MenuFavorite>(Path, ct);
            IsLoaded = true;
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

    // ── 「고정탭 관리」 화면이 쓰는 셋 ──────────────────────────
    //
    // 위의 ToggleAsync 와 달리 **실패를 삼키지 않는다.** 헤더의 별은 눌러도
    // 아무 일이 없으면 다시 누르면 그만이지만, 관리 화면은 여러 건을 옮겨
    // 놓는 자리라 「저장된 줄 알았는데 아니었다」가 그대로 남는다.
    // 화면의 DataPage.RunAsync 가 ApiException 을 받아 이유를 띄운다.

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

    /// <summary>
    /// 순서를 다시 매긴다. <b>원하는 순서대로의 경로 목록을 통째로</b> 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「이것을 저기로」가 아니라 「전체가 이 순서다」로 보내는 이유는, 한 칸씩
    /// 주고받으면 중간에 실패했을 때 서버와 화면의 순서가 <b>서로 다른 채로</b>
    /// 남기 때문이다. 통째로 보내면 성공이면 같고 실패면 안 바뀐다.
    /// </para>
    /// <para>
    /// 경로는 <b>DB 에 저장된 그대로</b>다 — <c>RouteAliases</c> 로 옮긴 값을
    /// 보내면 서버가 그 메뉴를 못 찾는다.
    /// </para>
    /// </remarks>
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
