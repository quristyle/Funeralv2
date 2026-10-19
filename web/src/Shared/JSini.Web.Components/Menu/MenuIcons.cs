namespace JSini.Web.Components.Menu;

/// <summary>DB 의 메뉴 아이콘 이름(iconify 형식, 예 lucide:calendar-days)을 CSS 클래스로 옮긴다. 옛 포털은 iconify 런타임이 그 이름으로 SVG 를 받았지만, 여기서는 필요한 139가지를 menu-icons.css 에 mask 로 굳혔다(scripts/build-menu-icons.py). 아이콘 때문에 바깥으로 나가는 요청이 없고 색은 currentColor 라 테마를 따라온다.
/// 이름 목록을 C# 에 들고 판정하지 않는다 — 목록을 코드에 두면 CSS 를 다시 만들 때 두 곳을 맞춰야 하고, 어긋나면 아이콘이 사라진다. 모르는 이름은 기본 동그라미가 나온다.</summary>
public static class MenuIcons
{
    /// <summary>크기·색·mask 규칙을 갖는 클래스. 그림이 없으면 이것만 붙는다.</summary>
    public const string BaseClass = "jsini-mi";

    /// <summary>icon 에 맞는 클래스 목록. 값이 없거나 이름 꼴이 아니면 BaseClass 만 돌려준다.</summary>
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
