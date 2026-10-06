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
    /// 고른 시각이 <b>진입</b>인가 <b>진출</b>인가.
    ///
    /// <para>
    /// 기사가 아는 쪽이 둘 중 하나다 — 「지금 올라탄다」일 때도 있고
    /// 「몇 시까지 대야 한다」일 때도 있다. 뒤엣것을 진입으로만 받으면
    /// 사람이 머릿속에서 소요시간을 빼서 넣어야 하고, 그 뺄셈이 틀리면
    /// <b>할인율이 조용히 틀린다</b>.
    /// </para>
    ///
    /// <para>
    /// 개방식에는 이 가름이 없다 — 통과 시각 한 점이라 들고 나는 것이 같은 일이다.
    /// </para>
    /// </summary>
    private string _anchor = EntryAnchor;

    private const string EntryAnchor = "ENTRY";
    private const string ExitAnchor = "EXIT";
    private string? _vehicleId;

    /// <summary>고른 날짜. 열자마자 오늘(한국 달력)이다.</summary>
    private DateTime _atDate = AppTime.TodayDate;

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
    private int _atHour = AppTime.ToKorea(AppTime.UtcNow).Hour;

    /// <summary>
    /// 고른 분. 고르개가 10분 단위라 <b>지금을 10분으로 내린 값</b>으로 연다.
    ///
    /// <para>
    /// 올리지 않고 내리는 까닭: 고르는 시각이 대개 이미 지났거나 지금인 일이라,
    /// 올리면 아직 오지 않은 시각이 기본값이 된다.
    /// </para>
    /// </summary>
    private int _atMinute = AppTime.ToKorea(AppTime.UtcNow).Minute / 10 * 10;

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

    private bool AnchorIsEntry => _anchor != ExitAnchor;

    /// <summary>시각 칸의 이름. 개방식은 들고 나는 것이 같아 「통과」다.</summary>
    private string AtLabel => IsClosed ? (AnchorIsEntry ? "진입" : "진출") : "통과";

    /// <summary>추천이 알려 주는 쪽 — 고른 쪽의 반대다.</summary>
    private string OtherSideLabel => AnchorIsEntry ? "진출" : "진입";

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
    /// 되찾는 것은 <b>고른 것</b>뿐이다. 날짜와 시각은 담지 않는다 —
    /// 이 화면을 여는 까닭이 거의 「지금 올라탄다」라서, 어제 적어 둔 시각이
    /// 되살아나면 사람이 그것을 못 보고 어제 기준으로 셈한 할인율을 읽는다.
    ///
    /// <para>
    /// [회로가 아니라 <b>브라우저</b>에 적는다]
    /// </para>
    ///
    /// <para>
    /// 처음에는 <c>ScreenState</c>(회로 수명)에 맡겼다. 그것은 「떠났다 돌아오기」만
    /// 버티고 <b>새로고침에 사라진다</b> — 이 저장소가 그 자리를 그렇게 정해 둔 것은
    /// 조건을 지우려고 F5 를 누른 사람에게 빠져나갈 길을 주려는 것이었다.
    /// </para>
    ///
    /// <para>
    /// 그런데 여기 담기는 것은 조회 조건이 아니라 <b>이 기사가 늘 쓰는 값</b>이다 —
    /// 내 차, 내가 다니는 구간, 늘 걸리는 시간. 그것이 F5 한 번에 사라지면
    /// 매번 네 번을 다시 누른다. 사이드바 폭을 브라우저에 적어 두는 것과 같은
    /// 성질이라(기기에 붙는 값) <c>localStorage</c> 로 옮겼다.
    /// </para>
    /// </summary>
    private sealed record Kept(string SectionType, string Anchor, string? VehicleId, string Duration, string? Target, bool HelpOpen);

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
    /// 고를 수 있는 시(時) — <b>24시간 전부</b>.
    ///
    /// <para>
    /// 한동안 「가장 긴 소요시간으로도 야간창에 닿지 못하는 시각」을 빼 두었다.
    /// 폐쇄식에서 넷(07~10)이 빠졌는데, <b>이 화면의 쓰임을 하나 놓친 추림이었다</b> —
    /// 앞으로 잡을 운행만 보는 것이 아니라 <b>이미 지나온 운행</b>의 할인율을
    /// 확인하는 데도 쓴다. 실제 통과 시각이 09시였던 사람은 그 시각을 고를 수가
    /// 없었고, 「0%라 고를 이유가 없다」는 우리 판단이 그 사람에게는
    /// 「왜 내 시각이 없지」였다.
    /// </para>
    ///
    /// <para>
    /// 넷을 아끼자고 고르개가 묻는 말을 바꿀 값어치가 없다. 전부 둔다.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<int> HourOptions = [.. Enumerable.Range(0, 24)];

    /// <summary>
    /// 고를 수 있는 분 — <b>10분 단위 여섯</b>.
    ///
    /// <para>
    /// 한동안 「지금」의 분이 눈금에 없으면 그 값을 한 줄 끼워 넣었다. 열자마자
    /// 보이는 시각이 실제 시각과 1분도 안 어긋나게 하려던 것인데, <b>값에 비해
    /// 비싼 규칙이었다</b> — 00·10·20·30·40·50 사이에 41 하나가 끼면 고르개가
    /// 고장 난 것처럼 보이고, 정작 그 1분은 비율을 바꾸지 못한다(분모가 몇 시간이다).
    /// 다섯 시간 운행에서 9분은 비율로 3%p 안쪽이라 띠가 바뀌지 않는다.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<int> MinuteOptions = [0, 10, 20, 30, 40, 50];

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
    }

    /// <summary>
    /// 적어 둔 것을 되찾고 첫 셈을 돌린다.
    ///
    /// <para>
    /// <b>여기여야 한다.</b> 브라우저 저장소는 회로가 붙은 뒤에만 읽을 수 있어서
    /// (프리렌더 중에는 JS 를 못 부른다) <c>OnInitialized</c> 에서는 꺼낼 수가 없다.
    /// </para>
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

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
        _atDate = _atDate.AddDays(days);
        return RecalcAsync();
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
        _ = JS.InvokeVoidAsync("localStorage.setItem", StateKey,
            JsonSerializer.Serialize(new Kept(
                _sectionType, _anchor, _vehicleId, _duration, _targetDiscount, _helpOpen)));

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
        var picked = Wall(_atDate.Date.AddHours(_atHour).AddMinutes(_atMinute));
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
