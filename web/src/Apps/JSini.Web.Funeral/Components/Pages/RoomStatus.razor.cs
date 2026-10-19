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

    /// <summary>화면에 보일 호실. 거르기는 서버에 넘기지 않는다 — 요약 숫자(전체·빈 호실)는 거르기 전 기준이어야 한다.</summary>
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

    // ── 스스로 새로 고친다 ──
    // 상황판으로 띄워 두는 화면이라 사람이 새로 고치지 않는다. 60초 폴링은 방송을 놓쳤을 때의 보험이다.

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private Timer? _poll;
    private IDisposable? _statusWatch;
    private IDisposable? _assignmentWatch;
    private DateTime? _lastRefreshAt;
    private bool _backgroundRefreshError;

    /// <summary>방송이 몰리면 다시 읽기를 묶는다. 한 건물이 동시에 켜지면 방송이 수십 개 온다.</summary>
    private CancellationTokenSource? _debounce;

    protected override async Task OnInitializedAsync()
    {
        _statusWatch = Status.Subscribe((_, _) => ScheduleReload());
        _assignmentWatch = Status.SubscribeAssignments(_ => ScheduleReload());

        _poll = new Timer(_ => ScheduleReload(), null, PollInterval, PollInterval);

        await ReloadAsync();
    }

    /// <summary>0.8초 뒤에 조용히 다시 읽는다. LoadAsync 를 쓰면 안내 줄이 깜빡여 상황판에서 사람이 무언가 누른 것으로 읽힌다.</summary>
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
            _lastRefreshAt = AppTime.UtcNow;
            _backgroundRefreshError = false;
            StateHasChanged();
        }
        catch (ApiException)
        {
            // 마지막 자료는 유지하되, 자동 갱신이 멎었다는 사실을 남겨 상황판을 믿고 지나가는 일을 막는다.
            _backgroundRefreshError = true;
            StateHasChanged();
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _board = await Api.GetRoomBoardAsync(
            buildingId: _buildingId,
            floorId: _floorId,
            name: string.IsNullOrWhiteSpace(_name) ? null : _name.Trim(),
            coffinStartDate: _coffinFrom,
            coffinEndDate: _coffinTo,
            burialStartDate: _burialFrom,
            burialEndDate: _burialTo);
        _rooms = _board?.Rooms ?? [];
        _lastRefreshAt = AppTime.UtcNow;
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

    /// <summary>출상 취소. 되돌아갈 호실에 다른 고인이 있으면 서버가 거절한다. 화면이 미리 판단하면 두 판단이 어긋나므로 서버에만 둔다.</summary>
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

    /// <summary>시계와 방송 구독을 놓는다. 빠뜨리면 화면을 닫아도 60초 조회가 계속 나간다.</summary>
    public void Dispose()
    {
        _poll?.Dispose();
        _debounce?.Cancel();
        _debounce?.Dispose();
        _statusWatch?.Dispose();
        _assignmentWatch?.Dispose();
    }
}
