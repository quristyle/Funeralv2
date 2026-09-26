using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class HomePage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    private HomeInfo? _home;

    /// <summary>
    /// 검색칸. 라벨이 없는 칸이라 자리표시 글이 라벨 노릇을 한다(web/CLAUDE.md
    /// 「라벨을 붙인 칸에는 자리표시 글을 두지 않는다」의 예외 쪽).
    /// </summary>
    private string? _q;

    protected override Task OnInitializedAsync() => LoadOneAsync(
        () => Api.GetHomeAsync(),
        h => _home = h,
        emptyMessage: string.Empty,
        failMessage: "홈을 읽지 못했습니다");

    private async Task OnSearchKeyAsync(KeyboardEventArgs e)
    {
        // 한 칸짜리라 Enter 의 뜻이 「검색」 하나뿐이다.
        if (e.Key == "Enter")
        {
            await Task.Yield();
            Search();
        }
    }

    private void Search()
    {
        var q = _q?.Trim();
        Navigation.NavigateTo(string.IsNullOrEmpty(q)
            ? "/cargotrust/companies"
            : $"/cargotrust/companies?q={Uri.EscapeDataString(q)}");
    }
}
