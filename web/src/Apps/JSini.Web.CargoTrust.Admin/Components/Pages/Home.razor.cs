using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class Home
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    private AdminDashboard? _board;

    private IReadOnlyList<AdminAuditEntry> Recent => _board?.RecentAudit ?? [];

    /// <summary>
    /// 결제 상태를 **코드표의 차례로** 늘어놓는다. 서버가 준 차례(건수순일 수도,
    /// 알파벳순일 수도 있다)를 따르면 새로 읽을 때마다 딱지 자리가 바뀌어
    /// 「미지급이 어디 있었나」를 매번 찾는다. 코드표에 없는 것은 뒤에 붙인다.
    /// </summary>
    private IEnumerable<AdminStatusCount> OrderedCounts
    {
        get
        {
            var order = CargoAdminCodes.PaymentStatus.All
                .Select((c, i) => (c.Code, i))
                .ToDictionary(x => x.Code, x => x.i, StringComparer.OrdinalIgnoreCase);

            return (_board?.StatusCounts ?? [])
                .OrderBy(r => order.TryGetValue(r.Status, out var i) ? i : int.MaxValue);
        }
    }

    /// <summary>할 일이 남은 타일만 색을 바꾼다. 0 인데 색이 있으면 늘 경보처럼 보여 무뎌진다.</summary>
    private static string DueClass(int count) =>
        count > 0 ? "jsini-stat ca-stat-link ca-stat--due" : "jsini-stat ca-stat-link";

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadOneAsync(
        () => Api.GetDashboardAsync(),
        board => _board = board,
        "대시보드 자료를 받지 못했습니다.",
        "대시보드를 읽지 못했습니다");
}
