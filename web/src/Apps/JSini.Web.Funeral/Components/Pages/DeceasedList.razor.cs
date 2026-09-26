using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class DeceasedList
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _keyword,
        SchSummary.NameOf(Statuses, s => s.Code, s => s.Name, _status, string.Empty),
        _showDates ? SchSummary.Period(_enterFrom, _enterTo) : null,
        _showDates ? SchSummary.Period(_funeralFrom, _funeralTo) : null);

    /// <summary>
    /// 주소로 받은 검색어 (<c>?keyword=</c>).
    ///
    /// 빈소현황 카드에서 「고인 자료」로 건너올 때 그 고인의 이름을 실어 보낸다.
    /// 안 받으면 목록 맨 앞으로 떨어져 다시 찾아야 하고, 그러면 링크를 걸어 둔
    /// 뜻이 없다.
    /// </summary>
    [Parameter, SupplyParameterFromQuery(Name = "keyword")]
    public string? Keyword { get; set; }

    private sealed record StatusOption(string Code, string Name);

    /// <summary>
    /// 서버가 받는 상태값. 화면에 보이는 배지와 같은 것을 쓴다 —
    /// 두 벌로 두면 하나를 고칠 때 다른 하나를 빠뜨린다.
    /// </summary>
    private static readonly StatusOption[] Statuses =
    [
        new("FUNERAL_IN_PROGRESS", "진행 중"),
        new("FUNERAL_DEPARTURE_COMPLETED", "발인 완료"),
        new("COMPLETED", "종료"),
    ];

    private string? _buildingId;
    private string? _floorId;
    private string? _roomId;
    private string? _status;

    private bool _showDates;
    private DateTime? _enterFrom;
    private DateTime? _enterTo;
    private DateTime? _funeralFrom;
    private DateTime? _funeralTo;

    private string? _keyword;

    private bool _editing;
    private string? _editingId;
    private IReadOnlyList<Deceased> _rows = [];
    private IReadOnlyList<Room> _available = [];

    /// <summary>줄마다 고른 이동 대상. 하나로 두면 한 줄에서 고른 값이 다른 줄에도 뜬다.</summary>
    private readonly Dictionary<string, string?> _targets = new(StringComparer.Ordinal);

    /// <summary>
    /// 주소가 바뀌어도 다시 읽는다. <c>OnInitializedAsync</c> 만 걸면 같은
    /// 화면에서 검색어만 바뀔 때 목록이 그대로다.
    /// </summary>
    protected override Task OnParametersSetAsync()
    {
        if (!string.Equals(_appliedKeyword, Keyword, StringComparison.Ordinal))
        {
            _appliedKeyword = Keyword;
            _keyword = Keyword;
        }

        return ReloadAsync();
    }

    /// <summary>마지막으로 반영한 주소 검색어. 사용자가 칸을 고친 것을 덮지 않으려고 둔다.</summary>
    private string? _appliedKeyword;

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 서버가 받는 이름 그대로 적는다. 모르는 이름은 조용히 무시되므로
        // (오류가 아니다) 여기서 틀리면 화면만 봐서는 알 수 없다.
        var query = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(_keyword)) query["name"] = _keyword.Trim();
        if (!string.IsNullOrWhiteSpace(_buildingId)) query["buildingId"] = _buildingId;
        if (!string.IsNullOrWhiteSpace(_floorId)) query["floorId"] = _floorId;
        if (!string.IsNullOrWhiteSpace(_roomId)) query["roomId"] = _roomId;
        if (!string.IsNullOrWhiteSpace(_status)) query["status"] = _status;

        Put(query, "roomEnterStartDate", _enterFrom);
        Put(query, "roomEnterEndDate", _enterTo);
        Put(query, "funeralStartDate", _funeralFrom);
        Put(query, "funeralEndDate", _funeralTo);

        // 목록과 빈 호실을 나란히. 이동 드롭다운이 바로 채워져 있어야 한다.
        var rows = Api.GetDeceasedListAsync(query);
        var rooms = Api.GetAvailableRoomsAsync();

        await Task.WhenAll(rows, rooms);

        _rows = rows.Result;
        _available = rooms.Result;
        _targets.Clear();

        return _rows.Count;
    }, "조건에 맞는 고인 자료가 없습니다.", "고인 목록을 읽지 못했습니다");

    /// <summary>
    /// 날짜 조건 하나. <b>불변 형식으로 적는다</b> — 지역 형식(<c>ToString()</c>)
    /// 으로 보내면 한국어 환경에서 「2026-09-06 오후 9:00:00」이 되어 서버가 못 읽는다.
    /// </summary>
    private static void Put(Dictionary<string, string?> query, string key, DateTime? value)
    {
        if (value is { } at)
        {
            query[key] = at.ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }

    private Task ClearDatesAsync()
    {
        _enterFrom = null;
        _enterTo = null;
        _funeralFrom = null;
        _funeralTo = null;

        return ReloadAsync();
    }

    private void OpenNew()
    {
        _editingId = null;
        _editing = true;
    }

    private void OpenEdit(Deceased d)
    {
        _editingId = d.Id;
        _editing = true;
    }

    private string? GetTarget(string id) => _targets.GetValueOrDefault(id);

    private void SetTarget(string id, string? value) => _targets[id] = value;

    private async Task MoveAsync(Deceased d)
    {
        var target = GetTarget(d.Id);

        if (string.IsNullOrWhiteSpace(target))
        {
            Say("옮길 호실을 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.MoveDeceasedRoomAsync(d.Id, target),
                $"{d.Name} 님을 옮겼습니다.", "호실을 옮기지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 발인 처리.
    ///
    /// 호실이 비고 현황판에서 내려간다. <b>되돌릴 수 있다</b> —
    /// 그래서 확인 창을 따로 띄우지 않는다. 되돌릴 수 없는 일이었다면
    /// 한 번 더 물었을 것이다.
    /// </summary>
    private async Task DepartAsync(Deceased d)
    {
        if (await RunAsync(() => Api.DepartDeceasedAsync(d.Id),
                           $"{d.Name} 님을 발인 처리했습니다.", "발인 처리를 하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>발인 취소. 호실이 다시 차고 현황판에 올라온다.</summary>
    private async Task CancelDepartureAsync(Deceased d)
    {
        if (await RunAsync(() => Api.CancelDeceasedDepartureAsync(d.Id),
                           $"{d.Name} 님의 발인을 되돌렸습니다.", "되돌리지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 삭제.
    ///
    /// 발인 처리와 다르다 — 기록이 사라진다. 과금·호실 이력이 이 고인을
    /// 가리키고 있으면 서버가 막는다.
    /// </summary>
    private async Task DeleteAsync(Deceased d)
    {
        if (await RunAsync(() => Api.DeleteDeceasedAsync(d.Id),
                           $"{d.Name} 님의 기록을 지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>발인이 끝난 상태인가.</summary>
    private static bool IsDeparted(string? status) =>
        status is "FUNERAL_DEPARTURE_COMPLETED" or "COMPLETED";

    private static string StatusText(string? status) => status switch
    {
        "FUNERAL_IN_PROGRESS" => "장례 중",
        "FUNERAL_DEPARTURE_COMPLETED" => "발인 완료",
        "COMPLETED" => "종료",
        _ => status ?? "-",
    };

    private static string StatusClass(string? status) => status switch
    {
        "FUNERAL_IN_PROGRESS" => "jsini-badge--warn",
        "FUNERAL_DEPARTURE_COMPLETED" or "COMPLETED" => "jsini-badge--on",
        _ => "",
    };
}
