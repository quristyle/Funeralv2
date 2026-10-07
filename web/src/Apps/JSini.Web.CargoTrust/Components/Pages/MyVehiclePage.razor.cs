using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class MyVehiclePage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    private IReadOnlyList<VehicleInfo> _vehicles = [];
    private VehicleEditPopup? _popup;


    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _vehicles = await Api.GetMyVehiclesAsync();
        return _vehicles.Count;
    }, "등록한 차량이 없습니다. 「차량 등록」으로 한 대 넣으십시오.", "차량을 읽지 못했습니다");

    private void Open(VehicleInfo? vehicle) => _popup?.Open(vehicle);

    private async Task DeleteAsync(VehicleInfo vehicle)
    {
        // 지워도 계산 이력은 남는다 — 서버가 표시만 지운다.
        if (await RunAsync(() => Api.DeleteVehicleAsync(vehicle.VehicleId),
                "차량을 지웠습니다.", "차량을 지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
