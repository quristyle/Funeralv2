using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>메뉴 제목의 다국어를 서버에서 한 번에 붙인다. 저장된 제목 대부분이 번역 키가 아니라 완성된 글자라, 화면이 제목마다 <c>$t()</c> 를 부르면 키 없음 경고가 쏟아지므로 서버가 옮긴다.</summary>
public static class MenuTitleTranslator
{
    /// <summary>제목을 옮길 언어를 못 받았을 때 쓰는 기본 언어.</summary>
    public const string DefaultLocale = "ko";

    /// <summary>메뉴 제목에 해당하는 번역만 사전으로 읽어 온다. 실제로 쓰이는 제목만 IN 으로 좁히고, 언어를 못 찾으면 ko 로 한 번 더 본다 — 키가 그대로 뜨는 것보다 다른 언어 글자라도 보이는 편이 낫다.</summary>
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

    /// <summary>저장된 제목을 화면에 찍을 글자로 옮긴다. 사전에 있으면 옮긴 글자를, 없으면 null 을 준다. 키가 아닌 제목도 null 이다 — 옮길 것이 없으니 화면이 저장된 글자를 그대로 쓴다.</summary>
    public static string? Resolve(string? title, Dictionary<string, string> titles)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        return titles.TryGetValue(title, out var text) ? text : null;
    }

    /// <summary>메뉴 하나를 사람이 읽는 이름으로 줄인다 — 옮긴 제목 → 저장된 제목 → name. 이 순서가 화면의 규칙이라, 한 응답만 name 을 그대로 주면 메뉴마다 이름이 달라진다.</summary>
    public static string Display(SystemMenu menu, Dictionary<string, string> titles)
    {
        var text = Resolve(menu.Title, titles);
        if (!string.IsNullOrWhiteSpace(text)) return text!;

        return string.IsNullOrWhiteSpace(menu.Title) ? menu.Name : menu.Title!.Trim();
    }
}
