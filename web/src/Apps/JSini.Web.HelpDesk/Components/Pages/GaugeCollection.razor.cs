using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevExpress.Blazor;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class GaugeCollection
{
    [Inject] private OadrApi Oadr { get; set; } = default!;

    /// <summary>지연 몇 분부터 「중지」로 보는가.</summary>
    private const int StoppedMinutes = 5;

    /// <summary>60초 자동 조회. 스위치를 켠 동안에만 돈다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(60);

    private ConfirmDialog? _confirm;

    private IReadOnlyList<Gauge> _gauges = [];
    private IReadOnlyList<GaugeNode> _nodes = [];

    private GaugeNode? _node;
    private Gauge? _gauge;

    /// <summary>지금 보고 있는 액션의 프로시저 이름. 표 머리에 그대로 적는다.</summary>
    private string _action = Basic;

    /// <summary>`P_QURI_HOUR` 이 준 요약. 액션이 무엇이든 이 줄은 따로 읽는다.</summary>
    private HourRow? _hour;

    private DataTable _rows = JsonTable.Empty;
    private DataTable _replicaRows = JsonTable.Empty;

    private string? _keyword;
    private Grouping _group = Grouping.Section;
    private bool _onlyActive = true;
    private bool _excludeMaintenance = true;
    private bool _onlyReplicated;
    private bool _auto;

    private const string Basic = "P_QURI_HOUR";

    // ── 왼쪽 나무 ───────────────────────────────────────────

    private enum Grouping { None, Section, Customer }

    private sealed record GroupMode(string Text, Grouping Value);

    private static readonly IReadOnlyList<GroupMode> GroupModes =
    [
        new("전체", Grouping.None),
        new("구역", Grouping.Section),
        new("고객", Grouping.Customer),
    ];

    /// <summary>나무의 줄 하나. 묶음 줄과 계량기 줄이 같은 형이다 — CommTree(DxTreeList)는 자식 목록을 한 가지 형으로만 받는다.</summary>
    public sealed class GaugeNode
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;

        /// <summary>묶음 줄에만 붙는 곁말. 고객으로 묶으면 회사 이름이 온다.</summary>
        public string? Extra { get; init; }

        public bool IsGroup { get; init; }
        public Gauge? Gauge { get; init; }
        public List<GaugeNode> Children { get; init; } = [];

        /// <summary>5분 넘게 안 들어오고 있다.</summary>
        public bool Delayed => Gauge is { LastMgap: > StoppedMinutes };

        /// <summary>다른 계량기가 이것을 복제 대상으로 가리킨다.</summary>
        public bool ReplicaTarget { get; init; }
    }

    private string ListHint => _gauges.Count == 0 ? string.Empty : $"총 {_gauges.Count}대";

    private bool HasReplica => !string.IsNullOrWhiteSpace(_gauge?.DstMcId);

    /// <summary>수집중인가. 프로시저가 준 run_state 는 쓰지 않는다.</summary>
    private bool Running => (_hour?.LastMgap ?? _gauge?.LastMgap ?? 0) <= StoppedMinutes;

    /// <summary>수집 서버. 요약을 아직 못 읽었으면 장비의 포트로 대신한다.</summary>
    private string Server => _hour?.ComSrv is { Length: > 0 } srv ? srv : (_gauge?.ComPort ?? "-");

    // ── 액션 ────────────────────────────────────────────────

    /// <summary>액션 단추 하나. Writes 가 켜진 것은 되돌릴 수 없다 — 누르기 전에 한 번 묻는다.</summary>
    private sealed record GaugeAction(
        string Text, string Procedure, ButtonRenderStyle On, bool Writes = false);

    private static readonly IReadOnlyList<GaugeAction> Actions =
    [
        new("기본", Basic, ButtonRenderStyle.Dark),
        new("5분H", "P_QURI_HOUR_Last5", ButtonRenderStyle.Success),
        new("5분M", "P_QURI_MIN_Last5MIN", ButtonRenderStyle.Info),
        new("실시간", "P_QURI_REAL", ButtonRenderStyle.Warning),
        new("Peak", "P_QURI_PEAK", ButtonRenderStyle.Danger),
        new("Peak RT", "P_QURI_PEAK_RUNTIME", ButtonRenderStyle.Danger),
        new("ADJ재계산", "P_QURI_ADJ_MC_RECALC", ButtonRenderStyle.Secondary, Writes: true),
    ];

    protected override Task OnInitializedAsync() => ReloadAsync();

    // ── 목록 ────────────────────────────────────────────────

    /// <summary>계량기 목록을 읽고 나무를 세운다. 고른 것이 없으면 처음 걸러진 하나를 저절로 고른다 — 빈 오른쪽 판으로 열면 사람이 「고장인가」를 먼저 묻는다.</summary>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var list = await Oadr.ExecuteProcedureAsync<List<Gauge>>("P_QURI_MC") ?? [];

        // 복제 대상으로 지목된 계량기를 미리 모은다 — 목록 줄에 표시를 달고, 「복제계측기」 거르개가 양쪽을 남기기 위해서다.
        var targets = list
            .Select(g => g.DstMcId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _gauges = [.. list];
        Rebuild(targets);

        if (_gauge is null)
        {
            var first = _nodes.SelectMany(Leaves).FirstOrDefault();
            if (first is not null)
            {
                await PickAsync(first);
            }
        }
        else
        {
            await FetchAsync();
        }

        return _gauges.Count;
    }, "계량기 목록이 비어 있습니다.", "계량기 목록을 읽지 못했습니다");

    private static IEnumerable<GaugeNode> Leaves(GaugeNode node) =>
        node.IsGroup ? node.Children : [node];

    /// <summary>거르고 묶어 나무를 다시 세운다. 서버에 다시 묻지 않는다 — 목록이 작아 브라우저 안에서 끝내는 것이 빠르다.</summary>
    private void Rebuild(HashSet<string>? targets = null)
    {
        _replicaTargets = targets ?? _replicaTargets;

        var search = _keyword?.Trim() ?? string.Empty;

        var shown = _gauges.Where(g =>
            (!_onlyActive || g.UseFlag == "Y")
            && (!_excludeMaintenance || g.MaintenanceYn != "Y")
            && (!_onlyReplicated || !string.IsNullOrWhiteSpace(g.DstMcId) || _replicaTargets.Contains(g.McId))
            && Matches(g, search));

        _nodes = _group == Grouping.None
            ? [.. shown.Select(Leaf).OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase)]
            : [.. shown
                .GroupBy(g => (_group == Grouping.Section ? g.Section : g.CustId) ?? "기타", StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GaugeNode
                {
                    Key = $"group-{g.Key}",
                    Label = g.Key,
                    Extra = _group == Grouping.Customer ? g.First().ComName : null,
                    IsGroup = true,
                    Children = [.. g.Select(Leaf).OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase)],
                })];
    }

    private HashSet<string> _replicaTargets = new(StringComparer.OrdinalIgnoreCase);

    private GaugeNode Leaf(Gauge gauge) => new()
    {
        Key = gauge.McId,
        Label = gauge.McId,
        Gauge = gauge,
        ReplicaTarget = _replicaTargets.Contains(gauge.McId),
    };

    /// <summary>이름이나 설명에 걸리면 남긴다.</summary>
    private static bool Matches(Gauge gauge, string search) =>
        search.Length == 0
        || gauge.McId.Contains(search, StringComparison.OrdinalIgnoreCase)
        || (gauge.Descript?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);

    private void Filter(string? keyword)
    {
        _keyword = keyword;
        Rebuild();
    }

    private void Regroup(Grouping group)
    {
        _group = group;
        Rebuild();
    }

    /// <summary>체크칸 셋. 하나만 바꾸고 나머지는 그대로 둔다 — 셋을 각각 다른 처리기로 두면 같은 줄이 셋이 된다.</summary>
    private Task SetFilterAsync(bool? active = null, bool? maintenance = null, bool? replicated = null)
    {
        _onlyActive = active ?? _onlyActive;
        _excludeMaintenance = maintenance ?? _excludeMaintenance;
        _onlyReplicated = replicated ?? _onlyReplicated;
        Rebuild();

        // 걸러진 뒤에도 고른 것이 목록에 남아 있나 — 없으면 오른쪽 판이 목록에 없는 계량기를 계속 보여 준다.
        if (_gauge is not null && !_nodes.SelectMany(Leaves).Any(n => n.Key == _gauge.McId))
        {
            var first = _nodes.SelectMany(Leaves).FirstOrDefault();
            return first is null ? Clear() : PickAsync(first);
        }

        return Task.CompletedTask;
    }

    private Task Clear()
    {
        _node = null;
        _gauge = null;
        _hour = null;
        _rows = JsonTable.Empty;
        _replicaRows = JsonTable.Empty;
        return Task.CompletedTask;
    }

    // ── 고르기와 조회 ───────────────────────────────────────

    /// <summary>나무에서 줄을 골랐다. 묶음 줄은 고른 것으로 치지 않는다 — 묶음에는 수집 이력이 없다.</summary>
    private async Task PickAsync(GaugeNode? node)
    {
        if (node is null || node.IsGroup || node.Gauge is null)
        {
            return;
        }

        _node = node;
        _gauge = node.Gauge;
        _action = Basic;

        await LoadAsync(async () =>
        {
            await FetchAsync();
            return _rows.Rows.Count;
        }, "수집 이력이 없습니다.", "수집 이력을 읽지 못했습니다");
    }

    /// <summary>액션 단추. 쓰는 것은 묻고 나서 돌린다.</summary>
    private async Task RunAsync(GaugeAction action)
    {
        if (_gauge is null)
        {
            return;
        }

        // ADJ 재계산은 한주 쪽 자료를 다시 쓴다 — 조회 단추 사이에 섞여 있어 빗나간 손가락 하나로 돌아가므로 묻는다.
        if (action.Writes
            && _confirm is not null
            && !await _confirm.AskAsync(
                $"{_gauge.McId} 의 ADJ 를 다시 계산합니다. 한주 쪽 자료가 바뀝니다.",
                "ADJ 재계산", "재계산", ButtonRenderStyle.Danger))
        {
            return;
        }

        _action = action.Procedure;

        await LoadAsync(async () =>
        {
            await FetchAsync();
            return _rows.Rows.Count;
        }, "조회 결과가 없습니다.", $"{action.Text} 을(를) 돌리지 못했습니다");
    }

    private Task RefreshNowAsync() => LoadAsync(async () =>
    {
        await FetchAsync();
        return _rows.Rows.Count;
    }, "조회 결과가 없습니다.", "다시 읽지 못했습니다");

    /// <summary>스위치. 켜면 시계가 돌고 끄면 선다. 남은 초를 적지 않는다 — Blazor Server 라 1초마다 세는 숫자가 회로를 분당 예순 번 두드린다.</summary>
    private void ToggleAuto(bool on)
    {
        _auto = on;

        if (on)
        {
            StartAutoRefresh();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    /// <summary>자동 조회. 안내 줄을 건드리지 않는다.</summary>
    protected override Task RefreshAsync() => FetchAsync();

    /// <summary>고른 계량기를 지금 액션으로 읽는다. 요약 줄(P_QURI_HOUR)은 액션과 따로 읽는다 — 실시간·Peak 응답에는 지연·다음 예정이 없어 그대로 두면 머리띠가 옛 값에 굳는다.</summary>
    private async Task FetchAsync()
    {
        if (_gauge is null)
        {
            return;
        }

        var mcId = _gauge.McId;
        var dstId = _gauge.DstMcId;

        var main = Oadr.ExecuteProcedureAsync<JsonElement>(_action, [new OadrApi.OadrParameter("@MCID", mcId)]);

        // 기본 액션이면 그 응답이 곧 요약이다. 한 번 더 부를 까닭이 없다.
        var summary = _action == Basic
            ? Task.FromResult<List<HourRow>?>(null)
            : Oadr.ExecuteProcedureAsync<List<HourRow>>(Basic, [new OadrApi.OadrParameter("@MCID", mcId)]);

        var replica = string.IsNullOrWhiteSpace(dstId)
            ? Task.FromResult<JsonElement>(default)
            : Oadr.ExecuteProcedureAsync<JsonElement>(_action, [new OadrApi.OadrParameter("@MCID", dstId)]);

        await Task.WhenAll(main, summary, replica);

        _rows = JsonTable.From(main.Result);
        _replicaRows = string.IsNullOrWhiteSpace(dstId) ? JsonTable.Empty : JsonTable.From(replica.Result);

        _hour = _action == Basic
            ? main.Result.Deserialize<List<HourRow>>(HelpDeskApi.JsonOptions)?.FirstOrDefault()
            : summary.Result?.FirstOrDefault();
    }

    /// <summary>OADR 이 준 시각 글자. 표준시를 옮기지 않는다 — 꼬리표가 없는 한주 DB 의 벽시계 값이라 UTC 로 읽으면 아홉 시간이 어긋난다.</summary>
    private static string Stamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var text = value.Replace('T', ' ');
        return text.Length >= 19 ? text[..19] : text;
    }

    // ── OADR 응답 ──
    // 외부 시스템이라 칸 이름이 MSSQL 그대로다 — 칸마다 JsonPropertyName 을 적는다.

    /// <summary>`P_QURI_MC` 한 줄 — 계량기 하나의 메타.</summary>
    public sealed class Gauge
    {
        [JsonPropertyName("mc_id")] public string McId { get; set; } = string.Empty;
        [JsonPropertyName("tag_id")] public string? TagId { get; set; }
        [JsonPropertyName("DESCRIPT")] public string? Descript { get; set; }
        [JsonPropertyName("GAUGE_SECTION")] public string? Section { get; set; }
        [JsonPropertyName("CUST_ID")] public string? CustId { get; set; }
        [JsonPropertyName("com_name")] public string? ComName { get; set; }
        [JsonPropertyName("com_port")] public string? ComPort { get; set; }
        [JsonPropertyName("Maintenance_YN")] public string? MaintenanceYn { get; set; }
        [JsonPropertyName("USE_FLAG")] public string? UseFlag { get; set; }
        [JsonPropertyName("dst_mc_id")] public string? DstMcId { get; set; }
        /// <summary>마지막 수집에서 몇 분 지났나. null 이 온다 — 아직 한 번도 안 들어온 계량기가 그렇다. int 로 받으면 목록 전체가 깨진다.</summary>
        [JsonPropertyName("last_mgap")] public int? LastMgap { get; set; }
    }

    /// <summary>`P_QURI_HOUR` 한 줄 — 머리띠에 적는 요약.</summary>
    public sealed class HourRow
    {
        [JsonPropertyName("last_hgap")] public int? LastHgap { get; set; }
        [JsonPropertyName("last_mgap")] public int? LastMgap { get; set; }
        [JsonPropertyName("last_sgap")] public int? LastSgap { get; set; }
        [JsonPropertyName("com_srv")] public string? ComSrv { get; set; }
        [JsonPropertyName("last_get_dt")] public string? LastGetDt { get; set; }
        [JsonPropertyName("next_get_dt")] public string? NextGetDt { get; set; }
        [JsonPropertyName("retry_cnt")] public int? RetryCnt { get; set; }
        [JsonPropertyName("last_error_msg")] public string? LastErrorMsg { get; set; }
    }
}
