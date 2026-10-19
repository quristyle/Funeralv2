using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Components.Settings;
using JSini.Web.Http;
using JSini.Web.LifeEnv.Api;
using JSini.Web.Models;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherDashboard
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// 브라우저에 현재 위치를 묻기만 한다(재기만 하고 저장하지 않는다).
    /// 저장(GeoLocator.SaveAsync)은 알림 설정 화면의 몫 — 여기서 저장하면 날씨만 봐도 알림이 바뀐다.
    /// </summary>
    [Inject] private GeoLocator Geo { get; set; } = default!;

    private List<WeatherInfo> _weatherList = [];
    private List<WeatherLocation> _locations = [];
    private int _selectedLocationId;

    private WeatherInfo? _current;
    private List<MidTermForecast> _midTerm = [];
    private List<TrendPoint> _trend = [];

    /// <summary>
    /// 오늘 남은 시간대의 예보. 추이 차트와 같은 응답(forecast/{id})에서 갈라 낸다.
    /// </summary>
    private List<HourPoint> _todayHours = [];
    private TrendMetric _metric = TrendMetric.Temp;
    private bool _detailError;

    // ── 내 위치 ──────────────────────────────────────────────
    // 내 위치 날씨는 좌표로 지점 날씨를 받는다(life/weather/point). 주간 예보는 weekly=true 로 받고, 중기 구역은 서버가 좌표에서 고른다.
    // 예보 추이는 등록 지역에만 있어 내 위치에서는 빠진다 — 화면이 그 까닭을 한 줄로 말한다.

    /// <summary>
    /// 지역 고르개에서 내 위치를 가리키는 값. 음수여야 한다 — 0 은 「아직 안 골랐다」, 양수는 지역 Id 다.
    /// </summary>
    private const int MyLocationId = -1;

    private bool MyOn => _selectedLocationId == MyLocationId;

    /// <summary>
    /// 고르개에 얹는 목록 — 내 위치가 맨 앞, 뒤가 등록 지역. _locations 는 등록 지역만이라 기본 선택이 그것을 본다.
    /// 섞어 두면 화면이 열리자마자 위치 물음창이 뜬다.
    /// </summary>
    private List<WeatherLocation> _picks = [MyPick];

    /// <summary>고르개에 얹는 <b>가짜 지역</b> 한 줄. 서버에는 이런 행이 없다.</summary>
    private static WeatherLocation MyPick => new() { Id = MyLocationId, Name = "📍 내 위치" };

    /// <summary>브라우저가 준 좌표로 받아 온 그 지점의 날씨.</summary>
    private PointWeatherDto? _point;

    /// <summary>마지막으로 잰 좌표. 「새로고침」이 위치를 다시 묻지 않고 쓴다.</summary>
    private double _myLat;
    private double _myLon;

    /// <summary>위치를 묻는 중인가. 브라우저가 10초까지 붙들 수 있다.</summary>
    private bool _myBusy;

    /// <summary>
    /// 내 위치를 못 잡았거나 날씨를 못 읽은 까닭. 토스트가 아니라 화면에 붙박는다 — 토스트는 읽기 전에 사라진다.
    /// </summary>
    private string? _myError;

    /// <summary>추이 차트를 감싼 담장. 여기서 터진 것이 회로를 끌어내리지 않게 막는다.</summary>
    private ErrorBoundary? _chartGuard;

    /// <summary>
    /// 손가락으로 보는 화면(hover: none)이면 차트의 말풍선·십자선을 달지 않는다.
    /// 서버가 그리는 말풍선은 짚을 때마다 회로를 왕복해 화면이 굳고 스크롤과 다툰다. 너비가 아니라 hover 질의로 가른다.
    /// </summary>
    private bool _touchOnly;

    protected override Task OnInitializedAsync() => LoadDataAsync();

    /// <summary>
    /// 첫 그림 뒤에 손가락 화면 여부와 보이는 카드 번호를 브라우저에 묻고 맡긴다.
    /// JS 가 없거나 회로가 끊겼으면 조용히 넘어간다 — 그래도 화면은 세우지 않는다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.LifeEnv/js/weather-trend.js");

            if (firstRender)
            {
                var touchOnly = await _module.InvokeAsync<bool>("isTouchOnly");

                if (touchOnly != _touchOnly)
                {
                    _touchOnly = touchOnly;
                    StateHasChanged();
                }

                // 위치를 이미 허용한 브라우저만 물음창 없이 한 번 잰다. 허용한 적이 없으면 재지 않고 물러난다.
                await TryQuietLocationAsync();
            }

            // **아래 마크업과 같은 조건이어야 한다.** 칸이 없는데 지켜보라고
            // 하면 JS 가 빈 참조를 받는다.
            var hasDeck = !Loading && _current is not null && _trend.Count > 0;

            if (!hasDeck)
            {
                _deckWatched = false;
            }
            else if (!_deckWatched)
            {
                _self ??= DotNetObjectReference.Create(this);

                await _module.InvokeVoidAsync(
                    "watchDeck", _deck, _self, Array.IndexOf(Slides, _metric));

                _deckWatched = true;
            }
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        // 프리렌더 중이거나 회로가 아직/이미 없다.
        }
    }

    /// <summary>
    /// 전 지역 실황과 지역 목록. 실패를 삼키지 않는다 — 빈 카드는 「자료가 없다」로 읽히므로 실패 문구를 따로 낸다.
    /// </summary>
    private Task LoadDataAsync() => LoadAsync(async () =>
    {
        var weather = Client.GetLatestWeatherAsync();
        var locations = Client.GetLocationsAsync();

        await Task.WhenAll(weather, locations);

        _weatherList = [.. weather.Result];
        _locations = [.. locations.Result];

        // 고르개에는 내 위치가 한 줄 더 선다. 기본값은 등록 지역의 첫 줄이다 — 내 위치를 기본으로 두면 열자마자 위치 물음창이 뜬다.
        _picks = [MyPick, .. _locations];

        if (_selectedLocationId == 0 && _locations.Count > 0)
        {
            _selectedLocationId = _locations[0].Id;
        }

        // 내 위치를 보는 중의 새로고침은 위치를 다시 묻지 않고 잰 좌표로 날씨만 받는다. 물음창은 사람이 고르거나 단추를 누를 때만 뜬다.
        if (MyOn)
        {
            await FetchPointAsync(quiet: false);
        }
        else
        {
            await LoadSelectedAsync();
        }

        return _weatherList.Count;
    }, "등록된 관측 지역의 실황이 아직 없습니다.", "실황을 읽지 못했습니다");

    private async Task OnLocationSelected(int id)
    {
        if (id == 0 || id == _selectedLocationId)
        {
            return;
        }

        if (id == MyLocationId)
        {
            await SelectMyLocationAsync();
            return;
        }

        _selectedLocationId = id;
        _myError = null;
        await LoadSelectedAsync();
    }

    /// <summary>
    /// 내 위치로 돌아온다 — 잡아 둔 좌표의 날씨만 다시 받는다. 볼 때마다 위치를 물으면 GPS 가 켜져 기다리게 된다.
    /// 자리를 옮겼으면 UseMyLocationAsync 를 쓴다. 아직 한 번도 안 잡았으면 그쪽으로 넘긴다.
    /// </summary>
    private async Task SelectMyLocationAsync()
    {
        if (_point is null)
        {
            await UseMyLocationAsync();
            return;
        }

        _selectedLocationId = MyLocationId;
        _myError = null;
        _detailError = false;

        _current = null;
        _midTerm = [];
        _trend = [];
        _todayHours = [];

        await FetchPointAsync(quiet: false);
    }

    /// <summary>
    /// 지금 여기의 날씨로 바꾼다 — 위치를 묻고 그 좌표의 날씨를 받는다. 사람이 시킨 자리에서만 부른다.
    /// 화면을 먼저 내 위치로 바꾸고 묻는다 — 브라우저가 10초까지 걸리는 동안 앞 지역 예보가 남으면 고른 것과 보이는 것이 어긋난다.
    /// </summary>
    private async Task UseMyLocationAsync()
    {
        _selectedLocationId = MyLocationId;
        _myError = null;
        _detailError = false;

        // 앞 지역의 상세를 비운다. 주간 예보는 FetchPointAsync 가 다시 채우고, 추이는 내 위치에 없다.
        _current = null;
        _midTerm = [];
        _trend = [];
        _todayHours = [];

        _myBusy = true;
        StateHasChanged();

        try
        {
            var geo = await Geo.LocateAsync();

            if (!geo.Ok)
            {
                // 거절인지, 기기가 못 잡은 것인지, HTTPS 가 아닌지에 따라 할 일이 다르므로 까닭을 그대로 옮긴다.
                var why = string.IsNullOrWhiteSpace(geo.Error)
                    ? "위치를 받지 못했습니다."
                    : geo.Error!;

                // 앞서 잡은 것이 있으면 버리지 않는다 — 실외 GPS 한 번 늦은 것에 날씨까지 사라지면 고장으로 읽힌다.
                // 다만 지난 자리의 것이라는 말을 붙인다. 안 붙이면 옮긴 자리의 날씨를 보고 있는 줄 안다.
                if (_point is null)
                {
                    _myError = why;
                }
                else
                {
                    _myError = $"{why} 앞서 잡은 위치({MyPlaceName})를 그대로 보여 줍니다.";
                }

                return;
            }

            _myLat = geo.Latitude;
            _myLon = geo.Longitude;

            await FetchPointAsync(quiet: false);
        }
        finally
        {
            _myBusy = false;
        }
    }

    /// <summary>
    /// 잡아 둔 좌표의 날씨를 받는다.
    /// </summary>
    /// <param name="quiet">사람이 누르지 않은 길에서는 참 — 실패를 화면에 말하지 않는다.</param>
    private async Task FetchPointAsync(bool quiet)
    {
        try
        {
            // 주간 예보까지 한 번에 받는다. 따로 부르면 서버가 단기예보를 두 번 받는다.
            // 주간은 내 위치를 보고 있을 때만(MyOn) — 조용한 길에서 받으면 안 보는 예보로 기상청을 두 번 더 부른다.

            _point = await Client.GetPointWeatherAsync(_myLat, _myLon, weekly: MyOn);

            // 내 위치를 보고 있을 때만 담는다 — 조용한 길이 덮으면 등록 지역의 주간 예보가 사람이 모르게 바뀐다.
            // 같은 칸을 쓰는 것은 같은 줄(WeekForecastRow)을 그리기 때문이다.

            if (MyOn)
            {
                _midTerm = _point is null ? [] : [.. _point.Weekly.Select(ToMidTerm)];
            }

            if (_point is null && !quiet)
            {
                _myError = "내 위치의 날씨를 받지 못했습니다.";
            }
        }
        catch (ApiException ex)
        {
            _point = null;

            if (MyOn)
            {
                _midTerm = [];
            }

            if (!quiet)
            {
                _myError = $"내 위치의 날씨를 읽지 못했습니다 — {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 지점 주간 예보 하루치를 등록 지역과 같은 모양(MidTermForecast)으로 옮긴다. 두 타입은 사는 모듈이 달라 갈려 있다.
    /// </summary>
    private static MidTermForecast ToMidTerm(PointWeekDayDto day) => new()
    {
        Date = day.Date,
        DayDisplay = day.DayDisplay,
        MinTemp = day.MinTemp,
        MaxTemp = day.MaxTemp,
        AmSky = day.AmSky,
        PmSky = day.PmSky,
        AmPop = day.AmPop,
        PmPop = day.PmPop,
    };

    /// <summary>
    /// 내 위치 주간 예보 아래의 힌트. 중기 구역은 시·군이고 하늘·강수는 도 단위라 사흘 예보와 정밀도가 다르다.
    /// 말하지 않으면 어긋나는 날 둘 중 하나가 틀렸다고 읽힌다.
    /// </summary>
    private string WeekHint => _point?.Region1 is { Length: > 0 } sido ? $"{sido} 기준" : string.Empty;

    /// <summary>
    /// 물음창 없이 위치를 한 번 재어 내 위치 카드를 미리 올린다. 고른 지역은 바꾸지 않는다 — 사람이 안 했는데 바뀌면 고장으로 읽힌다.
    /// 허용한 적이 없으면 QuietAsync 가 재지 않고 물러나므로 이 길로 물음창은 뜨지 않는다.
    /// </summary>
    private async Task TryQuietLocationAsync()
    {
        if (_point is not null)
        {
            return;
        }

        var geo = await Geo.QuietAsync();

        if (!geo.Ok)
        {
            return;
        }

        _myLat = geo.Latitude;
        _myLon = geo.Longitude;

        await FetchPointAsync(quiet: true);

        if (_point is not null)
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// 실황의 기준 시각을 KST 로 적는다 — 서버 표준시가 KST 라는 보장이 없다.
    /// 「관측」이 아니라 우리가 받은 시각(초단기 기준은 정시)이라, 등록 지역 카드의 「관측」과 뜻을 섞지 않는다.
    /// </summary>
    private static string SeenAt(DateTimeOffset at)
        => at.ToOffset(TimeSpan.FromHours(9)).ToString("HH:mm 기준");

    /// <summary>
    /// 사람이 읽는 내 위치 이름. 못 찾았으면 「내 위치」(바다 위 좌표일 수 있다).
    /// </summary>
    private string MyPlaceName => string.IsNullOrWhiteSpace(_point?.Place)
        ? "내 위치"
        : _point!.Place!;

    /// <summary>
    /// 「여기가 맞나」를 되묻는 좌표와 격자. 이름만 보이면 격자가 낸 옆 동네 이름이 틀린 줄 모른다.
    /// </summary>
    private string? MyPlaceHint => _point is null
        ? null
        : FormattableString.Invariant(
            $"위도 {_point.Lat:0.####} · 경도 {_point.Lon:0.####} · 격자 {_point.Nx},{_point.Ny}");

    /// <summary>
    /// 고른 지역의 실황 상세 · 주간 예보 · 추이 재료. 셋을 나란히 부르고, 실패해도 전 지역 카드는 남긴다.
    /// </summary>
    private async Task LoadSelectedAsync()
    {
        _current = null;
        _midTerm = [];
        _trend = [];
        _todayHours = [];
        _detailError = false;

        // 0 은 아직 안 골랐다, 음수는 내 위치다 — 둘 다 여기서 부를 것이 없다.
        if (_selectedLocationId <= 0)
        {
            return;
        }

        try
        {
            var current = Client.GetCurrentWeatherAsync(_selectedLocationId);
            var midTerm = Client.GetMidTermForecastAsync(_selectedLocationId);
            var forecast = Client.GetForecastAsync(_selectedLocationId);

            await Task.WhenAll(current, midTerm, forecast);

            _current = current.Result;
            _midTerm = [.. midTerm.Result];
            _trend = BuildTrend(forecast.Result);
            _todayHours = BuildToday(forecast.Result);
        }
        catch (ApiException)
        {
            // 카드와 마지막 성공 자료는 남기고 상세 영역만 상태를 알린다 — 전 지역까지 지우면 정상과 장애를 구분할 수 없다.
            _detailError = true;
            _current = null;
            _midTerm = [];
            _trend = [];
            _todayHours = [];
        }
    }

    private ButtonRenderStyle MetricStyle(TrendMetric metric)
        => _metric == metric ? ButtonRenderStyle.Primary : ButtonRenderStyle.Secondary;

    // ── 카드 넷을 옆으로 넘긴다 ──────────────────────────────

    /// <summary>
    /// 카드가 놓이는 차례. 단추 줄의 차례이자 스크롤 차례라, 여기서 바꾸면 JS 가 돌려주는 번호가 엉뚱한 항목을 가리킨다.
    /// </summary>
    private static readonly TrendMetric[] Slides =
        [TrendMetric.Temp, TrendMetric.Rain, TrendMetric.Wind, TrendMetric.Humid];

    /// <summary>
    /// 카드 머리 이름. 단위를 여기서 말하므로 값 축에는 제목을 달지 않는다 — 둘 다 달면 좁은 화면에서 두 번 나온다.
    /// </summary>
    private static string SlideTitle(TrendMetric metric) => metric switch
    {
        TrendMetric.Rain => "강수량 (mm)",
        TrendMetric.Wind => "풍속 (m/s)",
        TrendMetric.Humid => "습도 (%)",
        _ => "기온 (°C)",
    };

    /// <summary>카드 머리의 점 색. 그 카드에 그려지는 선·막대와 같은 값이다.</summary>
    private static string SlideHex(TrendMetric metric) => metric switch
    {
        TrendMetric.Rain => RainHex,
        TrendMetric.Wind => WindHex,
        TrendMetric.Humid => HumidHex,
        _ => TempHex,
    };

    /// <summary>카드 넷을 담은 가로 칸. JS 가 이것의 스크롤을 지켜본다.</summary>
    private ElementReference _deck;

    /// <summary>
    /// 지금 그려진 칸을 JS 가 보고 있는가. 칸은 조회나 지역 전환 때 사라졌다 생기므로, 그릴 때마다 칸이 있어야 하는지 다시 보고 없으면 내린다.
    /// </summary>
    private bool _deckWatched;

    private IJSObjectReference? _module;
    private DotNetObjectReference<WeatherDashboard>? _self;

    /// <summary>
    /// 단추를 눌렀을 때 그 카드까지 민다. 넷 다 이미 그려져 있어 차트를 다시 그리지 않는다.
    /// </summary>
    private async Task ShowMetric(TrendMetric metric)
    {
        if (_metric == metric)
        {
            return;
        }

        _metric = metric;

        await SlideToAsync(metric);
    }

    /// <summary>
    /// JS 가 「이 카드가 보인다」고 알려 온다. 스크롤이 잠잠해진 뒤 한 번만 온다(SettleMs) — 매번 왕복하면 넘기는 중 화면이 굳는다.
    /// </summary>
    [JSInvokable]
    public Task OnSlideShown(int index)
    {
        if (index < 0 || index >= Slides.Length || _metric == Slides[index])
        {
            return Task.CompletedTask;
        }

        _metric = Slides[index];

        return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 그 카드로 민다. 못 밀어도 아무 일도 하지 않는다 — 단추 불은 이미 옮겨졌고 카드는 손으로 넘길 수 있다.
    /// </summary>
    private async Task SlideToAsync(TrendMetric metric)
    {
        if (_module is null || !_deckWatched)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("showSlide", _deck, Array.IndexOf(Slides, metric));
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// 떠날 때 지켜보기를 거둔다. 회로가 끊긴 뒤면 예외는 삼킨다.
    /// DotNetObjectReference 는 놓지 않으면 회로가 끝날 때까지 남으므로 어느 길로 나가든 놓는다.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                if (_deckWatched)
                {
                    await _module.InvokeVoidAsync("unwatchDeck", _deck);
                }

                await _module.DisposeAsync();
            }
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _self?.Dispose();
            _self = null;
            _module = null;
            _deckWatched = false;
        }
    }

    // ── 항목별 색 ──
    // 차트 색은 C# 에서 정하고 테마는 브라우저가 바꾼다. 네 값은 밝은·어두운 카드 양쪽에서 재어 고른 것이다.
    // 같은 값을 css 의 점 색과 함께 쓴다 — 둘이 갈라지면 무엇이 맞는지 알 수 없다.
    private const string TempHex = "#d95926";
    private const string RainHex = "#2a78d6";
    private const string WindHex = "#199e70";
    private const string HumidHex = "#9085e9";

    private static readonly System.Drawing.Color TempColor = System.Drawing.ColorTranslator.FromHtml(TempHex);
    private static readonly System.Drawing.Color RainColor = System.Drawing.ColorTranslator.FromHtml(RainHex);
    private static readonly System.Drawing.Color WindColor = System.Drawing.ColorTranslator.FromHtml(WindHex);
    private static readonly System.Drawing.Color HumidColor = System.Drawing.ColorTranslator.FromHtml(HumidHex);

    /// <summary>
    /// 말풍선 값. 소수가 길게 붙으면 읽히지 않으므로 한 자리까지만 남긴다.
    /// </summary>
    private static string TipValue(object? value)
        => value is double d ? d.ToString("0.#") : value?.ToString() ?? "-";

    /// <summary>
    /// 타임라인을 차트 점으로 옮긴다. X 라벨은 날짜가 바뀌는 칸만 M/D, 나머지는 HH시, 실측→예보 경계는 「지금」이다.
    /// 카테고리 축이라 하루를 넘는 자료를 다루면 라벨에 날짜를 넣어야 한다.
    /// </summary>
    private static List<TrendPoint> BuildTrend(IReadOnlyList<WeatherTimelinePoint> rows)
    {
        var list = new List<TrendPoint>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var isNow = row.IsForecast && (i == 0 || !rows[i - 1].IsForecast);
            var isNewDate = i == 0 || row.Date != rows[i - 1].Date;

            var label = isNow
                ? "지금"
                : isNewDate ? DayLabel(row.Date) : HourLabel(row.Time);

            list.Add(new TrendPoint(label, row.Temp, row.Rain, row.WindSpeed, row.Reh));
        }

        return list;
    }

    /// <summary>
    /// 「오늘의 예보」 줄 옆의 범위. 칸이 비면 아무것도 적지 않는다 — 「남은 예보가 없다」 안내와 같은 말이 된다.
    /// </summary>
    private string? TodayHint => _todayHours.Count == 0
        ? null
        : $"{DayLabel(_todayDate)} · {_todayHours[0].Label} ~ {_todayHours[^1].Label}";

    /// <summary>「오늘의 예보」가 말하는 날 (yyyyMMdd, KST).</summary>
    private string _todayDate = string.Empty;

    /// <summary>
    /// 타임라인에서 오늘치 예보 칸만 가른다. 「오늘」은 마지막 실측 칸의 날짜로 정한다 — 서버 시계는 KST 라는 보장이 없다.
    /// 밤늦게 열면 칸이 적거나 비는 것이 맞다. 내일치를 끌어다 채우면 줄 이름과 내용이 어긋난다.
    /// </summary>
    private List<HourPoint> BuildToday(IReadOnlyList<WeatherTimelinePoint> rows)
    {
        _todayDate = rows.LastOrDefault(r => r.IsPast)?.Date
            ?? rows.FirstOrDefault()?.Date
            ?? string.Empty;

        if (_todayDate.Length == 0)
        {
            return [];
        }

        return
        [
            .. rows
                .Where(r => r.IsForecast && r.Date == _todayDate)
                .Select(r => new HourPoint(HourLabel(r.Time), r.Temp, r.Pop, SkyText(r.Sky, r.Pty)))
        ];
    }

    /// <summary>
    /// 하늘 상태 한 낱말. 비·눈이 하늘 상태를 이긴다(구름많음이라 적고 비가 오면 틀린 말이다).
    /// 모르는 코드는 있는 그대로 보여 준다 — 「맑음」으로 삼키면 자료가 어긋난 것을 아무도 모른다.
    /// </summary>
    private static string SkyText(string sky, string pty) => pty switch
    {
        "1" => "비",
        "2" => "비/눈",
        "3" => "눈",
        "4" => "소나기",
        "5" => "빗방울",
        "6" => "빗방울눈날림",
        "7" => "눈날림",
        _ => sky switch
        {
            "1" => "맑음",
            "3" => "구름많음",
            "4" => "흐림",
            _ => string.IsNullOrWhiteSpace(sky) ? "-" : sky,
        },
    };

    /// <summary>yyyyMMdd → M/d. 형식이 어긋나면 그대로 보여 준다.</summary>
    private static string DayLabel(string date)
        => date.Length == 8 && int.TryParse(date[4..6], out var m) && int.TryParse(date[6..8], out var d)
            ? $"{m}/{d}"
            : date;

    /// <summary>HHmm → "HH시".</summary>
    private static string HourLabel(string time)
        => time.Length >= 2 ? $"{time[..2]}시" : time;

    /// <summary>차트 항목. 차례가 단추 줄·쓸기 차례라 값을 섞으면 쓸기가 단추와 어긋난다.</summary>
    private enum TrendMetric { Temp, Rain, Wind, Humid }

    /// <summary>차트 한 점. 습도는 실측 구간에 빈 칸이 있을 수 있어 nullable — DxChart 는 null 을 빈 칸으로 건너뛴다.</summary>
    private sealed record TrendPoint(string Label, double Temp, double? Rain, double WindSpeed, double? Humidity);

    /// <summary>
    /// 「오늘의 예보」 카드 한 장. 강수확률은 초단기에 없어 nullable — 없는 것을 0% 로 적으면 「비 안 온다」로 읽힌다.
    /// </summary>
    private sealed record HourPoint(string Label, double Temp, int? Pop, string Sky);
}
