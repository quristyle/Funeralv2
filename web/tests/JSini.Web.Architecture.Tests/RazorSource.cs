namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면 소스를 읽는다. <c>X.razor</c> 를 읽으면 옆의 코드비하인드
/// <c>X.razor.cs</c> 를 뒤에 붙여 돌려준다.
/// </summary>
/// <remarks>
/// 화면은 마크업(<c>.razor</c>)과 코드(<c>.razor.cs</c>)로 나뉘어 있다. 글자로
/// 검사하는 테스트는 「그 화면이 무엇을 하는가」를 보려는 것이라 둘을 한 덩어리로
/// 읽어야 한다 — razor 만 읽으면 코드 쪽에 있는 것을 「없다」고 판정한다.
/// razor 가 아닌 파일은 그대로 읽는다.
/// </remarks>
internal static class RazorSource
{
    public static string Read(string path)
    {
        var text = File.ReadAllText(path);

        if (!path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var codeBehind = path + ".cs";

        return File.Exists(codeBehind)
            ? text + "\n" + File.ReadAllText(codeBehind)
            : text;
    }

    public static string[] ReadLines(string path) =>
        Read(path).Replace("\r\n", "\n").Split('\n');
}
