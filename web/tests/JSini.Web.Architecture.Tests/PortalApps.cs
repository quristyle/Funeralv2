using System.Reflection;
using System.Text.Json;
using JSini.Web.Abstractions;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 검사 대상을 모아 둔다.
///
/// 셸은 업무 앱을 ProjectReference 로 싣지만 <b>타입을 이름으로 쓰지 않는다</b> —
/// 어셈블리를 훑어 IPortalModule 로 찾을 뿐이다. 그래서 C# 컴파일러가 셸의
/// 메타데이터에 업무 어셈블리 참조를 남기지 않고, 셸을 뒤져서는 앱 목록을
/// 얻을 수 없다. 대신 테스트 프로젝트가 업무 앱을 모두 직접 참조하므로 자기
/// 출력 폴더에 전부 복사돼 있다 — 그것을 훑는다.
///
/// 앱 이름을 코드에 나열하지 않는 것이 요점이다. 나열하면 새 앱을 붙일 때
/// 여기 추가하는 것을 잊고, 그러면 새 앱만 규칙 검사를 안 받는다.
/// </summary>
internal static class PortalApps
{
    internal const string ShellAssembly = "JSini.Web.Shell";

    /// <summary>공통 라이브러리. 업무 앱이 참조해도 되는 것들이다.</summary>
    internal static readonly string[] Shared =
    [
        "JSini.Web.Abstractions",
        "JSini.Web.Components",
        "JSini.Web.Http",
        "JSini.Web.Models",
    ];

    /// <summary>업무 MFE 어셈블리들.</summary>
    internal static IReadOnlyList<Assembly> Assemblies { get; } = Load();

    /// <summary>각 앱이 자기를 설명하는 <see cref="IPortalModule"/> 구현.</summary>
    internal static IReadOnlyList<IPortalModule> Descriptors { get; } =
    [
        .. Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IPortalModule).IsAssignableFrom(t))
            .Select(t => (IPortalModule)Activator.CreateInstance(t)!)
    ];

    /// <summary>
    /// 셸의 appsettings 에 적힌 업무 MFE 표.
    /// 앱 쪽 <see cref="IPortalModule"/> 와 대조하기 위해 읽는다.
    /// </summary>
    internal static IReadOnlyList<ShellAppEntry> ShellRegistry { get; } = LoadShellRegistry();

    private static List<Assembly> Load()
    {
        var found = new List<Assembly>();

        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "JSini.Web.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            if (name == ShellAssembly || Shared.Contains(name) || name.EndsWith(".Tests"))
            {
                continue;
            }

            found.Add(Assembly.LoadFrom(file));
        }

        return found;
    }

    private static List<ShellAppEntry> LoadShellRegistry()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shell.appsettings.json");
        if (!File.Exists(path))
        {
            return [];
        }

        // 셸의 appsettings 에는 주석이 있다(설정 파일에 왜 그런지를 적어 둔다).
        // System.Text.Json 은 기본적으로 주석에서 터지므로 허용해 준다.
        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };

        using var document = JsonDocument.Parse(RazorSource.Read(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (!document.RootElement.TryGetProperty("PortalApps", out var apps))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ShellAppEntry>>(apps.GetRawText(), options) ?? [];
    }
}

/// <summary>셸 appsettings 의 <c>PortalApps</c> 한 줄.</summary>
internal sealed class ShellAppEntry
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoutePrefix { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
