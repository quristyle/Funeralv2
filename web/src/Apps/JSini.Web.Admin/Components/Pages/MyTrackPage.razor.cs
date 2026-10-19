using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Components.Settings;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class MyTrackPage
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private GeoLocator Geo { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>지금 위치를 재어 기록하는 중인가. 단추를 잠근다.</summary>
    private bool _recording;

    /// <summary>지도 판. JS 가 여기에 타일 · 선 · 점을 붙인다.</summary>
    private ElementReference _stage;

    private IJSObjectReference? _module;
    private IJSObjectReference? _map;

    /// <summary>점을 눌렀을 때 JS 가 되부를 손잡이.</summary>
    private DotNetObjectReference<MyTrackPage>? _self;

    /// <summary>보고 있는 날. 한국 달력 날짜다 — 서버도 같은 달력으로 하루를 끊는다.</summary>
    private DateTime _date = AppTime.TodayDate;

    /// <summary>서버가 준 하루치 한 벌. 날짜를 바꾸면 통째로 갈린다.</summary>
    private MyLocationTrackDto? _track;

    /// <summary>기록이 있는 날들. 날짜 고르개가 빈 날을 피하게 해 준다.</summary>
    private IReadOnlyList<LocationTrackDayDto> _days = [];

    /// <summary>목록을 <b>날것</b>으로 볼 것인가. 지도는 따라 바뀌지 않는다.</summary>
    private bool _raw;

    /// <summary>지금 고른 자리. 지도의 점과 왼쪽 목록이 같은 값을 본다.</summary>
    private LocationStayDto? _picked;

    /// <summary>점·선을 다시 싣나. 그린 뒤에 보낸다 — 첫 진입에는 아직 판이 없을 수 있다.</summary>
    private bool _dirty;

    /// <summary>다시 실을 때 「전체 보기」까지 할 것인가.</summary>
    private bool _refit;

    /// <summary>휴대폰(≤767px)인가. <c>DxLayoutBreakpoint</c> 가 채운다.</summary>
    private bool _isPhone;

    /// <summary>휴대폰에서 지금 선 판.</summary>
    private PhoneView _view = PhoneView.Map;

    /// <summary>감춰져 있던 지도 판이 다시 섰다. 그린 뒤에 알린다 — 이 값을 올릴 때는 CSS 가 판을 펴기 전이라 크기가 0 이다.</summary>
    private bool _reveal;

    /// <summary>휴대폰에서 한 번에 하나씩 서는 판.</summary>
    private enum PhoneView
    {
        Map,
        List,
    }

    /// <summary>휴대폰에서 판 둘 중 하나를 감추는 클래스.</summary>
    private string? PhoneCss => _isPhone
        ? $"ad-split--phone ad-split--{(_view == PhoneView.Map ? "map" : "list")}"
        : null;

    /// <summary>묶어 낸 머문 자리들. 자료가 없으면 빈 목록이다.</summary>
    private IReadOnlyList<LocationStayDto> Stays => _track?.Stays ?? [];

    /// <summary>그날 쌓인 점 전부.</summary>
    private IReadOnlyList<LocationTrackPointDto> Points => _track?.Points ?? [];

    /// <summary>서버에 보낼 날짜 글자. 내려받기 파일 이름에도 쓰인다.</summary>
    private string DateKey => DateOnly.FromDateTime(_date).ToString("yyyy-MM-dd");

    /// <summary>지금 고른 날의 「기록이 있는 날」 항목. 없을 수 있다 — 빈 날을 직접 고르면 고르개는 빈 채로 남는다.</summary>
    private LocationTrackDayDto? CurrentDay =>
        _days.FirstOrDefault(d => d.Date == DateKey);

    /// <summary>왼쪽 판의 머리 줄. 무엇을 보고 있는지로 갈린다.</summary>
    private string ListTitle => _raw
        ? $"원시 기록 ({Points.Count})"
        : $"머문 자리 ({Stays.Count})";

    /// <summary>묶는 자를 사람이 읽는 말로. 요약 줄이 적는다.</summary>
    private string StayRadiusText => $"{_track?.StayRadiusMeters ?? 200:0}m";

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        DateKey,
        SchSummary.On(_raw, "원시 기록"),
        $"머문 곳 {Stays.Count}곳");

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _track = await Api.GetMyLocationTrackAsync(DateOnly.FromDateTime(_date));

        // 기록이 있는 날 목록은 함께 읽는다 — 따로 읽으면 방금 쌓인 날을 고르개가 모르게 된다.
        _days = await Api.GetMyLocationTrackDaysAsync();

        // 사라진 자리를 고른 채로 두지 않는다 — 날짜를 바꾸면 그 자리는 없다.
        if (_picked is { } prev && !Stays.Any(s => s.Seq == prev.Seq))
        {
            _picked = null;
        }

        // 자료가 바뀌었다 — 날이 달라졌으니 보던 자리도 되돌아가도 좋다.
        Refresh(true);

        return Points.Count;
    }, "그날 쌓인 기록이 없습니다.", "이동 경로를 읽지 못했습니다");

    /// <summary>날짜를 골랐다. <b>비면 오늘로 되돌린다</b> — 빈 날짜로는 물을 수 없다.</summary>
    private Task PickDateAsync(DateTime? date)
    {
        var next = (date ?? AppTime.TodayDate).Date;

        if (next == _date) return Task.CompletedTask;

        _date = next;
        return ReloadAsync();
    }

    /// <summary>「기록이 있는 날」에서 골랐다.</summary>
    private Task PickDayAsync(LocationTrackDayDto? day) =>
        day is null || !DateOnly.TryParse(day.Date, out var parsed)
            ? Task.CompletedTask
            : PickDateAsync(parsed.ToDateTime(TimeOnly.MinValue));

    /// <summary>하루씩 옮긴다. 앞으로는 오늘까지만 — 내일의 기록은 있을 수 없고, 빈 화면을 내주면 기능이 깨진 것으로 읽힌다.</summary>
    private Task ShiftAsync(int days)
    {
        var next = _date.AddDays(days);

        return next > AppTime.TodayDate ? Task.CompletedTask : PickDateAsync(next);
    }

    /// <summary>
    /// 지금 위치를 재서 오늘 이동 경로에 한 점 기록한다.
    /// </summary>
    private async Task RecordLocationAsync()
    {
        if (_recording) return;

        _recording = true;
        try
        {
            var result = await Geo.RecordCurrentAsync();
            Say(result.Message, result.Tone);

            if (!result.Ok) return;

            // 기록은 오늘 날짜로 들어가므로, 과거를 보고 있었으면 오늘로 옮긴다.
            if (_date != AppTime.TodayDate)
            {
                _date = AppTime.TodayDate;
            }

            await ReloadAsync();
        }
        finally
        {
            _recording = false;
        }
    }

    /// <summary>점·선을 다시 실어 달라고 적어 둔다. 실제로 싣는 것은 그린 뒤다.</summary>
    private void Refresh(bool refit)
    {
        _dirty = true;
        _refit |= refit;
    }

    /// <summary>화면 폭이 경계를 넘었다.</summary>
    private void OnPhoneChanged(bool active)
    {
        _isPhone = active;
        _reveal |= !active;
    }

    /// <summary>휴대폰의 보기를 바꾼다.</summary>
    private void SetView(PhoneView view)
    {
        if (_view == view) return;

        _view = view;
        _reveal |= view == PhoneView.Map;
    }

    /// <summary>목록에서 자리를 눌렀다 — 지도로 넘어가며 그 자리로 옮긴다. PickAsync 를 거치지 않는다 — 이미 고른 줄이면 아무 일도 하지 않아 지도를 옮긴 뒤 같은 자리를 다시 눌러도 화면이 돌아오지 않는다.</summary>
    private async Task ShowOnMapAsync(LocationStayDto stay)
    {
        _view = PhoneView.Map;
        _reveal = true;
        _picked = stay;

        if (_map is not null)
        {
            await _map.InvokeVoidAsync("focus", Key(stay));
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_module is null)
        {
            // 회로가 붙기 전(프리렌더)에는 JS 를 부를 수 없다.
            if (!RendererInfo.IsInteractive) return;

            _self = DotNetObjectReference.Create(this);

            _module = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/geo-map.js");

            _map = await _module.InvokeAsync<IJSObjectReference>("create", _stage, _self);

            // 판이 방금 생겼다. 조회가 먼저 끝났으면 그 점들이 아직 안 실렸다.
            _dirty = true;
        }

        if (_dirty && _map is not null)
        {
            var refit = _refit;
            _dirty = false;
            _refit = false;

            // 선을 먼저, 점을 나중에 — 선 쪽은 맞추기를 하지 않으므로 점을 싣는 쪽이 마지막이어야 전체 보기가 한 번만 돈다.
            await _map.InvokeVoidAsync("setPath", Stays.Select(s => new { lat = s.Lat, lon = s.Lon }));
            await _map.InvokeVoidAsync("setMarkers", Stays.Select(Pin), refit);
        }

        // 감춰져 있던 판이 방금 섰다 — setMarkers 다음이어야 미뤄 둔 맞추기가 제 점들로 셈한다.
        if (_reveal && _map is not null)
        {
            _reveal = false;
            await _map.InvokeVoidAsync("show");
        }
    }

    /// <summary>지도에서 점을 눌렀다. 빈 곳을 누르면 null 이 온다 — 고른 것을 푸는 길이다. JS 가 되부르므로 StateHasChanged 를 직접 부른다.</summary>
    [JSInvokable]
    public void PickMarker(string? key)
    {
        _picked = int.TryParse(key, out var seq)
            ? Stays.FirstOrDefault(s => s.Seq == seq)
            : null;

        StateHasChanged();
    }

    /// <summary>목록에서 골랐다. 지도도 그 자리로 옮긴다. 이미 고른 줄이면 아무것도 안 한다 — 지도에서 고른 것이 표에 되비치는데 이 자리가 또 불리면 지도가 점을 누를 때마다 당겨진다.</summary>
    private async Task PickAsync(LocationStayDto? stay)
    {
        if (ReferenceEquals(_picked, stay)) return;

        _picked = stay;

        if (_map is not null)
        {
            await _map.InvokeVoidAsync("focus", stay is null ? null : Key(stay));
        }
    }

    private async Task FitAsync()
    {
        if (_map is not null) await _map.InvokeVoidAsync("fit");
    }

    private async Task ZoomAsync(int step)
    {
        if (_map is not null) await _map.InvokeVoidAsync("zoomBy", step);
    }

    /// <summary>지도에 넘길 점 하나. JS 는 이 다섯 칸만 본다. badge 를 실으면 작은 핀으로 찍는다 — 하루치 자리가 한 동네에 모여 이름표가 서로를 덮기 때문이다. 이름은 title 로 달려 손가락만 올려도 보인다.</summary>
    private object Pin(LocationStayDto s) => new
    {
        key = Key(s),
        lat = s.Lat,
        lon = s.Lon,

        // **번호가 먼저다.** 목록과 지도를 잇는 끈은 번호 하나다.
        name = $"{s.Seq}. {Where(s)}",

        // 핀에 적히는 글자. 번호만이다 — 이보다 길면 핀이 아니라 이름표가 된다.
        badge = s.Seq.ToString(),

        // 한 번만 관측된 자리는 흐리게 — 0분이 머물지 않은 것으로 읽히지 않게 지도에서도 가른다.
        muted = s.Samples <= 1,
    };

    /// <summary>지도의 점을 가리키는 열쇠. <b>번호 하나면 그날 안에서 유일하다.</b></summary>
    private static string Key(LocationStayDto s) => s.Seq.ToString();

    /// <summary>어디인가. 지역 이름이 없으면 좌표를 적는다 — 빈 칸은 자리를 모르는 것으로 읽힌다.</summary>
    private static string Where(LocationStayDto s) =>
        string.IsNullOrWhiteSpace(s.Place) ? Coord(s.Lat, s.Lon) : s.Place!;

    /// <summary>좌표 두 개를 한 마디로. 소수점 넷째 자리면 10m 쯤이다.</summary>
    private static string Coord(double lat, double lon) =>
        FormattableString.Invariant($"{lat:0.####}, {lon:0.####}");

    /// <summary>언제부터 언제까지. <b>한국 시각</b>이다.</summary>
    private static string Span(LocationStayDto s) =>
        $"{s.ArrivedAt.Kst("HH:mm")} ~ {s.LeftAt.Kst("HH:mm")}";

    /// <summary>머문 시간을 사람이 읽는 말로. 0분도 그대로 적는다 — 「—」로 두면 셈하지 못한 것으로 보이는데 실제로는 한 번만 관측된 자리다.</summary>
    private static string MinutesText(int minutes) => minutes switch
    {
        <= 0 => "0분",
        < 60 => $"{minutes}분",
        _ when minutes % 60 == 0 => $"{minutes / 60}시간",
        _ => $"{minutes / 60}시간 {minutes % 60}분",
    };

    /// <summary>머문 시간 칸의 도움말. 왜 이 값이 짧은지를 그 자리에서 말한다.</summary>
    private static string DwellTitle(LocationStayDto s) => s.Samples <= 1
        ? "한 번만 관측된 자리입니다. 머물지 않았다는 뜻이 아니라, 그 사이에 한 번 본 것입니다."
        : $"관측 {s.Samples}회 — 처음과 마지막 사이입니다. 실제로 머문 시간은 이보다 깁니다.";

    /// <summary>거리를 사람이 읽는 말로. 1km 를 넘으면 킬로미터다.</summary>
    private static string DistanceText(double meters) => meters switch
    {
        < 1 => "0m",
        < 1000 => FormattableString.Invariant($"{meters:0}m"),
        _ => FormattableString.Invariant($"{meters / 1000:0.0}km"),
    };

    /// <summary>오차 반지름. <b>모르는 경우가 흔하다</b> — 그때는 「—」다.</summary>
    private static string AccuracyText(LocationTrackPointDto p) =>
        p.Accuracy is { } m ? FormattableString.Invariant($"{m:0}m") : "—";

    /// <summary>같은 자리를 OpenStreetMap 에서 여는 주소.</summary>
    private static string BigMapUrl(LocationStayDto s) => FormattableString.Invariant(
        $"https://www.openstreetmap.org/?mlat={s.Lat:0.######}&mlon={s.Lon:0.######}#map=16/{s.Lat:0.######}/{s.Lon:0.######}");

    public async ValueTask DisposeAsync()
    {
        // 회로가 끊겼으면 JS 를 부를 수 없다 — 화면을 옮기며 늘 지나는 길이라 예외를 남기지 않는다.
        try
        {
            if (_map is not null)
            {
                await _map.InvokeVoidAsync("dispose");
                await _map.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }

        _self?.Dispose();
    }
}
