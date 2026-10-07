using JSini.Web.CargoTrust.Api;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class VehicleFields
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>부모가 가진 폼을 그대로 고친다. 저장은 부르는 쪽이 한다.</summary>
    [Parameter, EditorRequired] public VehicleDraft Draft { get; set; } = default!;

    private PlateReadInfo? _plate;
    private string? _readFor;

    /// <summary>
    /// 축수 고르개. <b>「모름」이 첫 줄이다</b> — 안 고르면 모르는 것이지,
    /// 0축인 차가 있는 게 아니다.
    /// </summary>
    private static readonly IReadOnlyList<SchOption> AxleOptions =
    [
        new("0", "모름"),
        new("2", "2축"),
        new("3", "3축"),
        new("4", "4축"),
        new("5", "5축"),
        new("6", "6축 이상"),
    ];

    private string PlateText => _plate is null
        ? "차량번호를 적으면 화물차인지 · 사업용인지를 번호판에서 읽어 채웁니다."
        : _plate.Summary;

    /// <summary>못 읽은 것은 흐리게. 읽었어도 「사업용이 아니다」면 짚어 준다.</summary>
    private string PlateToneClass => _plate switch
    {
        null => "ct-plate ct-plate--idle",
        { Readable: false } => "ct-plate ct-plate--idle",
        { IsBusiness: false } or { IsFreight: false } => "ct-plate ct-plate--warn",
        _ => "ct-plate ct-plate--ok",
    };

    protected override async Task OnParametersSetAsync()
    {
        // 수정하러 열었으면 이미 적힌 번호를 한 번 읽어 둔다 — 창을 열자마자
        // 「이 차를 이렇게 읽고 있다」가 보여야 한다.
        if (!string.IsNullOrWhiteSpace(Draft.PlateNo) && _plate is null)
        {
            await ReadPlateAsync();
        }
    }

    /// <summary>
    /// 번호판을 읽어 칸을 채운다.
    ///
    /// <para>
    /// [사람이 고친 값을 덮지 않는다]
    /// </para>
    ///
    /// <para>
    /// 읽은 값은 <b>등록할 때만</b> 칸에 넣는다. 수정 중에도 밀어 버리면,
    /// 번호판과 다른 차를 적어 둔 사람이 매번 되돌려야 한다.
    /// </para>
    /// </summary>
    private async Task OnPlateChangedAsync(string? value)
    {
        Draft.PlateNo = value ?? string.Empty;
        await ReadPlateAsync();
    }

    private async Task ReadPlateAsync()
    {
        var plate = (Draft.PlateNo ?? string.Empty).Trim();
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
            _plate = await Api.ReadPlateAsync(plate);
        }
        catch (JSini.Web.Http.ApiException)
        {
            // 못 읽어도 폼은 그대로 쓴다. 번호판 해석은 거드는 것이지 막는 것이 아니다.
            _plate = null;
            return;
        }

        // **등록할 때만 채운다.** 수정 중에 번호를 손보면 차종과 사업용 여부를
        // 다시 밀어 버리는데, 번호판과 다른 차를 적어 둔 사람은(자가용 번호판에
        // 사업용 허가를 받은 차 같은) 그때마다 되돌려야 한다.
        if (_plate is null || !Draft.IsNew) return;

        if (_plate.SuggestedClass is { } cls) Draft.VehicleClass = cls;
        if (_plate.IsBusiness is { } business) Draft.IsBusiness = business;
    }
}
