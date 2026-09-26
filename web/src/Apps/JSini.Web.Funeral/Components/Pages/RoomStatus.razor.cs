using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class RoomStatus
{
    [Inject] private FuneralApi Api { get; set; } = default!;
    [Inject] private DeviceStatusRelay Status { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _name,
        SchSummary.On(_onlyInUse, "사용 중만"),
        SchSummary.On(_onlyAlert, "장비 이상만"),
        _showDates ? SchSummary.Period(_coffinFrom, _coffinTo) : null,
        _showDates ? SchSummary.Period(_burialFrom, _burialTo) : null);

    private string? _buildingId;
    private string? _floorId;
    private bool _onlyInUse;
    private bool _onlyAlert;

    private string? _name;
    private bool _showDates;

    private DateTime? _coffinFrom;
    private DateTime? _coffinTo;
    private DateTime? _burialFrom;
    private DateTime? _burialTo;

    private RoomBoard? _board;

    /// <summary>서버가 준 원본. 조건을 바꿔도 다시 부르지 않게 들고 있는다.</summary>
    private IReadOnlyList<FuneralStatus> _rooms = [];

    /// <summary>장비가 다 안 붙은 호실 수. 요약 줄에 적는다.</summary>
    private int OfflineRooms => _rooms.Count(r => r.DeviceCount > 0 && r.OnlineDeviceCount < r.DeviceCount);

    /// <summary>
    /// 화면에 보일 호실.
    ///
    /// <b>거르기를 서버에 넘기지 않는다.</b> 요약 숫자(전체·빈 호실)는 걸러지기
    /// 전 기준이어야 하기 때문이다 — 서버에서 거르면 「전체 호실 3」처럼 보인다.
    /// </summary>
    private IReadOnlyList<FuneralStatus> Shown
    {
        get
        {
            IEnumerable<FuneralStatus> rows = _rooms;

            if (_onlyInUse)
            {
                rows = rows.Where(r => r.Occupied);
            }

            if (_onlyAlert)
            {
                rows = rows.Where(r => r.DeviceCount > 0 && r.OnlineDeviceCount < r.DeviceCount);
            }

            return [.. rows];
        }
    }

    // ── 스스로 새로 고친다 ──────────────────────────────────
    //
    // 이 화면은 **상황판으로 띄워 둔다.** 사람이 새로 고침을 누르러 오지
    // 않는다는 뜻이다. 옛 화면은 60초 폴링 + 실시간 방송 둘 다 썼고,
    // 여기서는 그것을 그대로 옮긴다 — 폴링은 방송을 놓쳤을 때의 보험이다.

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private Timer? _poll;
    private IDisposable? _statusWatch;
    private IDisposable? _assignmentWatch;
    private DateTime? _lastRefreshAt;
    private bool _backgroundRefreshError;

    /// <summary>
    /// 방송이 몰릴 때 다시 읽기를 묶는 자리. 한 건물의 화면이 동시에 켜지면
    /// 방송이 수십 개 온다 — 그때마다 읽으면 조회가 그만큼 나간다.
    /// </summary>
    private CancellationTokenSource? _debounce;

    protected override async Task OnInitializedAsync()
    {
        _statusWatch = Status.Subscribe((_, _) => ScheduleReload());
        _assignmentWatch = Status.SubscribeAssignments(_ => ScheduleReload());

        _poll = new Timer(_ => ScheduleReload(), null, PollInterval, PollInterval);

        await ReloadAsync();
    }

    /// <summary>
    /// 0.8초 뒤에 다시 읽는다. 그 사이에 또 불리면 시계를 다시 맞춘다.
    ///
    /// <para>
    /// <b>조용히 읽는다</b> — <c>LoadAsync</c> 를 쓰면 안내 줄이 깜빡이고
    /// 「조회 결과가 없습니다」가 스스로 떴다 사라진다. 상황판에서 그 깜빡임은
    /// 사람이 무언가 눌렀다는 신호로 읽힌다.
    /// </para>
    /// </summary>
    private void ScheduleReload()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = new CancellationTokenSource();

        var token = _debounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(800), token);
                await InvokeAsync(ReloadSilentlyAsync);
            }
            catch (OperationCanceledException)
            {
                // 다음 것이 이어받는다.
            }
        }, token);
    }

    /// <summary>안내 줄을 건드리지 않고 자료만 갈아 끼운다.</summary>
    private async Task ReloadSilentlyAsync()
    {
        try
        {
            var board = await Api.GetRoomBoardAsync(
                buildingId: _buildingId,
                floorId: _floorId,
                name: string.IsNullOrWhiteSpace(_name) ? null : _name.Trim(),
                coffinStartDate: _coffinFrom,
                coffinEndDate: _coffinTo,
                burialStartDate: _burialFrom,
                burialEndDate: _burialTo);

            _board = board;
            _rooms = board?.Rooms ?? [];
            _lastRefreshAt = DateTime.Now;
            _backgroundRefreshError = false;
            StateHasChanged();
        }
        catch (ApiException)
        {
            // 마지막 자료는 유지하되, 자동 갱신이 멎었다는 사실은 남겨야
            // 상황판을 믿고 지나가는 일을 막을 수 있다.
            _backgroundRefreshError = true;
            StateHasChanged();
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // **이름을 붙여 넘긴다.** 첫 인자가 `companyId` 라, 자리로 넘기면
        // 건물이 회사 자리에 들어가 조건이 통째로 어긋난다.
        _board = await Api.GetRoomBoardAsync(
            buildingId: _buildingId,
            floorId: _floorId,
            name: string.IsNullOrWhiteSpace(_name) ? null : _name.Trim(),
            coffinStartDate: _coffinFrom,
            coffinEndDate: _coffinTo,
            burialStartDate: _burialFrom,
            burialEndDate: _burialTo);
        _rooms = _board?.Rooms ?? [];
        _lastRefreshAt = DateTime.Now;
        _backgroundRefreshError = false;

        return _rooms.Count;
    }, "조건에 맞는 호실이 없습니다.", "빈소현황을 읽지 못했습니다");

    private Task ClearDatesAsync()
    {
        _coffinFrom = null;
        _coffinTo = null;
        _burialFrom = null;
        _burialTo = null;

        return ReloadAsync();
    }

    /// <summary>
    /// 출상 취소. 그 호실에 고인이 다시 배정된다.
    ///
    /// 되돌아갈 호실에 이미 다른 고인이 있으면 <b>서버가 거절한다</b> —
    /// 화면이 미리 판단하지 않는 이유는, 그 판단이 서버에만 있고 화면이
    /// 따로 하면 둘이 어긋나기 때문이다.
    /// </summary>
    private async Task CancelDepartureAsync(FuneralStatus room)
    {
        if (room.LastDepartedDeceasedId is not { Length: > 0 } id)
        {
            return;
        }

        if (await RunAsync(() => Api.CancelDeceasedDepartureAsync(id),
                           $"故 {room.LastDepartedDeceasedName} 님의 출상을 되돌렸습니다.",
                           "출상을 되돌리지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private static string Gender(string? value) => value switch
    {
        "FEMALE" => "여",
        "MALE" => "남",
        _ => "-",
    };

    private static string Fmt(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    /// <summary>
    /// 시계와 방송 구독을 놓는다.
    ///
    /// <b>빠뜨리면 화면을 닫아도 60초마다 조회가 계속 나간다</b> — 상황판으로
    /// 띄워 두는 화면이라 열고 닫기를 반복하면 그만큼 쌓인다.
    /// </summary>
    public void Dispose()
    {
        _poll?.Dispose();
        _debounce?.Cancel();
        _debounce?.Dispose();
        _statusWatch?.Dispose();
        _assignmentWatch?.Dispose();
    }
}
