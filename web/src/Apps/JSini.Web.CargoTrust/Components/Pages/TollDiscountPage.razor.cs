using System.Globalization;
using System.Text.Json;
using JSini.Web.CargoTrust.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class TollDiscountPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private TollRulesInfo? _rules;
    private IReadOnlyList<VehicleInfo> _vehicles = [];

    private string _sectionType = CargoCodes.Closed;

    /// <summary>
    /// 고른 시각이 진입인가 진출인가. 기사는 둘 중 아는 쪽만 말하므로, 뒤엣것을 진입으로만 받으면 소요시간 뺄셈이 틀려 할인율이 조용히 틀린다.
    /// </summary>
    private string _anchor = EntryAnchor;

    private const string EntryAnchor = "ENTRY";
    private const string ExitAnchor = "EXIT";
    private string? _vehicleId;

    /// <summary>고른 날짜. 열자마자 오늘(한국 달력)이다.</summary>
    private DateTime _atDate = AppTime.TodayDate;

    /// <summary>
    /// 진입 시각. 열자마자 지금(한국 시각)이다. 분은 깎지 않는다 — 반올림해 보여 주면 사람이 적은 값과 다른 수를 보게 된다.
    /// </summary>
    private int _atHour = AppTime.ToKorea(AppTime.UtcNow).Hour;

    /// <summary>
    /// 고른 분. 고르개가 10분 단위라 지금을 10분으로 내린 값으로 연다 — 올리면 아직 오지 않은 시각이 기본값이 된다.
    /// </summary>
    private int _atMinute = AppTime.ToKorea(AppTime.UtcNow).Minute / 10 * 10;

    /// <summary>소요시간(분). 칩 값이라 문자열이다.</summary>
    private string _duration = "300";

    /// <summary>목표 할인율. <c>null</c> 이면 추천을 안 한다.</summary>
    private string? _targetDiscount;

    private IReadOnlyList<TollPlazaInfo> _plazas = [];
    private string? _fromPlaza;
    private string? _toPlaza;
    private TollFareInfo? _fare;

    /// <summary>지금 들고 있는 요금이 어느 (구간 · 차종)의 것인가. 같으면 다시 묻지 않는다.</summary>
    private string? _fareKey;

    /// <summary>규칙·차량·영업소를 다 받았나. 되찾기가 이것을 기다린다.</summary>
    private bool _loaded;

    /// <summary>되찾기는 한 번만.</summary>
    private bool _restored;

    private TollCalcResultInfo? _calc;
    private TollSuggestResultInfo? _suggest;

    /// <summary>
    /// 마지막으로 시작한 셈. 칩을 빨리 여러 번 누르면 앞선 왕복이 뒤늦게 돌아와
    /// <b>방금 고른 값과 다른 결과를 덮어쓸 수 있다</b> — 번호가 다르면 버린다.
    /// </summary>
    private int _turn;

    /// <summary>대상 조건을 펴 두었나. 이것도 기억한다 — 한 번 접은 사람은 계속 접어 둔다.</summary>
    private bool _helpOpen;

    private bool IsClosed => _sectionType == CargoCodes.Closed;

    private bool AnchorIsEntry => _anchor != ExitAnchor;

    /// <summary>시각 칸의 이름. 개방식은 들고 나는 것이 같아 「통과」다.</summary>
    private string AtLabel => IsClosed ? (AnchorIsEntry ? "진입" : "진출") : "통과";

    /// <summary>추천이 알려 주는 쪽 — 고른 쪽의 반대다.</summary>
    private string OtherSideLabel => AnchorIsEntry ? "진출" : "진입";

    /// <summary>
    /// 추천 카드가 무엇을 옮기라고 말하는가. DURATION 답이면 옮기는 것은 반대쪽이고, SHIFT 답이면 고른 쪽이다.
    /// </summary>
    private string ShiftSideLabel =>
        _suggest?.Mode == "SHIFT"
            ? (AnchorIsEntry ? "진입" : "진출")
            : OtherSideLabel;

    /// <summary>
    /// 고른 시각에서 얼마나 옮기는가. 「28분 당기면」이 「01:12 에 들어가십시오」보다
    /// 먼저 읽힌다 — 사람이 아는 것은 지금 시각이지 그 시각이 아니다.
    /// </summary>
    private string MovedLabel(TollSuggestOptionInfo option)
    {
        if (_suggest?.Mode != "SHIFT") return "지금 시각 그대로";

        var moved = (int)Math.Round((option.BestKst - Picked).TotalMinutes);
        return moved switch
        {
            0 => "지금 시각 그대로",
            < 0 => $"{CargoCodes.Minutes(-moved)} 당기면",
            _ => $"{CargoCodes.Minutes(moved)} 늦추면",
        };
    }

    /// <summary>사람이 고른 시각 한 덩어리.</summary>
    private DateTime Picked => _atDate.Date.AddHours(_atHour).AddMinutes(_atMinute);

    private static readonly IReadOnlyList<SchOption> AnchorChips =
    [
        new(EntryAnchor, "진입 시각"),
        new(ExitAnchor, "진출 시각"),
    ];

    private string HelpButtonClass => _helpOpen ? "ct-help-btn ct-help-btn--on" : "ct-help-btn";

    /// <summary>자격 판정의 옷. 색만으로 가르지 않고 표시(O·!·X)도 함께 둔다.</summary>
    private static string VerdictClass(string verdict) => verdict switch
    {
        "OK" => "ct-verdict ct-verdict--ok",
        "NO" => "ct-verdict ct-verdict--no",
        "CHECK" => "ct-verdict ct-verdict--check",
        _ => "ct-verdict",
    };

    private static string VerdictMark(string verdict) => verdict switch
    {
        "OK" => "O",
        "NO" => "X",
        "CHECK" => "!",
        _ => "·",
    };

    /// <summary>
    /// 고른 것을 적어 두는 열쇠. 화면 경로로 짓는다 — 겹치면 다른 화면의 조건을 되찾는다.
    /// </summary>
    private const string StateKey = "jsini-toll-pick";

    /// <summary>
    /// 되찾는 것은 고른 것뿐이다. 날짜와 시각은 담지 않는다 — 어제 적어 둔 시각이 되살아나면 어제 기준 할인율을 읽게 된다. 회로 상태가 아니라 브라우저(localStorage)에 적는다.
    /// </summary>
    private sealed record Kept(
        string SectionType, string Anchor, string? VehicleId, string Duration, string? Target, bool HelpOpen,
        string? FromPlaza = null, string? ToPlaza = null);

    /// <summary>
    /// 추천의 소요시간 상한. 고른 값에서 여섯 시간까지 더 끌 수 있다고 본다.
    /// 상한이 없으면 「열두 시간 더 달리면 50%」 같은 쓸모없는 답이 나온다.
    /// </summary>
    private const int SlackMinutes = 360;

    // ── 칩 ───────────────────────────────────────────────────

    private static readonly IReadOnlyList<SchOption> SectionChips = CargoCodes.SectionTypeChips;

    /// <summary>
    /// 소요시간 칩. 화물 운행에서 나오는 길이만 고른다 — 분 단위는 손가락으로 쓰기 어렵고, 비율의 분모라 몇 분 차이로는 띠가 바뀌지 않는다.
    /// </summary>
    private static readonly IReadOnlyList<SchOption> DurationChips =
    [
        new("120", "2시간"),
        new("180", "3시간"),
        new("240", "4시간"),
        new("300", "5시간"),
        new("360", "6시간"),
        new("480", "8시간"),
        new("600", "10시간"),
    ];

    /// <summary>목표 할인율. 규칙에서 만든다 — 제도가 바뀌면 칩도 따라 바뀐다.</summary>
    private IReadOnlyList<SchOption> TargetChips =>
    [
        new(null, "안 고름"),
        .. (_rules?.TargetChoices(_sectionType) ?? [])
            .Select(d => new SchOption(d.ToString("0.#", CultureInfo.InvariantCulture), $"{d:0.#}%")),
    ];

    /// <summary>
    /// 고를 수 있는 시(時) — 24시간 전부. 가장 긴 소요시간으로도 야간창에 닿지 못하는 시각을 빼면, 이미 지나온 운행의 할인율을 확인할 때 실제 통과 시각을 고를 수 없다.
    /// </summary>
    private static readonly IReadOnlyList<int> HourOptions = [.. Enumerable.Range(0, 24)];

    /// <summary>
    /// 고를 수 있는 분 — 10분 단위 여섯. 「지금」의 분을 눈금에 끼워 넣지 않는다 — 고르개가 고장 난 것처럼 보이고 1분은 비율을 바꾸지 못한다.
    /// </summary>
    private static readonly IReadOnlyList<int> MinuteOptions = [0, 10, 20, 30, 40, 50];

    /// <summary>
    /// 소요시간 칩. 구간을 골랐으면 <b>그 구간의 주행시간</b>을 한 줄로 더한다 —
    /// 「3시간 51분」처럼 눈금에 없는 값이라 더하지 않으면 고를 수가 없다.
    /// </summary>
    private IReadOnlyList<SchOption> DurationOptions
    {
        get
        {
            if (_fare is not { DriveMinutes: > 0 } fare) return DurationChips;

            var value = fare.DriveMinutes.ToString(CultureInfo.InvariantCulture);
            if (DurationChips.Any(c => c.Value == value)) return DurationChips;

            var list = DurationChips.ToList();
            list.Insert(0, new SchOption(value, $"주행 {CargoCodes.Minutes(fare.DriveMinutes)}"));
            return list;
        }
    }

    private IReadOnlyList<SchOption> VehicleChips =>
        [.. _vehicles.Select(v => new SchOption(
            v.VehicleId.ToString(CultureInfo.InvariantCulture), v.Display))];

    /// <summary>한 대일 때 이름 밑에 적는 줄 — 고르지 않아도 무엇으로 셈하는지는 보여야 한다.</summary>
    private static string VehicleSub(VehicleInfo v) =>
        $"{v.VehicleClassName} · {(v.AxleCount is { } a ? $"{a}축" : "축수 모름")} · {(v.IsBusiness ? "사업용" : "비사업용")}";

    /// <summary>대상 조건은 둘 중 들어온 쪽에서 가져온다 — 같은 목록이다.</summary>
    private IReadOnlyList<EligibilityCheckInfo> Checks =>
        _calc?.Checks ?? _suggest?.Checks ?? [];

    // ── 수명 주기 ────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(async () =>
        {
            // 규칙이 없으면 목표 칩을 만들 수 없고, 차량이 없어도 계산은 된다 —
            // 차량 쪽 실패로 화면을 막지 않는다.
            _rules = await Api.GetTollRulesAsync();
            _vehicles = await Api.GetMyVehiclesAsync();
            // 영업소는 477건이라 한 번에 받아 고르개에 담는다. 글자로 걸러 고른다.
            _plazas = await Api.SearchPlazasAsync(null);

            // 한 대면 고르게 하지 않지만, 셈에는 그 차를 쓴다.
            _vehicleId = (_vehicles.FirstOrDefault(v => v.IsDefault) ?? _vehicles.FirstOrDefault())
                ?.VehicleId.ToString(CultureInfo.InvariantCulture);
            _loaded = true;
            return 1;
        }, failMessage: "할인 규칙을 읽지 못했습니다");
    }

    /// <summary>
    /// 적어 둔 것을 되찾고 첫 셈을 돌린다. 저장소는 회로가 붙은 뒤에만 읽히고, 차량 목록이 올 때까지(_loaded) 기다린다 — firstRender 를 기다리면 차량만 기본값으로 떨어진다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_restored || !_loaded) return;
        _restored = true;

        try
        {
            var json = await JS.InvokeAsync<string?>("localStorage.getItem", StateKey);
            if (!string.IsNullOrWhiteSpace(json)
                && JsonSerializer.Deserialize<Kept>(json) is { } kept)
            {
                _sectionType = kept.SectionType;
                _anchor = kept.Anchor;
                _duration = kept.Duration;
                _targetDiscount = kept.Target;
                _helpOpen = kept.HelpOpen;
                _fromPlaza = kept.FromPlaza;
                _toPlaza = kept.ToPlaza;

                // 그때 고른 차가 **지금도 있을 때만** 되찾는다 — 지운 차의 번호가
                // 되살아나면 서버가 못 찾아 차량 없이 셈한 결과가 나온다.
                if (kept.VehicleId is { } id && _vehicles.Any(v =>
                        v.VehicleId.ToString(CultureInfo.InvariantCulture) == id))
                {
                    _vehicleId = id;
                }
            }
        }
        catch (JsonException)
        {
            // 옛 꼴이 남아 있으면 그냥 기본값으로 연다. 적어 둔 것 때문에 화면이 안 열리면 안 된다.
        }

        // 열자마자 한 번 셈해 둔다. 오늘·지금·다섯 시간이 이미 들어 있으므로
        // 사용자는 **아무것도 안 고치고도** 자기 할인율을 본다.
        await RecalcAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 칩을 누르면 값을 바꾸고 다시 셈한다. StateHasChanged 를 왕복을 기다리기 전에 부른다 — Blazor 는 핸들러가 끝나야 그리므로 기다리면 칩 색이 늦게 바뀐다.
    /// </summary>
    private Task PickAsync(Action apply)
    {
        apply();
        Keep();
        StateHasChanged();
        return RecalcAsync();
    }

    /// <summary>날짜를 하루씩 옮긴다. 달력을 열지 않고 어제·내일로 간다.</summary>
    private Task ShiftDayAsync(int days)
    {
        _atDate = _atDate.AddDays(days);
        return RecalcAsync();
    }

    /// <summary>
    /// 구간을 고르면 구간 유형과 주행시간을 채운다. 주행시간은 소요시간 칩에 한 줄로 더해 두므로 칩을 치우지 않는다 — 휴게소 시간을 사람이 더해 볼 수 있어야 한다.
    /// </summary>
    private Task OnFromPlazaAsync(string? code) => PickPlazaAsync(() => _fromPlaza = code);

    private Task OnToPlazaAsync(string? code) => PickPlazaAsync(() => _toPlaza = code);

    private Task PickPlazaAsync(Action apply)
    {
        apply();

        // 구간 유형은 **진입 영업소**가 정한다. 개방식은 들고 나는 것이 같은 일이라
        // 출발 쪽 하나로 족하고, 섞이면 둘 중 무엇을 따를지가 생긴다.
        if (Plaza(_fromPlaza) is { } from) _sectionType = from.SectionType;

        Keep();
        StateHasChanged();
        return RecalcAsync();
    }

    private TollPlazaInfo? Plaza(string? code) =>
        code is null ? null : _plazas.FirstOrDefault(p => p.UnitCode == code);

    private void ToggleHelp()
    {
        _helpOpen = !_helpOpen;
        Keep();
    }

    /// <summary>
    /// 고른 것을 맡긴다. 누를 때마다 맡긴다 — 떠날 때 한 번만 맡기면 탭을 닫는 길에서 그 호출이 안 온다.
    /// </summary>
    private void Keep() =>
        _ = JS.InvokeVoidAsync("localStorage.setItem", StateKey,
            JsonSerializer.Serialize(new Kept(
                _sectionType, _anchor, _vehicleId, _duration, _targetDiscount, _helpOpen,
                _fromPlaza, _toPlaza)));

    // ── 셈 ───────────────────────────────────────────────────

    /// <summary>
    /// 지금 고른 값으로 셈한다. 단추가 없으므로 여기가 유일한 길이다. 정방향은 늘 돌리고, 목표를 골랐을 때만 역방향을 더 돌린다.
    /// </summary>
    private async Task RecalcAsync()
    {
        var turn = ++_turn;
        var picked = Wall(Picked);
        var minutes = int.TryParse(_duration, out var m) ? m : 300;

        // 고른 쪽이 어디든 **엔진에는 진입·진출 한 쌍으로 넘긴다** —
        // 기준을 셈 안까지 끌고 들어가면 같은 식이 두 벌이 된다.
        var entry = IsClosed && !AnchorIsEntry ? picked.AddMinutes(-minutes) : picked;

        try
        {
            var calc = await Api.CalcTollDiscountAsync(new TollCalcRequest
            {
                SectionType = _sectionType,
                EntryAt = entry,
                ExitAt = IsClosed ? entry.AddMinutes(minutes) : null,
                VehicleId = VehicleId,
                // 칩을 누를 때마다 남기면 이력이 손가락 자국으로 뒤덮인다.
                Save = false,
            });

            if (turn != _turn) return;
            _calc = calc;

            // 통행료는 구간·차종이 바뀌었을 때만 간다. 안 바뀌었으면 왕복이 없다.
            await LoadFareAsync();
            if (turn != _turn) return;

            if (_targetDiscount is null)
            {
                _suggest = null;
                return;
            }

            var suggest = await Api.SuggestTollTimeAsync(new TollSuggestRequest
            {
                SectionType = _sectionType,
                // 추천은 **고른 쪽을 붙박고** 반대쪽을 찾는다 — 서버가 양쪽을 받는다.
                Anchor = AnchorIsEntry ? EntryAnchor : ExitAnchor,
                AnchorAt = picked,
                TargetDiscount = decimal.Parse(_targetDiscount, CultureInfo.InvariantCulture),
                MinDurationMinutes = minutes,
                MaxDurationMinutes = minutes + SlackMinutes,
                VehicleId = VehicleId,
                Save = false,
            });

            if (turn != _turn) return;
            _suggest = suggest;
        }
        catch (JSini.Web.Http.ApiException ex)
        {
            if (turn != _turn) return;
            _calc = null;
            _suggest = null;
            Say($"계산하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    /// <summary>
    /// 통행료를 묻는다. 구간이나 차종이 바뀌었을 때만 부른다 — 요금은 시각·소요시간과 무관하다. 깎인 금액은 정상요금 × 할인율로 화면에서 곱하고, 할인율을 정하는 곳은 엔진 한 군데다.
    /// </summary>
    private async Task LoadFareAsync()
    {
        if (_fromPlaza is null || _toPlaza is null)
        {
            _fare = null;
            _fareKey = null;
            return;
        }

        var cls = _vehicles.FirstOrDefault(v =>
            v.VehicleId.ToString(CultureInfo.InvariantCulture) == _vehicleId)?.VehicleClass ?? "C4";
        var key = $"{_fromPlaza}>{_toPlaza}:{cls}";
        if (key == _fareKey) return;

        _fareKey = key;
        try
        {
            // 할인율은 0 으로 묻는다 — 깎인 값은 화면이 곱한다.
            _fare = await Api.GetTollFareAsync(_fromPlaza, _toPlaza, cls, 0m);
        }
        catch (JSini.Web.Http.ApiException)
        {
            _fare = null;
            _fareKey = null;
        }
    }

    /// <summary>지금 할인율로 깎은 통행료. 원 단위로 <b>내린다</b> — 올리면 실제보다 많이 깎인 것처럼 말한다.</summary>
    private (int Normal, int Saved, int After)? FareNow
    {
        get
        {
            if (_fare?.NormalFare is not { } normal || normal <= 0) return null;
            var discount = _calc?.DiscountPercent ?? 0m;
            var saved = (int)Math.Floor(normal * discount / 100m);
            return (normal, saved, normal - saved);
        }
    }

    private long? VehicleId =>
        long.TryParse(_vehicleId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>
    /// 편집기가 준 시각을 KST 벽시계로 못 박는다. Kind 를 그대로 두면 +00:00 이 붙어 서버가 아홉 시간을 어긋나게 읽고, 할인율이 오류 없이 한 띠 밀린다.
    /// </summary>
    private static DateTime Wall(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
