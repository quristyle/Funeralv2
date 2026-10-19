using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Security;

/// <summary>권한을 따질 때 쓰는 화면 경로. 경로를 꺼내는 규칙(쿼리·앵커를 떼고 / 를 붙인다)이 화면마다 갈라지면 감춘 단추와 화면의 판정이 어긋나므로 여기 한 곳에 둔다.</summary>
public static class PermissionPath
{
    /// <summary>지금 열려 있는 화면의 경로. 쿼리와 앵커는 뗀다. 권한표의 열쇠(scom.system_menus.path)에는 그런 것이 없다.</summary>
    public static string Current(NavigationManager navigation)
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var cut = relative.IndexOfAny(['?', '#']);

        if (cut >= 0)
        {
            relative = relative[..cut];
        }

        return "/" + relative.TrimStart('/');
    }
}
