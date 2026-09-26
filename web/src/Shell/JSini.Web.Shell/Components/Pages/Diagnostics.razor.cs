using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using JSini.Web.Shell.Routing;

namespace JSini.Web.Shell.Components.Pages;

public partial class Diagnostics
{
    [Inject] private IReadOnlyList<PortalApp> Expected { get; set; } = default!;
    [Inject] private IReadOnlyList<IPortalModule> Loaded { get; set; } = default!;
    [Inject] private RouteInventory Routes { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;

    private sealed record Row(
        string Key,
        string DisplayName,
        string RoutePrefix,
        int RouteCount,
        string Status);

    private IReadOnlyList<Row> Rows => BuildRows();

    private List<Row> BuildRows()
    {
        var rows = new List<Row>();

        foreach (var expected in Expected)
        {
            var module = Loaded.FirstOrDefault(m =>
                string.Equals(m.Key, expected.Key, StringComparison.OrdinalIgnoreCase));

            var status = module is null ? "❌ 안 붙음"
                : module.RoutePrefix != expected.RoutePrefix ? "⚠ 접두사 어긋남"
                : "✅ 정상";

            rows.Add(new Row(
                expected.Key,
                expected.DisplayName,
                module?.RoutePrefix ?? expected.RoutePrefix,
                CountRoutes(expected.RoutePrefix),
                status));
        }

        // 설정에 없는데 붙어 있는 모듈. 있으면 안 되는 상태는 아니지만
        // (누가 참조만 넣고 설정을 잊었을 수 있다) 보이지 않으면 안 된다.
        foreach (var module in Loaded)
        {
            if (!Expected.Any(e => string.Equals(e.Key, module.Key, StringComparison.OrdinalIgnoreCase)))
            {
                rows.Add(new Row(
                    module.Key,
                    module.DisplayName,
                    module.RoutePrefix,
                    CountRoutes(module.RoutePrefix),
                    "⚠ 설정에 없음"));
            }
        }

        return rows;
    }

    private int CountRoutes(string prefix) => Routes.Paths.Count(p =>
        p.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(p, prefix, StringComparison.OrdinalIgnoreCase));
}
