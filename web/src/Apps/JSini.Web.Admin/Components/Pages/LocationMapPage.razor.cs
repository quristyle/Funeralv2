using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class LocationMapPage
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>「목록 전체에게」로 한 번에 보낼 수 있는 사람 수. 서버의 받는 사람 상한(NoteRecipientResolver.MaxRecipients)과 같아야 한다 — 크면 넘은 만큼이 조용히 잘린다.</summary>
    private const int MaxBulk = 30;

    /// <summary>지도 판. JS 가 여기에 타일과 점을 붙인다.</summary>
    private ElementReference _stage;

    private IJSObjectReference? _module;
    private IJSObjectReference? _map;

    /// <summary>점을 눌렀을 때 JS 가 되부를 손잡이.</summary>
    private DotNetObjectReference<LocationMapPage>? _self;

    /// <summary>서버가 준 전부. 거르기는 이것에서 한다.</summary>
    private IReadOnlyList<AccountLocationDto> _all = [];

    private string _keyword = string.Empty;
    private bool _reachableOnly;

    /// <summary>지금 고른 사람. 지도의 점과 왼쪽 목록이 같은 값을 본다.</summary>
    private AccountLocationDto? _picked;

    private bool _writing;
    private IReadOnlyList<string>? _writeTo;

    /// <summary>점을 다시 실어야 하나. 그린 뒤에 보낸다 — 첫 진입에는 아직 판이 없을 수 있다.</summary>
    private bool _dirty;

    /// <summary>다시 실을 때 「전체 보기」까지 할 것인가.</summary>
    private bool _refit;

    /// <summary>휴대폰(≤767px)인가. <c>DxLayoutBreakpoint</c> 가 채운다.</summary>
    private bool _isPhone;

    /// <summary>휴대폰에서 지금 선 판. 넓은 화면에서는 쓰이지 않는다.</summary>
    private PhoneView _view = PhoneView.Map;

    /// <summary>감춰져 있던 지도 판이 다시 섰다. 그린 뒤에 알린다 — 이 값을 올리는 때는 아직 CSS 가 판을 펴기 전이라 크기가 0 이다.</summary>
    private bool _reveal;

    /// <summary>걸러 둔 결과. 한 렌더에서 대여섯 번 읽히므로 담아 둔다.</summary>
    private IReadOnlyList<AccountLocationDto>? _shown;

    /// <summary>휴대폰에서 판 둘 중 하나를 감추는 클래스.</summary>
    private string? PhoneCss => _isPhone
        ? $"ad-split--phone ad-split--{(_view == PhoneView.Map ? "map" : "list")}"
        : null;

    /// <summary>휴대폰에서 한 번에 하나씩 서는 판.</summary>
    private enum PhoneView
    {
        Map,
        List,
    }

    /// <summary>조건에 걸러진 사람들. 한 렌더에서 여러 번 읽히므로 담아 두고, 조건이 바뀌는 길이 지나는 Refresh 에서 비운다.</summary>
    private IReadOnlyList<AccountLocationDto> Shown => _shown ??= Sift();

    private IReadOnlyList<AccountLocationDto> Sift()
    {
        var q = _keyword.Trim();

        return [.. _all
            .Where(p => !_reachableOnly || p.CanReceive)
            .Where(p => q.Length == 0
                        || Contains(p.Name, q)
                        || Contains(p.LoginId, q)
                        || Contains(p.Affiliation, q)
                        || Contains(p.Place, q))];
    }

    /// <summary>걸러진 사람 중 쪽지가 닿는 사람들. 「목록 전체에게」가 쓴다.</summary>
    private IReadOnlyList<AccountLocationDto> Reachable => [.. Shown.Where(p => p.CanReceive)];

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword.Trim()),
        SchSummary.On(_reachableOnly, "쪽지가 닿는 사람만"),
        $"{Shown.Count}명");

    /// <summary>「목록 전체에게」 단추의 도움말. 잠겼으면 까닭을 적는다.</summary>
    private string BulkTitle => Reachable.Count switch
    {
        0 => "쪽지가 닿는 사람이 목록에 없습니다.",
        > MaxBulk => $"한 번에 {MaxBulk}명까지 보냅니다. 조건으로 목록을 좁히십시오.",
        _ => "목록에 있는 사람 모두에게 같은 쪽지를 보냅니다.",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetAccountLocationsAsync();

        // 사라진 사람을 고른 채로 두면 그 카드의 쪽지 보내기가 목록에 없는 사람에게 간다.
        if (_picked is { } prev && !_all.Any(p => p.LoginId == prev.LoginId))
        {
            _picked = null;
        }

        // 자료가 바뀔 때만 맞춘다 — 거르기만 해도 맞추면 손으로 옮겨 둔 자리가 되돌아간다.
        Refresh(true);

        return _all.Count;
    }, "위치를 허용한 계정이 없습니다.", "위치 목록을 읽지 못했습니다");

    /// <summary>검색어가 바뀌었다. 치는 즉시 점이 줄어든다.</summary>
    private void Filter(string value)
    {
        _keyword = value ?? string.Empty;
        Refresh(false);
    }

    /// <summary>점을 다시 실어 달라고 적어 둔다. 실제로 싣는 것은 그린 뒤다.</summary>
    private void Refresh(bool refit)
    {
        _shown = null;
        _dirty = true;
        _refit |= refit;
    }

    /// <summary>화면 폭이 경계를 넘었다. 휴대폰을 벗어나면 지도 판이 다시 선다 — 목록 보기로 둔 채 창을 넓히면 감춰진 판이 그 순간 나타나므로 한 번 알린다.</summary>
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

    /// <summary>목록에서 사람을 눌렀다 — 지도로 넘어가며 그 자리로 옮긴다. PickAsync 를 거치지 않는다 — 이미 고른 줄이면 아무 일도 하지 않아 지도를 옮긴 뒤 자리로 돌아오지 않는다.</summary>
    private async Task ShowOnMapAsync(AccountLocationDto row)
    {
        _view = PhoneView.Map;
        _reveal = true;
        _picked = row;

        if (_map is not null)
        {
            await _map.InvokeVoidAsync("focus", row.LoginId);
        }
    }

    /// <summary>카드 목록의 강조. 표와 달리 <b>아이디로 견준다</b> — 조건을
    /// 바꾸면 같은 사람이라도 다른 객체가 온다.</summary>
    private bool IsPicked(AccountLocationDto p) =>
        _picked is { } cur && cur.LoginId == p.LoginId;

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

            await _map.InvokeVoidAsync("setMarkers", Shown.Select(Pin), refit);
        }

        // 감춰져 있던 판이 방금 섰다. `setMarkers` 다음이어야 한다 — 점을
        // 먼저 갈아 끼워 두어야 미뤄 둔 맞추기가 제 점들로 셈한다.
        if (_reveal && _map is not null)
        {
            _reveal = false;
            await _map.InvokeVoidAsync("show");
        }
    }

    /// <summary>지도에서 점을 눌렀다. 빈 곳을 누르면 null 이 온다 — 고른 것을 푸는 길이다. JS 가 되부르므로 StateHasChanged 를 직접 부른다.</summary>
    [JSInvokable]
    public void PickMarker(string? loginId)
    {
        _picked = loginId is { Length: > 0 }
            ? _all.FirstOrDefault(p => p.LoginId == loginId)
            : null;

        StateHasChanged();
    }

    /// <summary>목록에서 골랐다. 지도도 그 자리로 옮긴다 — 화면 밖에 있으면 고른 것이 눈에 안 보인다.</summary>
    private async Task PickAsync(AccountLocationDto? row)
    {
        // 이미 고른 줄이면 아무것도 안 한다 — 지도에서 고른 것이 표에 되비치는데 이 자리가 또 불리면 점을 누를 때마다 지도가 당겨진다.
        if (ReferenceEquals(_picked, row)) return;

        _picked = row;

        if (_map is not null)
        {
            await _map.InvokeVoidAsync("focus", row?.LoginId);
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

    /// <summary>쓰는 창을 연다. 받는 사람은 이미 들어가 있다.</summary>
    private void OpenWrite(IReadOnlyList<string> to)
    {
        _writeTo = to;
        _writing = true;
    }

    /// <summary>지도에 넘길 점 하나. JS 는 이 네 칸만 본다.</summary>
    private object Pin(AccountLocationDto p) => new
    {
        key = p.LoginId,
        lat = p.Lat,
        lon = p.Lon,
        name = Who(p),

        // 쪽지가 안 닿는 사람은 흐리게. 빼지 않는 까닭은 화면 머리말에 있다.
        muted = !p.CanReceive,
    };

    /// <summary>사람을 가리키는 한 마디. 이름이 없으면 아이디를 쓴다.</summary>
    private static string Who(AccountLocationDto p) =>
        string.IsNullOrWhiteSpace(p.Name) ? p.LoginId : p.Name!;

    /// <summary>어디에 있나. 지역 이름이 없으면 좌표를 적는다 — 빈 칸은 자리를 모르는 사람으로 읽힌다.</summary>
    private static string Where(AccountLocationDto p) =>
        string.IsNullOrWhiteSpace(p.Place)
            ? FormattableString.Invariant($"{p.Lat:0.####}, {p.Lon:0.####}")
            : p.Place!;

    /// <summary>자리를 마지막으로 확인한 때. 없으면 모른다고 적는다.</summary>
    private static string When(AccountLocationDto p) =>
        p.LastKnownAt is { } at
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "알 수 없음";

    /// <summary>목록 칸에 적는 짧은 말 — 며칠 전인가. 여기서 사람이 묻는 것은 「이 점을 믿어도 되나」라 날수가 답에 가깝다.</summary>
    private static string Fresh(AccountLocationDto p)
    {
        if (p.LastKnownAt is not { } at) return "—";

        var days = (int)(DateTime.UtcNow - at).TotalDays;

        return days switch
        {
            <= 0 => "오늘",
            1 => "어제",
            < 30 => $"{days}일 전",
            _ => at.ToLocalTime().ToString("yyyy-MM"),
        };
    }

    /// <summary>쪽지 단추의 도움말. 잠겼으면 까닭을 적는다.</summary>
    private static string ReachTitle(AccountLocationDto p) => p.CanReceive
        ? $"{Who(p)} 에게 쪽지를 보냅니다."
        : "앱 푸시도 쪽지 메일도 닿지 않아 쪽지가 도착하지 않습니다.";

    /// <summary>같은 자리를 OpenStreetMap 에서 여는 주소.</summary>
    private static string BigMapUrl(AccountLocationDto p) => FormattableString.Invariant(
        $"https://www.openstreetmap.org/?mlat={p.Lat:0.######}&mlon={p.Lon:0.######}#map=16/{p.Lat:0.######}/{p.Lon:0.######}");

    private static bool Contains(string? text, string needle) =>
        text is not null && text.Contains(needle, StringComparison.OrdinalIgnoreCase);

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
