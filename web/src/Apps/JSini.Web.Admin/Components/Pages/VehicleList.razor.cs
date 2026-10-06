using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

/// <summary>계정 고르개 한 줄 — 아이디와 이름을 한 글자로 묶어 보여 준다.</summary>
public sealed record AccountPick(string LoginId, string Text);

public partial class VehicleList
{
    [Inject] private CargoVehicleClient Vehicles { get; set; } = default!;
    [Inject] private AdminClient Api { get; set; } = default!;

    private IReadOnlyList<CargoVehicleDto> _vehicles = [];
    private IReadOnlyList<AccountPick> _accounts = [];

    private CargoVehicleForm _form = new();
    private bool _editing;
    private string? _keyword;

    private PlateReadDto? _plate;
    private string? _readFor;

    private string PlateText => _plate is null
        ? "차량번호를 적으면 화물차인지 · 사업용인지를 번호판에서 읽어 채웁니다."
        : _plate.Summary;

    /// <summary>못 읽은 것은 흐리게. 읽었어도 「사업용이 아니다」면 짚어 준다.</summary>
    private string PlateToneClass => _plate switch
    {
        null => "ad-plate ad-plate--idle",
        { Readable: false } => "ad-plate ad-plate--idle",
        { IsBusiness: false } or { IsFreight: false } => "ad-plate ad-plate--warn",
        _ => "ad-plate ad-plate--ok",
    };

    private string ConditionSummary => SchSummary.Of(SchSummary.Or(_keyword));

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _vehicles = await Vehicles.GetVehiclesAsync(_keyword);
        return _vehicles.Count;
    }, "등록된 차량이 없습니다.", "차량을 읽지 못했습니다");

    private Task Reset()
    {
        _keyword = null;
        return ReloadAsync();
    }

    private void Open(CargoVehicleDto? vehicle)
    {
        _form = vehicle is null ? new CargoVehicleForm() : CargoVehicleForm.From(vehicle);
        _plate = vehicle?.Plate;
        _readFor = vehicle?.PlateNo;
        _editing = true;

        // 계정 목록은 **등록할 때만** 필요하다. 목록 화면을 열자마자 읽으면
        // 차량을 보기만 하러 온 사람에게도 계정 전부를 읽히게 된다.
        if (_form.IsNew && _accounts.Count == 0)
        {
            _ = LoadAccountsAsync();
        }
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await Api.GetAccountsAsync();
            _accounts =
            [
                .. accounts
                    .Where(a => !string.Equals(a.Status, "RESIGNED", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.UserName)
                    .Select(a => new AccountPick(a.LoginId, $"{a.UserName} ({a.LoginId})")),
            ];
            StateHasChanged();
        }
        catch (JSini.Web.Http.ApiException ex)
        {
            // 계정을 못 읽어도 창은 떠 있다 — 아이디를 아는 사람은 직접 적을 수 있게
            // 남겨 두는 편이 창을 닫아 버리는 것보다 낫다.
            Say($"계정 목록을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private async Task OnPlateChangedAsync(string? value)
    {
        _form.PlateNo = value ?? string.Empty;
        await ReadPlateAsync();
    }

    /// <summary>
    /// 번호판을 읽어 칸을 채운다.
    ///
    /// <para>
    /// 읽은 값은 <b>등록할 때만</b> 칸에 넣는다. 수정 중에도 밀어 버리면,
    /// 번호판과 다른 차를 적어 둔 사람이(자가용 번호판에 사업용 허가를 받은 차 같은)
    /// 매번 되돌려야 한다.
    /// </para>
    /// </summary>
    private async Task ReadPlateAsync()
    {
        var plate = (_form.PlateNo ?? string.Empty).Trim();
        if (plate.Length == 0)
        {
            _plate = null;
            _readFor = null;
            return;
        }

        if (plate == _readFor) return;
        _readFor = plate;

        try
        {
            _plate = await Vehicles.ReadPlateAsync(plate);
        }
        catch (JSini.Web.Http.ApiException)
        {
            // 못 읽어도 폼은 그대로 쓴다. 번호판 해석은 거드는 것이지 막는 것이 아니다.
            _plate = null;
            return;
        }

        if (_plate is null || !_form.IsNew) return;

        if (_plate.SuggestedClass is { } cls) _form.VehicleClass = cls;
        if (_plate.IsBusiness is { } business) _form.IsBusiness = business;
    }

    private async Task SaveAsync()
    {
        if (_form.Problem() is { } problem)
        {
            Say(problem, NoticeTone.Warning);
            return;
        }

        // 고른 계정의 이름을 함께 보낸다 — 그 사람의 운송관리 줄이 아직 없으면
        // 서버가 이 이름으로 만든다.
        if (_form.IsNew)
        {
            _form.UserName = _accounts.FirstOrDefault(a => a.LoginId == _form.LoginId)?.Text;
        }

        var request = _form.ToRequest();
        var ok = _form.VehicleId is { } id
            ? await RunAsync(() => Vehicles.UpdateVehicleAsync(id, request), "차량을 고쳤습니다.", "차량을 고치지 못했습니다")
            : await RunAsync(() => Vehicles.CreateVehicleAsync(request), "차량을 등록했습니다.", "차량을 등록하지 못했습니다");

        if (ok)
        {
            _editing = false;
            await ReloadAsync();
        }
    }

    private async Task DeleteAsync(CargoVehicleDto vehicle)
    {
        if (await RunAsync(() => Vehicles.DeleteVehicleAsync(vehicle.VehicleId),
                "차량을 지웠습니다.", "차량을 지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
