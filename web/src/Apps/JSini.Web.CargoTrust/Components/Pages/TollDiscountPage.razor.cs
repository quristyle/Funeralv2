using System.Globalization;
using JSini.Web.CargoTrust.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class TollDiscountPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>떠날 때 고른 것을 맡기고 돌아와서 되찾는 자리.</summary>
    [Inject] private ScreenState Screen { get; set; } = default!;

    private TollRulesInfo? _rules;
    private IReadOnlyList<VehicleInfo> _vehicles = [];

    private string _sectionType = CargoCodes.Closed;
    private string? _vehicleId;

    /// <summary>진입 날짜. 열자마자 오늘(한국 달력)이다.</summary>
    private DateTime _entryDate = AppTime.TodayDate;

    /// <summary>
    /// 진입 시각 — 시와 분을 따로 고른다. 열자마자 지금(한국 시각)이다.
    ///
    /// <para>
    /// 기사가 쓰는 때는 거의 「지금 올라탄다」라서, 대개 여기를 고치지 않고
    /// 소요시간만 누르면 된다. 분은 <b>안 깎는다</b> — 23:58 과 00:02 는
    /// 야간창 안이라는 점에서 같지만, 반올림해 보여 주면 사람이 제가 적은 값과
    /// 다른 수를 보게 된다.
    /// </para>
    /// </summary>
    private int _entryHour = AppTime.ToKorea(AppTime.UtcNow).Hour;

    private int _entryMinute = AppTime.ToKorea(AppTime.UtcNow).Minute;

    /// <summary>시 고르개를 24시간 다 펴 두었나. 「전체 보기」를 누르면 참이 된다.</summary>
    private bool _allHours;

    /// <summary>소요시간(분). 칩 값이라 문자열이다.</summary>
    private string _duration = "300";

    /// <summary>목표 할인율. <c>null</c> 이면 추천을 안 한다.</summary>
    private string? _targetDiscount;

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

    private string HelpButtonClass => _helpOpen ? "ct-help-btn ct-help-btn--on" : "ct-help-btn";

    /// <summary>
    /// 맡기는 열쇠. 화면 경로로 짓는다 — 겹치면 다른 화면의 조건을 되찾는다.
    /// </summary>
    private const string StateKey = "cargotrust/toll";

    /// <summary>
    /// 되찾는 것은 <b>고른 것</b>뿐이다. 날짜와 시각은 담지 않는다 —
    /// 이 화면을 여는 까닭이 거의 「지금 올라탄다」라서, 어제 적어 둔 시각이
    /// 되살아나면 사람이 그것을 못 보고 어제 기준으로 셈한 할인율을 읽는다.
    /// </summary>
    private sealed record Kept(string SectionType, string? VehicleId, string Duration, string? Target, bool HelpOpen);

    /// <summary>
    /// 추천의 소요시간 상한. 고른 값에서 여섯 시간까지 더 끌 수 있다고 본다.
    /// 상한이 없으면 「열두 시간 더 달리면 50%」 같은 쓸모없는 답이 나온다.
    /// </summary>
    private const int SlackMinutes = 360;

    // ── 칩 ───────────────────────────────────────────────────

    private static readonly IReadOnlyList<SchOption> SectionChips = CargoCodes.SectionTypeChips;

    /// <summary>
    /// 소요시간. 화물 운행에서 실제로 나오는 길이만 고른다 —
    /// 분 단위로 적게 하면 손가락으로 쓰기 어렵고, 비율의 분모라 몇 분 차이로는
    /// 띠가 바뀌지도 않는다.
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
    /// 고를 수 있는 시(時).
    ///
    /// <para>
    /// [빼는 것은 「확실히 0%」인 시각뿐이다]
    /// </para>
    ///
    /// <para>
    /// 낮에 진입해도 길게 달리면 야간에 걸린다 — 15시에 들어가 열 시간을 달리면
    /// 새벽 1시에 나오고 야간이 네 시간이다. 그래서 「낮은 쓸모없다」로 자를 수 없다.
    /// <b>가장 긴 소요시간으로도 야간창에 닿지 못하는 시각</b>만 뺀다 — 그것은
    /// 무엇을 고르든 0% 라서 고를 이유가 없다.
    /// </para>
    ///
    /// <para>
    /// 개방식은 통과 시각 한 점이라 <b>야간창 자체</b>가 답이다(23~05 → 일곱 시각).
    /// 폐쇄식에서는 네 시각(07~10)밖에 못 뺀다 — 적어 보이지만 그 넷이
    /// 실제로 고를 일이 없는 유일한 시각이다.
    /// </para>
    ///
    /// <para>
    /// 「전체 보기」로 24시간을 다 펼 수 있다. 규칙이 바뀌거나 우리 셈이 틀렸을 때
    /// <b>고를 수 없게 되는 시각이 생기면 안 된다.</b>
    /// </para>
    /// </summary>
    private IReadOnlyList<int> HourOptions
    {
        get
        {
            if (_allHours || _rules is null) return AllHours;

            var (start, end) = _rules.NightHours(_sectionType);

            // 개방식은 통과 한 점이라 창 안의 시각만 뜻이 있다.
            // 폐쇄식은 가장 긴 소요시간만큼 앞당겨 들어가도 창에 닿는다.
            var from = IsClosed
                ? ((start - LongestDurationHours) % 24 + 24) % 24
                : start;

            var hours = new List<int>();
            for (var h = from; ; h = (h + 1) % 24)
            {
                hours.Add(h);
                if (h == end) break;
                if (hours.Count >= 24) break;
            }

            // 지금 고른 시각이 빠지면 고르개가 제멋대로 다른 값으로 튄다.
            if (!hours.Contains(_entryHour)) hours.Add(_entryHour);
            hours.Sort();
            return hours;
        }
    }

    private int HiddenHourCount => 24 - HourOptions.Count;

    /// <summary>소요시간 칩 중 가장 긴 것(시간). 시 고르개를 추리는 기준이다.</summary>
    private static readonly int LongestDurationHours =
        (int)Math.Ceiling(DurationChips.Max(c => int.Parse(c.Value!, CultureInfo.InvariantCulture)) / 60.0);

    private static readonly IReadOnlyList<int> AllHours = [.. Enumerable.Range(0, 24)];

    /// <summary>
    /// 고를 수 있는 분. <b>10분 단위</b>로 여섯 개다 — 손가락으로 빨리 고르려는 것이고,
    /// 몇 분 차이로 띠가 바뀌지도 않는다(비율의 분모가 몇 시간이다).
    ///
    /// <para>
    /// 다만 <b>지금 시각의 분은 깎지 않는다.</b> 열자마자 23:57 이면 57 을 그대로
    /// 한 줄 끼워 넣는다 — 반올림해 두면 사람이 제가 본 시각과 다른 수를 읽는다.
    /// </para>
    /// </summary>
    private IReadOnlyList<int> MinuteOptions
    {
        get
        {
            var minutes = new List<int> { 0, 10, 20, 30, 40, 50 };
            if (!minutes.Contains(_entryMinute)) minutes.Add(_entryMinute);
            minutes.Sort();
            return minutes;
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

            // 한 대면 고르게 하지 않지만, 셈에는 그 차를 쓴다.
            _vehicleId = (_vehicles.FirstOrDefault(v => v.IsDefault) ?? _vehicles.FirstOrDefault())
                ?.VehicleId.ToString(CultureInfo.InvariantCulture);
            return 1;
        }, failMessage: "할인 규칙을 읽지 못했습니다");

        // 맡겨 둔 것이 있으면 그것이 이긴다. 다만 **그때 고른 차가 지금도 있을 때만** —
        // 지운 차의 번호가 되살아나면 서버가 못 찾아 차량 없이 셈한 결과가 나온다.
        if (Screen.Get<Kept>(StateKey) is { } kept)
        {
            _sectionType = kept.SectionType;
            _duration = kept.Duration;
            _targetDiscount = kept.Target;
            _helpOpen = kept.HelpOpen;
            if (kept.VehicleId is { } id && _vehicles.Any(v =>
                    v.VehicleId.ToString(CultureInfo.InvariantCulture) == id))
            {
                _vehicleId = id;
            }
        }

        // 열자마자 한 번 셈해 둔다. 오늘·지금·다섯 시간이 이미 들어 있으므로
        // 사용자는 **아무것도 안 고치고도** 자기 할인율을 본다.
        await RecalcAsync();
    }

    /// <summary>칩을 누르면 값을 바꾸고 바로 다시 셈한다.</summary>
    private Task PickAsync(Action apply)
    {
        apply();
        Keep();
        return RecalcAsync();
    }

    /// <summary>날짜를 하루씩 옮긴다. 달력을 열지 않고 어제·내일로 간다.</summary>
    private Task ShiftDayAsync(int days)
    {
        _entryDate = _entryDate.AddDays(days);
        return RecalcAsync();
    }

    /// <summary>시 고르개를 24시간으로 편다. 되돌리는 길은 두지 않는다 — 한 번 편 사람은 그대로 쓴다.</summary>
    private Task ShowAllHoursAsync()
    {
        _allHours = true;
        return Task.CompletedTask;
    }

    private void ToggleHelp()
    {
        _helpOpen = !_helpOpen;
        Keep();
    }

    /// <summary>
    /// 고른 것을 맡긴다. <b>누를 때마다 맡긴다</b> — 떠날 때 한 번만 맡기면
    /// 탭을 닫거나 창을 새로 여는 길에서 그 호출이 안 온다.
    /// </summary>
    private void Keep() =>
        Screen.Set(StateKey, new Kept(_sectionType, _vehicleId, _duration, _targetDiscount, _helpOpen));

    // ── 셈 ───────────────────────────────────────────────────

    /// <summary>
    /// 지금 고른 값으로 셈한다. <b>단추가 없으므로 여기가 유일한 길이다.</b>
    ///
    /// <para>
    /// 정방향은 늘 돌린다 — 「지금 조건이면 얼마」가 이 화면의 바닥이다.
    /// 목표를 골랐을 때만 역방향을 더 돌려 「몇 시에 나가면 되는지」를 붙인다.
    /// </para>
    /// </summary>
    private async Task RecalcAsync()
    {
        var turn = ++_turn;
        var entry = Wall(_entryDate.Date.AddHours(_entryHour).AddMinutes(_entryMinute));
        var minutes = int.TryParse(_duration, out var m) ? m : 300;

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

            if (_targetDiscount is null)
            {
                _suggest = null;
                return;
            }

            var suggest = await Api.SuggestTollTimeAsync(new TollSuggestRequest
            {
                SectionType = _sectionType,
                Anchor = "ENTRY",
                AnchorAt = entry,
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

    private long? VehicleId =>
        long.TryParse(_vehicleId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>
    /// 편집기가 준 시각을 <b>KST 벽시계</b>로 못 박는다.
    ///
    /// <para>
    /// 운영 컨테이너의 시계는 UTC 다(TZ=Etc/UTC). <c>Kind</c> 를 그대로 두면
    /// 직렬화기가 오프셋 <c>+00:00</c> 을 붙여 보내고, 서버는 그것을 한국 시각으로
    /// 읽어 <b>아홉 시간이 어긋난다.</b> 그 어긋남은 오류 없이 할인율이 한 띠
    /// 밀리는 모습으로만 드러난다.
    /// </para>
    /// </summary>
    private static DateTime Wall(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
