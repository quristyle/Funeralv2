using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 메뉴 제목의 다국어를 <b>서버에서 한 번에</b> 붙인다.
///
/// [화면이 옮기지 않고 서버가 옮기는 이유]
///
/// 예전에는 화면이 제목마다 <c>$t()</c> 를 불렀다. 그런데 저장된 제목
/// 대부분(179건 중 162건)이 번역 키가 아니라 이미 완성된 글자라서, vue-i18n 이
/// "그런 키는 없다" 는 경고를 <b>한 번 새로 그릴 때마다 수백 줄</b> 쏟아냈다.
/// 옮길 것이 적은데도 화면이 늦게 뜬 이유가 이것이다.
///
/// 그래서 언어 하나에 해당하는 <c>scom.i18n_resources</c> 를 사전 하나로 읽어
/// (왕복 한 번) 제목을 맞춰 본다. 화면은 내려온 글자를 그대로 찍기만 한다.
///
/// [두 서비스가 같은 코드를 쓰게 뽑아 둔 것이다]
///
/// 메뉴를 내려보내는 곳이 둘이다 — 사이드바용 <c>MenuService</c>(<c>/menu/all</c>)와
/// 메뉴 관리 화면용 <c>SystemMenuService</c>. 한동안 <b>뒤엣것만</b> 번역을 붙였고,
/// 그래서 관리 화면에서는 "메뉴 관리" 로 보이는 항목이 사이드바에서는
/// <c>system.menu.title</c> 로 보였다. 복제해 두면 반드시 다시 갈라지므로
/// 여기 한 벌만 둔다.
/// </summary>
public static class MenuTitleTranslator
{
    /// <summary>제목을 옮길 언어를 못 받았을 때 쓰는 기본 언어.</summary>
    public const string DefaultLocale = "ko";

    /// <summary>
    /// 메뉴 제목에 해당하는 번역만 사전으로 읽어 온다.
    ///
    /// 표 전체를 읽지 않고 <b>실제로 쓰이는 제목</b>만 <c>IN</c> 으로 좁힌다
    /// (지금은 179건 중 키처럼 생긴 것 17건뿐이다).
    /// 언어를 못 찾으면 <c>ko</c> 로 한 번 더 본다 — 화면에 키가 그대로 뜨는 것보다
    /// 다른 언어의 글자라도 보이는 편이 관리에 낫다.
    /// </summary>
    /// <param name="db">DB 컨텍스트</param>
    /// <param name="menus">제목을 모을 메뉴들</param>
    /// <param name="locale">옮길 언어. 비우면 <c>ko</c>.</param>
    public static async Task<Dictionary<string, string>> LoadAsync(
        AppDbContext db, List<SystemMenu> menus, string? locale)
    {
        var wanted = menus
            .Select(m => m.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .Distinct()
            .ToList();

        if (wanted.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);

        var lang = string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale.Trim();

        var rows = await db.I18nResources
            .Where(r => wanted.Contains(r.Key)
                        && (r.Locale == lang || r.Locale == DefaultLocale)
                        && r.Value != "")
            .Select(r => new { r.Key, r.Locale, r.Value })
            .ToListAsync();

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        // 먼저 기본 언어를 깔고, 요청한 언어가 있으면 그 위에 덮는다.
        foreach (var row in rows.Where(r => r.Locale == DefaultLocale))
        {
            map[row.Key] = row.Value;
        }

        if (lang != DefaultLocale)
        {
            foreach (var row in rows.Where(r => r.Locale == lang))
            {
                map[row.Key] = row.Value;
            }
        }

        return map;
    }

    /// <summary>
    /// 저장된 제목을 화면에 찍을 글자로 옮긴다.
    ///
    /// 사전에 있으면 옮긴 글자를, 없으면 <c>null</c> 을 준다.
    /// <b>키가 아닌 제목(이미 완성된 글자)도 null 이다</b> — 옮길 것이 없으니
    /// 화면이 저장된 글자를 그대로 쓰면 된다.
    /// </summary>
    public static string? Resolve(string? title, Dictionary<string, string> titles)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        return titles.TryGetValue(title, out var text) ? text : null;
    }
}
