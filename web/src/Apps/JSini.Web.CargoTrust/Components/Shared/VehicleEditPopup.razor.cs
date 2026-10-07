using JSini.Web.CargoTrust.Api;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class VehicleEditPopup
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public EventCallback OnSaved { get; set; }

    private bool _visible;
    private VehicleDraft? _draft;

    /// <summary><paramref name="vehicle"/> 가 null 이면 등록이다.</summary>
    public void Open(VehicleInfo? vehicle)
    {
        _draft = vehicle is null ? new VehicleDraft() : VehicleDraft.From(vehicle);
        _visible = true;
        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (_draft is null) return;

        if (_draft.Problem() is { } problem)
        {
            Say(problem, NoticeTone.Warning);
            return;
        }

        var request = _draft.ToRequest();
        var ok = _draft.VehicleId is { } id
            ? await RunAsync(() => Api.UpdateVehicleAsync(id, request), "차량을 고쳤습니다.", "차량을 고치지 못했습니다")
            : await RunAsync(() => Api.CreateVehicleAsync(request), "차량을 등록했습니다.", "차량을 등록하지 못했습니다");

        if (ok)
        {
            _visible = false;
            await OnSaved.InvokeAsync();
        }
    }
}
