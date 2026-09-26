using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class MyInfoPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    private MeInfo? _me;

    protected override Task OnInitializedAsync() => LoadOneAsync(
        () => Api.GetMeAsync(),
        m => _me = m,
        "내 정보를 찾지 못했습니다.",
        "내 정보를 읽지 못했습니다");
}
