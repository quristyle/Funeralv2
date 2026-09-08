using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Security;

/// <summary>
/// 권한을 따질 때 쓰는 화면 경로.
///
/// <para>
/// <b>왜 부품 밖으로 냈는가</b> — 권한 판정에는 경로가 필요한데, 그 경로를
/// 꺼내는 규칙(쿼리스트링·앵커를 떼고 앞에 <c>/</c> 를 붙인다)이 화면마다
/// 다시 적히면 조용히 갈라진다. 갈라지는 순간 <c>PermissionView</c> 가 감춘
/// 단추와 화면이 스스로 판정한 결과가 어긋나고, 그때 나는 사고가
/// <b>「단추는 없는데 끌어 옮기기는 된다」</b> 같은 모양이다.
/// </para>
/// </summary>
public static class PermissionPath
{
    /// <summary>
    /// 지금 열려 있는 화면의 경로. 쿼리스트링과 앵커는 떼어 낸다 —
    /// 권한표의 열쇠는 <c>scom.system_menus.path</c> 이고 거기에는 그런 것이 없다.
    /// </summary>
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
