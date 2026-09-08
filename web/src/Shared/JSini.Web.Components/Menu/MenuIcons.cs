namespace JSini.Web.Components.Menu;

/// <summary>
/// DB 의 메뉴 아이콘 이름을 CSS 클래스로 옮긴다.
///
/// <para>
/// [DB 에 있는 값은 iconify 이름이다]
/// </para>
///
/// <para>
/// <c>scom.system_menus.icon</c> 에 <c>lucide:calendar-days</c> ·
/// <c>carbon:building</c> 처럼 들어 있다(179건 중 178건에 값이 있고 139가지다).
/// 옛 Vue 포털은 iconify 런타임이 그 이름으로 SVG 를 받아 그렸다.
/// </para>
///
/// <para>
/// [런타임을 들이지 않고 CSS 로 굳혔다]
/// </para>
///
/// <para>
/// 필요한 139가지를 <c>menu-icons.css</c> 에 mask 로 박아 두었다
/// (<c>scripts/build-menu-icons.py</c> 가 만든다). 그래서 아이콘 때문에
/// 바깥으로 나가는 요청이 없고, 색이 <c>currentColor</c> 라 테마 스물둘을
/// 그대로 따라온다 — app.css 의 아이콘 묶음과 같은 방식이다.
/// </para>
///
/// <para>
/// [모르는 이름은 동그라미가 된다]
/// </para>
///
/// <para>
/// 메뉴 관리 화면에서 CSS 에 없는 이름을 넣을 수 있다. 그때 클래스는 붙지만
/// <c>--svg</c> 가 없어서 <c>.jsini-mi</c> 의 기본값(동그라미)이 나온다.
/// <b>여기서 이름 목록을 들고 판정하지 않는 이유가 그것이다</b> — 목록을
/// 코드에 두면 CSS 를 다시 만들 때 두 곳을 맞춰야 하고, 어긋나면 아이콘이
/// 사라지는 쪽으로 틀린다.
/// </para>
/// </summary>
public static class MenuIcons
{
    /// <summary>크기·색·mask 규칙을 갖는 클래스. 그림이 없으면 이것만 붙는다.</summary>
    public const string BaseClass = "jsini-mi";

    /// <summary>
    /// <paramref name="icon"/> 에 맞는 클래스 목록.
    /// 값이 없거나 이름 꼴이 아니면 <see cref="BaseClass"/> 만 돌려준다.
    /// </summary>
    public static string CssClass(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return BaseClass;
        }

        var name = icon.Trim().ToLowerInvariant();

        // 아이콘 이름에 쓰이는 글자만 받는다. DB 를 사람이 고치는 자리라
        // (메뉴 관리 화면) 엉뚱한 값이 들어올 수 있고, 그것을 클래스 이름에
        // 그대로 붙이면 선택자가 깨지거나 옆 클래스를 하나 더 켜게 된다.
        foreach (var c in name)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or ':'))
            {
                return BaseClass;
            }
        }

        return $"{BaseClass} {BaseClass}--{name.Replace(':', '-')}";
    }
}
