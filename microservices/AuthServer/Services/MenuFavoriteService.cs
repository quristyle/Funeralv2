using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>사용자별 즐겨찾기 메뉴 서비스.</summary>
public interface IMenuFavoriteService
{
    /// <summary>로그인한 사용자의 즐겨찾기 목록. 순서대로 돌려준다.</summary>
    Task<List<MenuFavoriteDto>> GetFavoritesAsync(string loginId);

    /// <summary>경로의 메뉴를 즐겨찾기에 담는다. 이미 있으면 그대로 두고 지금 목록을 돌려준다.</summary>
    /// <returns>갱신된 즐겨찾기 목록</returns>
    /// <exception cref="KeyNotFoundException">계정이나 메뉴를 찾지 못한 경우</exception>
    Task<List<MenuFavoriteDto>> AddFavoriteAsync(string loginId, string path);

    /// <summary>경로에 해당하는 메뉴를 즐겨찾기에서 뺀다. 없으면 아무 일도 하지 않는다.</summary>
    /// <returns>갱신된 즐겨찾기 목록</returns>
    Task<List<MenuFavoriteDto>> RemoveFavoriteAsync(string loginId, string path);

    /// <summary>즐겨찾기 순서를 경로 목록 순서대로 다시 매긴다. 목록에 없는 것은 지금 순서를 지키며 뒤로 민다.</summary>
    /// <returns>갱신된 즐겨찾기 목록</returns>
    Task<List<MenuFavoriteDto>> ReorderFavoritesAsync(string loginId, List<string> paths);
}

/// <inheritdoc />
public class MenuFavoriteService : IMenuFavoriteService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MenuFavoriteService> _logger;

    /// <summary>서비스를 생성한다.</summary>
    public MenuFavoriteService(AppDbContext context, ILogger<MenuFavoriteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<MenuFavoriteDto>> GetFavoritesAsync(string loginId)
    {
        var accountId = await ResolveAccountIdAsync(loginId);
        if (accountId is null) return new List<MenuFavoriteDto>();

        return await LoadAsync(accountId);
    }

    /// <inheritdoc />
    public async Task<List<MenuFavoriteDto>> AddFavoriteAsync(string loginId, string path)
    {
        var accountId = await ResolveAccountIdAsync(loginId)
            ?? throw new KeyNotFoundException($"계정 '{loginId}' 을 찾을 수 없습니다.");

        var menu = await FindMenuByPathAsync(path)
            ?? throw new KeyNotFoundException($"경로 '{path}' 에 해당하는 메뉴가 없습니다.");

        // 화면이 없는 묶음(CATALOG)은 담아도 열 수 없다. 담기 전에 거른다.
        if (string.Equals(menu.Type, "CATALOG", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("하위 메뉴를 묶는 항목은 즐겨찾기에 담을 수 없습니다.");
        }

        var exists = await _context.MenuFavorites
            .AnyAsync(f => f.AccountId == accountId && f.MenuId == menu.Id);

        if (!exists)
        {
            // 맨 뒤에 붙인다. 방금 담은 것이 아래에 오는 편이 순서를 예측하기 쉽다.
            var lastOrder = await _context.MenuFavorites
                .Where(f => f.AccountId == accountId)
                .MaxAsync(f => (int?)f.SortOrder) ?? -1;

            _context.MenuFavorites.Add(new MenuFavorite
            {
                AccountId = accountId,
                MenuId = menu.Id,
                SortOrder = lastOrder + 1,
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("즐겨찾기 추가: {LoginId} → {Path} ({MenuId})", loginId, path, menu.Id);
        }

        return await LoadAsync(accountId);
    }

    /// <inheritdoc />
    public async Task<List<MenuFavoriteDto>> RemoveFavoriteAsync(string loginId, string path)
    {
        var accountId = await ResolveAccountIdAsync(loginId);
        if (accountId is null) return new List<MenuFavoriteDto>();

        var menu = await FindMenuByPathAsync(path);
        if (menu is null) return await LoadAsync(accountId);

        var favorite = await _context.MenuFavorites
            .FirstOrDefaultAsync(f => f.AccountId == accountId && f.MenuId == menu.Id);

        if (favorite is not null)
        {
            _context.MenuFavorites.Remove(favorite);
            await _context.SaveChangesAsync();
            _logger.LogInformation("즐겨찾기 해제: {LoginId} → {Path} ({MenuId})", loginId, path, menu.Id);
        }

        return await LoadAsync(accountId);
    }

    /// <inheritdoc />
    public async Task<List<MenuFavoriteDto>> ReorderFavoritesAsync(string loginId, List<string> paths)
    {
        var accountId = await ResolveAccountIdAsync(loginId);
        if (accountId is null) return new List<MenuFavoriteDto>();

        var favorites = await _context.MenuFavorites
            .Where(f => f.AccountId == accountId)
            .ToListAsync();
        if (favorites.Count == 0) return new List<MenuFavoriteDto>();

        // 메뉴 경로 → 원하는 순번. 경로 정리는 FindMenuByPathAsync 와 같은 규칙.
        var wanted = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < paths.Count; i++)
        {
            var clean = paths[i].Split('?')[0].Split('#')[0].TrimEnd('/');
            if (clean.Length == 0) clean = "/";
            wanted.TryAdd(clean, i);
        }

        var menuPaths = await _context.SystemMenus
            .AsNoTracking()
            .Where(m => favorites.Select(f => f.MenuId).Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Path);

        // 목록에 있는 것은 그 순번으로, 없는 것은 지금 순서를 지키며 뒤에 붙인다.
        var ordered = favorites
            .OrderBy(f => menuPaths.TryGetValue(f.MenuId, out var p) && wanted.TryGetValue(p, out var w)
                ? w : int.MaxValue - favorites.Count + favorites.IndexOf(f))
            .ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("즐겨찾기 순서 변경: {LoginId} ({Count}건)", loginId, ordered.Count);
        return await LoadAsync(accountId);
    }

    /// <summary>즐겨찾기 목록을 메뉴 정보와 함께 읽는다. 비활성 메뉴는 내려보내지 않되 즐겨찾기 자체는 지우지 않는다.</summary>
    private async Task<List<MenuFavoriteDto>> LoadAsync(string accountId)
    {
        var rows = await _context.MenuFavorites
            .AsNoTracking()
            .Where(f => f.AccountId == accountId)
            .Join(_context.SystemMenus.Where(m => m.Status == 1),
                  f => f.MenuId, m => m.Id,
                  (f, m) => new { Menu = m, f.SortOrder })
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        // 제목 다국어는 여기서도 붙인다. 메뉴를 내려보내는 세 번째 자리라 옮기는 코드는 MenuTitleTranslator 한 벌만 둔다.
        var titles = await MenuTitleTranslator.LoadAsync(
            _context, [.. rows.Select(x => x.Menu)], locale: null);

        return
        [
            .. rows.Select(x => new MenuFavoriteDto
            {
                MenuId = x.Menu.Id,
                Path = x.Menu.Path,
                Name = x.Menu.Name,
                Title = MenuTitleTranslator.Resolve(x.Menu.Title, titles)
                        ?? x.Menu.Title
                        ?? x.Menu.Name,
                Icon = x.Menu.Icon,
                SortOrder = x.SortOrder,
            })
        ];
    }

    /// <summary>로그인 아이디로 계정 식별자를 찾는다. X-User-Id 는 로그인 아이디라 그대로 찾는다.</summary>
    private async Task<string?> ResolveAccountIdAsync(string loginId)
    {
        if (string.IsNullOrWhiteSpace(loginId)) return null;

        return await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == loginId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>경로로 메뉴를 찾는다. 경로는 system_menus 에서 유일하다(메뉴 관리 화면이 중복을 막는다).</summary>
    private async Task<SystemMenu?> FindMenuByPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // 탭의 fullPath 에는 조회 조건이 붙어 올 수 있다(`/a/b?id=3`). 경로만 떼어 찾는다.
        var clean = path.Split('?')[0].Split('#')[0].TrimEnd('/');
        if (clean.Length == 0) clean = "/";

        return await _context.SystemMenus
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Path == clean);
    }
}
