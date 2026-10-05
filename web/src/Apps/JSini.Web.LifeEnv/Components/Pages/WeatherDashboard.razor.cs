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
    /// 브라우저에게 「지금 어디인가」를 묻는 <b>절차 한 벌</b>. 이 화면은 그중
    /// 재는 것만 쓴다(<see cref="GeoLocator.LocateAsync"/> ·
    /// <see cref="GeoLocator.QuietAsync"/>) — <b>저장은 하지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// 좌표를 서버에 남기는 것은 「내 위치 날씨 <b>알림</b>」을 켜는 일이고
    /// (<c>GeoLocator.SaveAsync</c>), 그 자리는 알림 설정 화면이다. 여기서
    /// 날씨를 한 번 보는 것만으로 알림 설정이 바뀌면 아무도 그것을 예상하지
    /// 못한다 — 화면이 하는 말과 실제로 벌어지는 일이 달라진다.
    /// </remarks>
    [Inject] private GeoLocator Geo { get; set; } = default!;

    private List<WeatherInfo> _weatherList = [];
    private List<WeatherLocation> _locations = [];
    private int _selectedLocationId;

    private WeatherInfo? _current;
    private List<MidTermForecast> _midTerm = [];
    private List<TrendPoint> _trend = [];

    /// <summary>
    /// 오늘 남은 시간대의 예보. 추이 차트와 <b>같은 응답</b>에서 갈라 낸다
    /// (<c>forecast/{id}</c>) — 서버를 한 번 더 부르지 않는다.
    /// </summary>
    private List<HourPoint> _todayHours = [];
    private TrendMetric _metric = TrendMetric.Temp;
    private bool _detailError;

    // ── 내 위치 ──────────────────────────────────────────────
    //
    // 이 화면이 원래 보여 주던 것은 **회사가 등록해 둔 관측 지역**뿐이었다
    // (`ghub.weather_locations`). 그 목록에 없는 곳에 있는 사람 — 출장 중이거나
    // 지사가 없는 도시에 사는 사람 — 은 가장 가까운 등록 지역을 골라 「대충
    // 이쯤이겠지」로 읽어야 했다.
    //
    // 브라우저가 준 좌표 한 쌍으로 **그 지점**의 날씨를 만드는 길이 서버에
    // 이미 있다(`GET life/weather/point`). 알림 설정 화면의 「내 위치 날씨」
    // 미리보기와 발송기가 같은 길을 쓰므로, **여기서 보는 글과 알림으로 오는
    // 글이 같은 재료**다.
    //
    // 그 길에 `weekly=true` 를 붙이면 **주간 예보까지** 온다. 중기예보는 격자로
    // 못 묻고 기상청이 따로 매긴 구역코드로만 묻는데, 서버가 좌표에서 찾은
    // 시·도·시·군으로 그 코드를 골라 준다(`MidTermRegions`). 구역이 시·군 단위라
    // 사흘 예보의 5km 격자보다 성기고, 화면이 그 말을 힌트에 적는다.
    //
    // **예보 추이만 여전히 없다** — 지난 10시간 실측이 수집 이력에서 나오는
    // 것이라 등록 지역에만 있다. 그 줄이 없어지는 것이 맞고, 화면이 그 까닭을
    // 한 줄로 말한다 — 안 말하면 「자료가 빠졌다」로 읽힌다.

    /// <summary>
    /// 지역 고르개에서 <b>내 위치</b>를 가리키는 값.
    /// </summary>
    /// <remarks>
    /// <b>음수여야 한다.</b> 0 은 「아직 안 골랐다」이고(<see cref="OnLocationSelected"/>
    /// 가 그것으로 막는다) 양수는 실제 지역 Id 다. 셋이 겹치면 내 위치를 고른
    /// 것이 어떤 지역을 고른 것으로 읽힌다.
    /// </remarks>
    private const int MyLocationId = -1;

    /// <summary>지금 보고 있는 것이 내 위치인가.</summary>
    private bool MyOn => _selectedLocationId == MyLocationId;

    /// <summary>
    /// 고르개에 얹는 목록 — <b>내 위치가 맨 앞, 그 뒤가 등록 지역</b>이다.
    /// </summary>
    /// <remarks>
    /// <see cref="_locations"/> 를 그대로 두고 따로 만든다. 저쪽은 <b>등록
    /// 지역만</b>이라는 뜻이고, 첫 지역을 기본으로 고르는 자리가 그것을 본다
    /// (<see cref="LoadDataAsync"/>) — 섞어 두면 화면이 열리자마자 위치
    /// 물음창이 튀어나온다.
    /// </remarks>
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
    /// 내 위치를 못 잡았거나 그 지점의 날씨를 못 읽은 까닭.
    /// </summary>
    /// <remarks>
    /// <b>토스트가 아니라 화면에 붙박아 둔다.</b> 이 자리의 실패는 대부분
    /// 「권한을 허용하지 않았다」라 사람이 <b>읽고 무언가를 해야</b> 하는데,
    /// 토스트는 읽기 전에 사라진다.
    /// </remarks>
    private string? _myError;

    /// <summary>추이 차트를 감싼 담장. 여기서 터진 것이 회로를 끌어내리지 않게 막는다.</summary>
    private ErrorBoundary? _chartGuard;

    /// <summary>
    /// <b>손가락으로 보는 화면인가.</b> 참이면 차트의 말풍선과 십자선을
    /// 아예 달지 않는다.
    ///
    /// <para>
    /// 마우스가 있는 화면에서 말풍선은 <b>덧붙는 것</b>이다 — 얹으면 뜨고,
    /// 치우면 사라지고, 다른 손짓과 겹치지 않는다. 손가락뿐인 화면에는
    /// 「얹기」가 없어서 <b>짚어야</b> 뜨는데, 그 짚는 손짓이 곧 카드를 미는
    /// 손짓이다. 차트 위에서 손가락이 움직이기 시작할 때 DevExpress 가 그것을
    /// 말풍선·십자선 추적으로 집으면 <b>브라우저가 스크롤을 시작하지 못한다</b>
    /// (첫 <c>touchmove</c> 는 아직 막을 수 있는 이벤트다). 미는 일을
    /// 브라우저에 맡긴 지금도 다투는 자리는 그대로다.
    /// </para>
    ///
    /// <para>
    /// 더 나쁜 것은 <b>멈추는 것</b>이었다. Blazor Server 에서 이 말풍선은
    /// <b>서버가 그린다</b> — 짚은 점이 바뀔 때마다 회로를 한 번씩 왕복한다.
    /// 손가락으로 21칸을 훑으면 그 왕복이 줄줄이 쌓이고, 화면이 멈춘 듯하다가
    /// 노란 띠와 「새로고침」이 떴다(회로가 내려갔다는 뜻이다). 말풍선을 달지
    /// 않으면 그 왕복 자체가 없다 — 카드를 미는 동안 회로로 가는 것은 <b>붙고
    /// 나서 한 번</b>뿐이다(<see cref="OnSlideShown"/>).
    /// </para>
    ///
    /// <para>
    /// 잃는 것은 손가락 화면의 <b>값 읽기</b>다. 축의 눈금이 남아 대략은
    /// 읽히고, 정확한 숫자가 필요한 사람은 마우스가 있는 화면에서 본다.
    /// </para>
    ///
    /// <para>
    /// 기준은 <c>(hover: none)</c> 하나다. 차트 옆 「옆으로 쓸어 넘깁니다」
    /// 안내가 css 에서 쓰는 것과 <b>같은 질의</b>여서, 안내가 보이는 화면과
    /// 말풍선을 뗀 화면이 어긋나지 않는다. <b>너비로 가르지 않는다</b> —
    /// 창을 좁힌 PC 는 마우스가 있고 넓은 태블릿은 없다.
    /// </para>
    /// </summary>
    private bool _touchOnly;

    protected override Task OnInitializedAsync() => LoadDataAsync();

    /// <summary>
    /// 첫 그림 뒤에 브라우저에게 둘을 묻고 맡긴다 — <b>손가락으로 보는
    /// 화면인가</b>(한 번만)와 <b>지금 어느 카드가 보이는가</b>(칸이 새로
    /// 생길 때마다).
    ///
    /// <para>
    /// 못 물어봐도(회로가 이미 끊겼거나 JS 가 막혔거나) 조용히 넘어간다 —
    /// 그때는 마우스가 있는 것으로 보고 말풍선을 남기고, 카드는 단추와 불이
    /// 어긋날 뿐 <b>손으로 미는 것은 그대로 된다</b>(스크롤은 브라우저가
    /// 한다). 이것 하나 때문에 화면을 세우지 않는다.
    /// </para>
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

                // 위치를 이미 허용해 둔 브라우저면 물음창 없이 한 번 재어
                // 내 위치 카드를 올려 둔다. 허용한 적이 없으면 재어 보지도
                // 않고 물러난다 — 여기서 물음창이 뜰 일은 없다.
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
    /// 전 지역 실황과 지역 목록.
    ///
    /// 실패를 삼키지 않는다 — 게이트웨이가 내려가 있어도 빈 카드가 나오면
    /// 사용자는 「자료가 없다」로 읽는다. 기상 자료는 실제로 비는 때가 있어서
    /// 그 둘을 구분해 주지 않으면 아무도 신고하지 않는다.
    /// </summary>
    private Task LoadDataAsync() => LoadAsync(async () =>
    {
        // 둘을 나란히. 서로 기다릴 이유가 없다.
        var weather = Client.GetLatestWeatherAsync();
        var locations = Client.GetLocationsAsync();

        await Task.WhenAll(weather, locations);

        _weatherList = [.. weather.Result];
        _locations = [.. locations.Result];

        // 고르개에는 내 위치가 한 줄 더 선다. **기본값은 여전히 등록 지역의
        // 첫 줄이다** — 내 위치를 기본으로 두면 화면이 열리자마자 아무도
        // 부르지 않은 위치 물음창이 뜬다.
        _picks = [MyPick, .. _locations];

        if (_selectedLocationId == 0 && _locations.Count > 0)
        {
            _selectedLocationId = _locations[0].Id;
        }

        // 내 위치를 보고 있는 중에 새로고침을 누른 것이면 **위치를 다시 묻지
        // 않는다** — 이미 잰 좌표로 날씨만 새로 받는다. 물음창은 사람이
        // 「내 위치」를 고르거나 단추를 눌렀을 때만 뜬다.
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
    /// 내 위치로 <b>돌아온다</b> — 이미 잡아 둔 좌표의 날씨만 다시 받는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>위치를 다시 묻지 않는다.</b> 카드를 누르거나 고르개에서 고르는 것은
    /// 「그것을 보겠다」이지 「다시 재겠다」가 아니다. 볼 때마다 물으면
    /// 브라우저가 GPS 를 켜고(휴대폰에서는 배터리를 쓴다) 몇 초를 기다리게
    /// 되는데, 격자가 5km 칸이라 그 사이 움직임은 대개 같은 칸 안이다.
    /// </para>
    /// <para>
    /// 자리를 옮겼으면 <b>단추</b>를 누른다(<see cref="UseMyLocationAsync"/>).
    /// 아직 한 번도 안 잡았으면 여기서도 그쪽으로 넘긴다 — 보여 줄 좌표가
    /// 없어서다.
    /// </para>
    /// </remarks>
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
    /// <b>지금 여기</b>의 날씨로 바꾼다 — 위치를 묻고, 그 좌표의 날씨를 받는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>사람이 시킨 자리에서만 부른다</b>(고르개에서 「내 위치」를 고르거나
    /// 단추를 눌렀을 때). <see cref="GeoLocator.LocateAsync"/> 는 브라우저의
    /// 물음창을 띄울 수 있어서, 화면이 뜨자마자 부르면 아무도 부르지 않은
    /// 창이 튀어나온다. 뒤에서 도는 쪽은 <see cref="TryQuietLocationAsync"/> 다.
    /// </para>
    /// <para>
    /// <b>먼저 화면을 내 위치로 바꾸고 나서 묻는다.</b> 브라우저가 위치를
    /// 잡는 데 10초까지 걸릴 수 있는데(geo.js 의 <c>timeout</c>), 그동안 앞
    /// 지역의 예보가 그대로 서 있으면 고른 것과 보이는 것이 어긋난다.
    /// </para>
    /// </remarks>
    private async Task UseMyLocationAsync()
    {
        _selectedLocationId = MyLocationId;
        _myError = null;
        _detailError = false;

        // 앞서 고른 지역의 상세를 비운다. 주간 예보는 아래에서 내 위치 것으로
        // 다시 차고(FetchPointAsync), 추이는 내 위치에 없다.
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
                // 까닭을 그대로 옮긴다 — 권한을 거절한 것인지, 기기가 못 잡은
                // 것인지, HTTPS 가 아니라 막힌 것인지에 따라 할 일이 다르다.
                var why = string.IsNullOrWhiteSpace(geo.Error)
                    ? "위치를 받지 못했습니다."
                    : geo.Error!;

                // **앞서 잡아 둔 것이 있으면 버리지 않는다.** 실외에서 GPS 가
                // 한 번 늦은 것 때문에 보고 있던 날씨까지 사라지면, 사람은
                // 「고장났다」로 읽고 단추를 연달아 누른다. 다만 **그것이 지난
                // 자리의 것**이라는 말을 함께 붙인다 — 안 붙이면 옮긴 자리의
                // 날씨를 보고 있는 줄 안다.
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
    /// 잡아 둔 좌표(<see cref="_myLat"/> · <see cref="_myLon"/>)의 날씨를 받는다.
    /// </summary>
    /// <param name="quiet">
    /// 참이면 <b>실패를 말하지 않는다.</b> 아무도 시키지 않고 뒤에서 도는
    /// 길에서만 참이다 — 사람이 누른 적 없는 일의 실패를 화면에 붙여 두면
    /// 무엇을 하라는 말인지 알 수 없다.
    /// </param>
    private async Task FetchPointAsync(bool quiet)
    {
        try
        {
            // **주간 예보까지 한 번에 받는다.** 따로 부르면 왕복이 하나 더 붙고,
            // 서버가 단기예보를 두 번 받게 된다 — 주간 예보의 앞쪽 이틀이 바로
            // 그 단기예보에서 나오기 때문이다.
            //
            // **지금 내 위치를 보고 있을 때만 받는다**(`MyOn`). 이 길은 아무도
            // 시키지 않은 자리에서도 돈다(TryQuietLocationAsync — 카드 한 장만
            // 올리고 아래는 등록 지역의 것을 그대로 둔다). 거기서까지 받으면
            // 아무도 안 보는 주간 예보 때문에 서버가 기상청을 두 번 더 부른다.
            _point = await Client.GetPointWeatherAsync(_myLat, _myLon, weekly: MyOn);

            // **내 위치를 보고 있을 때만 담는다.** 이 칸은 지금 고른 것의 주간
            // 예보이고, 조용한 길에서 덮어쓰면 **등록 지역을 보고 있는 화면의
            // 주간 예보가 내 위치 것으로 바뀐다** — 사람이 아무것도 안 했는데.
            //
            // 등록 지역과 같은 칸을 쓰는 까닭은 화면이 같은 줄(WeekForecastRow)을
            // 그리기 때문이다. 담는 자리를 갈라 두면 둘이 다른 모양으로 갈라진다.
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
    /// 한 지점의 주간 예보 하루치를 <b>등록 지역과 같은 모양</b>으로 옮긴다.
    /// </summary>
    /// <remarks>
    /// 두 타입이 갈려 있는 까닭은 사는 곳이 달라서다 — <see cref="PointWeekDayDto"/>
    /// 는 알림 설정 화면도 쓰는 <c>JSini.Web.Models</c> 에 있고,
    /// <see cref="MidTermForecast"/> 는 이 모듈의 것이다. 칸은 하나도 다르지 않다.
    /// </remarks>
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
    /// 내 위치 주간 예보 밑에 붙는 한 줄 — <b>이 숫자가 어느 구역의 것인가</b>.
    /// </summary>
    /// <remarks>
    /// 위 사흘 예보는 5km 격자라 「내가 선 자리」에 가깝지만, 중기예보 구역은
    /// 시·군 단위이고 하늘 상태·강수확률은 아예 도(道) 단위다. 그 말을 안 적으면
    /// 사람은 두 줄을 같은 정밀도로 읽고, 어긋나는 날에 <b>둘 중 하나가
    /// 틀렸다</b>고 생각한다.
    /// </remarks>
    private string WeekHint => _point?.Region1 is { Length: > 0 } sido ? $"{sido} 기준" : string.Empty;

    /// <summary>
    /// <b>물음창 없이</b> 위치를 한 번 재어 카드 한 장을 미리 올려 둔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 위치 권한을 이미 허용한 브라우저는 <c>getCurrentPosition</c> 을 물음창
    /// 없이 돌려준다. 그 사람에게는 화면이 열릴 때부터 실황 카드 줄 맨 앞에
    /// 내 위치가 서 있는 것이 맞다 — <b>고를 것이 있다는 사실 자체</b>를
    /// 단추 글자보다 카드가 먼저 알린다.
    /// </para>
    /// <para>
    /// <b>고른 것을 바꾸지 않는다.</b> 카드만 올리고 아래 상세는 그대로 둔다 —
    /// 사람이 아무것도 안 했는데 보던 지역이 바뀌면 그것은 고장으로 읽힌다.
    /// </para>
    /// <para>
    /// 허용한 적이 없으면 <see cref="GeoLocator.QuietAsync"/> 가 재어 보지도
    /// 않고 물러난다(<see cref="GeoResult.Skipped"/>). 이 길로는 물음창이
    /// 뜨지 않는다.
    /// </para>
    /// </remarks>
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
    /// 한 지점 실황이 <b>언제 것인가</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>KST 로 옮겨 적는다.</b> 서버가 주는 값은 UTC 고(<c>DateTimeOffset.UtcNow</c>),
    /// 이 화면을 그리는 것은 브라우저가 아니라 서버인데 그 서버의 표준시가
    /// KST 라는 보장이 없다 — 그대로 찍으면 아홉 시간 어긋난 시각이 나온다.
    /// 「오늘의 예보」가 서버 시계를 안 쓰는 것과 같은 까닭이다
    /// (<see cref="BuildToday"/>).
    /// </para>
    /// <para>
    /// <b>「관측」이 아니라 「기준」이다.</b> 이 값은 기상청이 잰 시각이 아니라
    /// <b>우리가 받아 온 시각</b>이다(초단기 실황의 기준 시각은 정시라 최대 한
    /// 시간 앞선다). 옆에 선 등록 지역 카드의 「관측」은 수집 이력에 찍힌 진짜
    /// 시각이라, 같은 말을 쓰면 둘이 같은 뜻인 줄로 읽힌다.
    /// </para>
    /// </remarks>
    private static string SeenAt(DateTimeOffset at)
        => at.ToOffset(TimeSpan.FromHours(9)).ToString("HH:mm 기준");

    /// <summary>
    /// 내 위치를 사람이 읽는 이름으로. 이름을 못 찾았으면
    /// (좌표가 바다 위일 수도 있다) 「내 위치」로 둔다 — 빈 제목보다 낫다.
    /// </summary>
    private string MyPlaceName => string.IsNullOrWhiteSpace(_point?.Place)
        ? "내 위치"
        : _point!.Place!;

    /// <summary>
    /// 「여기가 맞나」를 되묻는 줄 — 좌표와 기상청 격자.
    /// </summary>
    /// <remarks>
    /// 이름만 보여 주면 <b>틀렸을 때 틀린 줄 모른다.</b> 격자가 5km 칸이라
    /// 옆 동네 이름이 나오는 일이 있는데, 좌표가 함께 있으면 사람이 그것을
    /// 지도에 찍어 확인할 수 있다.
    /// </remarks>
    private string? MyPlaceHint => _point is null
        ? null
        : FormattableString.Invariant(
            $"위도 {_point.Lat:0.####} · 경도 {_point.Lon:0.####} · 격자 {_point.Nx},{_point.Ny}");

    /// <summary>
    /// 고른 지역의 실황 상세 · 주간 예보 · 예보 추이(차트 재료).
    ///
    /// 셋을 나란히 부른다. 실패해도 위 카드는 그대로 둔다 — 한 지역의 예보를
    /// 못 읽었다고 전 지역 실황까지 사라지면 이 화면을 여는 이유가 없어진다.
    /// </summary>
    private async Task LoadSelectedAsync()
    {
        _current = null;
        _midTerm = [];
        _trend = [];
        _todayHours = [];
        _detailError = false;

        // 0 은 「아직 안 골랐다」, 음수는 내 위치다(MyLocationId). 둘 다 여기서
        // 부를 것이 없다 — 내 위치는 <see cref="FetchPointAsync"/> 가 맡는다.
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
            // 전 지역 카드까지 지우면 정상 자료와 장애를 구분할 수 없으므로
            // 카드와 마지막 성공 자료는 남기고, 상세 영역만 상태를 알린다.
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
    /// 카드가 놓이는 차례. <b>이것이 곧 단추 줄의 차례이자 스크롤 차례다</b> —
    /// 여기서 섞으면 JS 가 돌려주는 번호가 엉뚱한 항목을 가리킨다.
    /// </summary>
    private static readonly TrendMetric[] Slides =
        [TrendMetric.Temp, TrendMetric.Rain, TrendMetric.Wind, TrendMetric.Humid];

    /// <summary>
    /// 카드 머리에 적는 이름. <b>단위를 여기서 말하므로 값 축에는 제목을 달지
    /// 않는다</b> — 둘 다 달면 좁은 화면에서 같은 말이 두 번 나온다.
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
    /// 지금 그려진 칸을 JS 가 보고 있는가.
    ///
    /// <para>
    /// 칸은 <b>사라졌다 다시 생긴다</b> — 조회 중에는 자리에 도는 표시가 들어
    /// 앉고, 지역을 바꾸면 자료가 비는 동안 통째로 빠진다. 그때마다 새 DOM
    /// 요소라 옛 감시는 같이 없어지므로, 그릴 때마다 「칸이 있어야 하는가」를
    /// 다시 보고 없으면 이 값을 내린다.
    /// </para>
    /// </summary>
    private bool _deckWatched;

    private IJSObjectReference? _module;
    private DotNetObjectReference<WeatherDashboard>? _self;

    /// <summary>
    /// 단추를 눌렀을 때. <b>그 카드까지 밀어 준다</b> — 손으로 쓸어 넘긴 것과
    /// 같은 자리에 서고, 차트를 다시 그리는 일은 없다(넷 다 이미 그려져 있다).
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
    /// JS 가 「이 카드가 보인다」고 알려 올 때. 단추 불을 그쪽으로 옮긴다.
    ///
    /// <para>
    /// 스크롤이 <b>잠잠해진 뒤에 한 번만</b> 온다(weather-trend.js 의
    /// <c>SettleMs</c>). 손가락을 따라오는 동안 매번 왕복하면 넘기는 중에
    /// 화면이 굳는다 — 말풍선이 그래서 문제였다.
    /// </para>
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
    /// 그 카드로 민다. <b>못 밀어도 아무 일도 하지 않는다</b> — 단추 불은 이미
    /// 옮겨졌고, 카드는 손으로 밀어 넘길 수 있다.
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
    /// 떠날 때 지켜보기를 거둔다.
    ///
    /// <para>
    /// 회로가 이미 끊긴 뒤라면 부를 곳이 없다 — 그때 나는 예외는 삼킨다.
    /// 브라우저 쪽 듣는 이는 화면이 통째로 사라질 때 같이 없어진다.
    /// <see cref="DotNetObjectReference{T}"/> 는 <b>놓지 않으면 이 화면이
    /// 회로가 끝날 때까지 안 지워지므로</b> 어느 길로 나가든 놓는다.
    /// </para>
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

    /*  ── 항목별 색 ──────────────────────────────────────────

        차트 색은 여기 C# 에서 정해지는데 **테마는 브라우저가 바꾼다**
        (theme.js 가 `data-theme` 을 세운다 — 서버 쪽에는 그 신호가 없다).
        그래서 한쪽 바탕에만 맞춘 색을 쓰면 다른 쪽에서 묻힌다.

        네 값은 **밝은 카드(#ffffff)와 어두운 카드(#1e3048) 양쪽에서 재어**
        고른 것이다 — 밝기 띠(OKLCH L)·채도 바닥·바탕 대비(WCAG ≥ 3:1)
        셋을 두 바탕에서 모두 통과한다. 색을 바꿀 일이 생기면 두 바탕에서
        다시 재고 바꾼다.

        카드 넷이 한 줄에 놓이면서 **네 색이 한 화면에 함께 걸리게 됐다** —
        넘기는 중에는 두 색이 나란히 보인다. 그래도 색이 뜻을 혼자 지지
        않는다: 카드마다 제목이 단위까지 적어 놓았고(「기온 (°C)」) 값 축의
        눈금이 다르다. 색은 「지금 넘어가고 있다」를 눈에 먼저 알리는 몫이다.

        같은 값을 글자(`…Hex`)로도 둔다 — 선은 DevExpress 가 칠하고 카드
        제목 옆의 점은 css 가 칠하는데, 둘이 갈라지면 무엇이 맞는지 알 수
        없다. 한 곳에서 꺼내 쓴다. */
    private const string TempHex = "#d95926";
    private const string RainHex = "#2a78d6";
    private const string WindHex = "#199e70";
    private const string HumidHex = "#9085e9";

    private static readonly System.Drawing.Color TempColor = System.Drawing.ColorTranslator.FromHtml(TempHex);
    private static readonly System.Drawing.Color RainColor = System.Drawing.ColorTranslator.FromHtml(RainHex);
    private static readonly System.Drawing.Color WindColor = System.Drawing.ColorTranslator.FromHtml(WindHex);
    private static readonly System.Drawing.Color HumidColor = System.Drawing.ColorTranslator.FromHtml(HumidHex);

    /// <summary>
    /// 말풍선에 쓰는 값. 소수점이 길게 붙으면(18.200000000000003) 숫자가
    /// 읽히지 않으므로 한 자리까지만 남긴다.
    /// </summary>
    private static string TipValue(object? value)
        => value is double d ? d.ToString("0.#") : value?.ToString() ?? "-";

    /// <summary>
    /// 타임라인을 차트 점으로 옮긴다.
    ///
    /// X 라벨 규칙은 옛 화면 그대로다 — 날짜가 바뀌는 칸은 M/D, 나머지는 HH시.
    /// 실측→예보가 갈리는 칸만 「지금」으로 바꾼다(옛 NOW 점선 자리).
    /// 타임라인이 21시간이라 시각 라벨이 겹칠 일이 없다 — 카테고리 축은
    /// 같은 라벨을 한 칸으로 합치므로, 하루를 넘는 자료를 다루게 되면
    /// 라벨에 날짜를 함께 넣어야 한다.
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
    /// 「오늘의 예보」 줄 옆에 적는 말. <b>어느 날의 몇 시부터 몇 시까지인가</b> 하나다.
    ///
    /// <para>
    /// 칸이 비면 아무것도 적지 않는다 — 그 자리에는 「오늘 남은 시간대의
    /// 예보가 아직 없습니다」가 이미 서 있고, 빈 범위를 덧붙이면 같은 말이
    /// 두 번 나온다.
    /// </para>
    /// </summary>
    private string? TodayHint => _todayHours.Count == 0
        ? null
        : $"{DayLabel(_todayDate)} · {_todayHours[0].Label} ~ {_todayHours[^1].Label}";

    /// <summary>「오늘의 예보」가 말하는 날 (yyyyMMdd, KST).</summary>
    private string _todayDate = string.Empty;

    /// <summary>
    /// 타임라인에서 <b>오늘치 예보 칸만</b> 갈라 낸다.
    ///
    /// <para>
    /// 「오늘」은 <b>자료가 정한다</b> — 마지막 실측 칸의 날짜다. 서버의 시계를
    /// 쓰지 않는 까닭은, 이 화면을 그리는 것이 브라우저가 아니라 서버이고 그
    /// 서버의 표준시가 KST 라는 보장이 없어서다. 타임라인의 날짜는 이미 KST 로
    /// 적혀 온다(<c>WeatherEndpoints</c> 의 <c>Kst.FromUtc</c>).
    /// </para>
    ///
    /// <para>
    /// 실측이 하나도 없으면 첫 칸의 날짜로 본다. 그것도 없으면 빈 줄이다.
    /// </para>
    ///
    /// <para>
    /// 밤늦게 열면 <b>몇 칸 안 남거나 아예 비는 것이 맞다</b> — 오늘이 끝나
    /// 가는 것이지 자료가 빠진 것이 아니고, 그때 내일치를 끌어다 채우면
    /// 줄 이름과 내용이 어긋난다.
    /// </para>
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
    /// 하늘 상태 한 낱말. <b>비·눈이 하늘 상태를 이긴다</b> — 「구름많음」이라
    /// 적어 두고 비가 오면 그 칸은 틀린 말을 한 것이다(서버의
    /// <c>PointWeatherService</c> 도 같은 차례로 고른다).
    ///
    /// <para>
    /// 코드는 기상청 것이다 — PTY 0 없음 · 1 비 · 2 비/눈 · 3 눈 · 4 소나기 ·
    /// 5 빗방울 · 6 빗방울눈날림 · 7 눈날림, SKY 1 맑음 · 3 구름많음 · 4 흐림.
    /// 모르는 코드가 오면 <b>있는 그대로 보여 준다</b> — 「맑음」으로 삼키면
    /// 자료가 어긋난 것을 아무도 모른다.
    /// </para>
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

    /// <summary>yyyyMMdd → "M/d". 형식이 어긋난 값은 그대로 보여 준다 — 라벨 하나 때문에 화면을 세우지 않는다.</summary>
    private static string DayLabel(string date)
        => date.Length == 8 && int.TryParse(date[4..6], out var m) && int.TryParse(date[6..8], out var d)
            ? $"{m}/{d}"
            : date;

    /// <summary>HHmm → "HH시".</summary>
    private static string HourLabel(string time)
        => time.Length >= 2 ? $"{time[..2]}시" : time;

    /// <summary>차트에 그릴 항목. <b>차례가 단추가 놓인 차례이자 쓸기 차례다</b> — 값을 섞으면 쓸기가 단추 줄과 어긋난다.</summary>
    private enum TrendMetric { Temp, Rain, Wind, Humid }

    /// <summary>차트 한 점. 습도는 실측 구간에 빈 칸이 있을 수 있어 nullable — DxChart 는 null 을 빈 칸으로 건너뛴다.</summary>
    private sealed record TrendPoint(string Label, double Temp, double? Rain, double WindSpeed, double? Humidity);

    /// <summary>
    /// 「오늘의 예보」 카드 한 장. 강수확률은 <b>초단기 예보에 없어서</b>
    /// nullable 이다 — 없는 것을 0% 로 적으면 「비 안 온다」로 읽힌다.
    /// </summary>
    private sealed record HourPoint(string Label, double Temp, int? Pop, string Sky);
}
