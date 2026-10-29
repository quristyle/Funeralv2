using System.Text.Json.Serialization;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;
using JSini.Web.HelpDesk.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class HanjuSystemMonitor
{
    [Inject] private OadrApi Oadr { get; set; } = default!;

    // ── 판정 기준 ──
    // 표의 색·배지와 점수판이 같은 수를 봐야 해서 한 곳에 모았다 — 두 곳에 적으면 한쪽만 고쳐진다.

    /// <summary>CPU 사용률(%) — 넘으면 표에서 붉어진다.</summary>
    private const double CpuLimit = 80;

    /// <summary>디스크 응답(ms) — 20ms 가 「지연」이 시작되는 자리다.</summary>
    private const double IoLimit = 20;

    /// <summary>TempDB·로그 지연(ms/s).</summary>
    private const double StallLimit = 50;

    /// <summary>PLE(초) — 이 아래는 메모리가 고갈되고 있다는 뜻이다.</summary>
    private const double PleCritical = 300;

    /// <summary>PLE 권장선(초).</summary>
    private const double PleGood = 1000;

    /// <summary>1분 자동 조회. 스위치를 켠 동안에만 돈다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(60);

    /// <summary>틀을 자리표시로 먼저 세우므로 옮김 표시로 덮지 않는다.</summary>
    protected override bool CoverWhileLoading => false;

    private const string MinuteHeight = "170px";
    private const string HourHeight = "210px";

    private static readonly IReadOnlyList<string> MinuteTitles = ["1H CPU", "1H I/O", "1H Memory", "1H Traffic"];
    private static readonly IReadOnlyList<string> HourTitles =
        ["24H CPU Load (%)", "24H I/O Latency (ms)", "24H Memory Stability", "24H Subsystem Stalls"];

    private RealtimeRow? _now;
    private IReadOnlyList<MinutePoint> _minutes = [];
    private IReadOnlyList<HourPoint> _hours = [];

    /// <summary>표에 거는 것. 차트와 달리 <b>늦은 시간대부터</b> 센다.</summary>
    private IReadOnlyList<HourPoint> _rows = [];

    // ── 경량 SVG 차트용 라벨 및 시리즈 ──
    private IReadOnlyList<string> _minuteLabels = [];
    private IReadOnlyList<string> _hourLabels = [];

    private IReadOnlyList<SysMonSeries> _chart1hCpu = [];
    private IReadOnlyList<SysMonSeries> _chart1hIo = [];
    private IReadOnlyList<SysMonRefLine> _refLines1hIo = [];
    private IReadOnlyList<SysMonSeries> _chart1hPle = [];
    private IReadOnlyList<SysMonRefLine> _refLines1hPle = [];
    private IReadOnlyList<SysMonSeries> _chart1hTps = [];
    private IReadOnlyList<SysMonRefLine> _refLines1hTps = [];

    private IReadOnlyList<SysMonSeries> _chart24hCpu = [];
    private IReadOnlyList<SysMonSeries> _chart24hIo = [];
    private IReadOnlyList<SysMonRefLine> _refLines24hIo = [];
    private IReadOnlyList<SysMonSeries> _chart24hPle = [];
    private IReadOnlyList<SysMonRefLine> _refLines24hPle = [];
    private IReadOnlyList<SysMonSeries> _chart24hStalls = [];

    private bool _auto;
    private string _updatedAt = "-";

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>스위치. 끌 때 바로 서야 한다 — 끄고도 한 번 더 읽히면 「껐는데 왜 바뀌나」가 된다.</summary>
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

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        await FetchAsync();
        return (_now is null ? 0 : 1) + _minutes.Count + _hours.Count;
    }, "한주 서버 지표가 아직 쌓이지 않았습니다.", "한주운영시스템 지표를 읽지 못했습니다");

    /// <summary>자동 조회. 안내 줄을 건드리지 않는다.</summary>
    protected override Task RefreshAsync() => FetchAsync();

    /// <summary>셋을 나란히 부른다. 같은 프로시저에 QueryType 만 다르고, 차례로 기다리면 왕복이 쌓인다.</summary>
    private async Task FetchAsync()
    {
        var realtime = Oadr.GetServerReportAsync<List<RealtimeRow>>("REALTIME");
        var monitoring = Oadr.GetServerReportAsync<List<MinuteRow>>("MONITORING");
        var daily = Oadr.GetServerReportAsync<List<DailyRow>>("DAILY");

        await Task.WhenAll(realtime, monitoring, daily);

        _now = realtime.Result?.FirstOrDefault();
        _minutes = [.. (monitoring.Result ?? []).Select(MinutePoint.From)];
        _hours = [.. (daily.Result ?? []).Select(HourPoint.From)];
        _rows = [.. _hours.OrderByDescending(h => h.Slot, StringComparer.Ordinal)];

        // SVG 차트용 라벨
        _minuteLabels = _minutes.Select(m => m.Label).ToList();
        _hourLabels = _hours.Select(h => $"{h.Label}시").ToList();

        // 1H 고해상도 차트 데이터
        _chart1hCpu = [new("CPU", "#42A5F5", _minutes.Select(m => m.Cpu).ToList())];
        _chart1hIo = [new("I/O", "#EF5350", _minutes.Select(m => m.Io).ToList())];
        _refLines1hIo = [new("기준선(20ms)", IoLimit, "#EF5350", "4 4")];
        _chart1hPle = [new("PLE", "#66BB6A", _minutes.Select(m => m.Ple).ToList())];
        _refLines1hPle =
        [
            new("권장(1000s)", PleGood, "#4CAF50", "2 3"),
            new("위기(300s)", PleCritical, "#EF5350", "2 3"),
        ];
        _chart1hTps = [new("TPS", "#FFA726", _minutes.Select(m => m.Tps).ToList())];
        _refLines1hTps =
        [
            new("상한(500)", 500, "#FFA726", "4 4"),
            new("하한(100)", 100, "#2196F3", "4 4"),
        ];

        // 24H 장기 추이 차트 데이터
        _chart24hCpu = [new("평균 CPU (%)", "#42A5F5", _hours.Select(h => h.Cpu).ToList(), IsArea: true, AreaOpacity: 0.15)];
        _chart24hIo = [new("피크 I/O (ms)", "#EF5350", _hours.Select(h => h.PeakIo).ToList(), IsArea: true, AreaOpacity: 0.15)];
        _refLines24hIo = [new("임계치(20ms)", IoLimit, "#EF5350", "2 3")];
        _chart24hPle =
        [
            new("평균 PLE (s)", "#2196F3", _hours.Select(h => h.AvgPle).ToList()),
            new("최소 PLE (s)", "#FFA726", _hours.Select(h => h.MinPle).ToList(), DashStyle: "2 3"),
        ];
        _refLines24hPle =
        [
            new("권장선(1000s)", PleGood, "#4CAF50", "4 4"),
            new("위기선(300s)", PleCritical, "#EF5350", "4 4"),
        ];
        _chart24hStalls =
        [
            new("TempDB 지연", "#66BB6A", _hours.Select(h => h.TempDb).ToList(), IsArea: true, AreaOpacity: 0.2),
            new("로그 지연", "#AB47BC", _hours.Select(h => h.Log).ToList(), IsArea: true, AreaOpacity: 0.15),
        ];

        _updatedAt = AppTime.UtcNow.Kst("HH:mm:ss");
    }

    // ── 점수판 ──────────────────────────────────────────────

    /// <summary>점수판 칸 하나의 판정. 배지 글자·색과 한마디를 함께 들고 다닌다.</summary>
    private sealed record Verdict(string Label, string Badge, string Advice);

    /// <summary>점수판 칸 하나의 정의(제목·기준·설명·판정).</summary>
    private sealed record MetricDef(
        string Key,
        string Title,
        string Unit,
        string Format,
        string Guide,
        string Desc,
        Func<RealtimeRow, double> Pick,
        Func<double, Verdict> Judge);

    private static readonly IReadOnlyList<MetricDef> Metrics =
    [
        new("cpu", "CPU 사용률", "%", "0.##",
            "표준: 70% 이하 유지",
            "프로세서의 작업 부하량을 의미합니다. 80% 이상 지속 시 쿼리 처리 지연 및 시스템 병목이 발생합니다.",
            r => r.Cpu,
            v => v >= 90 ? new("위험", "jsini-badge--err", "임계치 초과! 점검 요망.")
               : v >= 70 ? new("주의", "jsini-badge--warn", "부하 증가 추세.")
                         : new("정상", "jsini-badge--success", "안정 운영 중.")),

        new("io", "디스크 응답", "ms", "0",
            "표준: 20ms 이내 정상",
            "데이터 읽기/쓰기 요청이 완료되는 시간(ms)입니다. 100ms 초과 시 심각한 I/O 병목으로 판단합니다.",
            r => r.Io,
            v => v >= 50 ? new("심각", "jsini-badge--err", "I/O 장애 수준!")
               : v >= 20 ? new("지연", "jsini-badge--warn", "디스크 응답 저하.")
                         : new("쾌적", "jsini-badge--success", "응답 속도 양호.")),

        new("ple", "메모리 수명(PLE)", "s", "0",
            "권장: 1000s 이상 유지",
            "Page Life Expectancy. 데이터가 메모리에 머무는 시간(초)입니다. 300초 미만은 메모리 압박이 심각함을 의미합니다.",
            r => r.Ple,
            v => v < 300 ? new("위기", "jsini-badge--err", "메모리 고갈 위험!")
               : v < 1000 ? new("압박", "jsini-badge--warn", "가용 메모리 부족.")
                          : new("양호", "jsini-badge--success", "수명 안정적 유지.")),

        new("tps", "초당 처리량(TPS)", "", "0",
            "통상: 100~500 수준",
            "Batch Requests/sec. 서버가 초당 처리하는 SQL 명령 수입니다. 서비스의 실제 업무 부하를 나타내는 핵심 지표입니다.",
            r => r.Tps,
            v => v >= 1000 ? new("고부하", "jsini-badge--err", "트래픽 폭주 중.")
               : v >= 500 ? new("활발", "jsini-badge--warn", "업무 처리 활발.")
                          : new("원활", "jsini-badge--success", "통상적인 업무량.")),
    ];

    /// <summary>맨 위 종합 배지. 프로시저가 주는 글자(Server_State)는 영어 한 줄이고, 무엇 때문에 Warning 인지가 그 안에 들어 있어 그대로 보여 준다.</summary>
    private static string StateBadge(string? state) =>
        state is null ? "jsini-badge--muted"
        : state.Equals("Healthy", StringComparison.OrdinalIgnoreCase) ? "jsini-badge--success"
        : state.Contains("Warning", StringComparison.OrdinalIgnoreCase) ? "jsini-badge--warn"
        : "jsini-badge--err";

    // ── OADR 응답 ──
    // 외부 시스템이라 칸 이름이 MSSQL 그대로다 — 우리 규칙으로는 못 맞춰 칸마다 JsonPropertyName 을 적는다.

    /// <summary>`QueryType = REALTIME` — 가장 최근 한 줄.</summary>
    public sealed class RealtimeRow
    {
        [JsonPropertyName("CPU_SQL_USAGE")] public double Cpu { get; set; }
        [JsonPropertyName("AVG_IO_LATENCY_MS")] public double Io { get; set; }
        [JsonPropertyName("PLE")] public double Ple { get; set; }
        [JsonPropertyName("BATCH_REQUESTS_SEC")] public double Tps { get; set; }
        [JsonPropertyName("Server_State")] public string? State { get; set; }
    }

    /// <summary>`QueryType = MONITORING` — 최근 한 시간, 1분 간격.</summary>
    public sealed class MinuteRow
    {
        [JsonPropertyName("CHECK_TIME")] public string? CheckTime { get; set; }
        [JsonPropertyName("CPU_SQL_USAGE")] public double Cpu { get; set; }
        [JsonPropertyName("AVG_IO_LATENCY_MS")] public double Io { get; set; }
        [JsonPropertyName("PLE")] public double Ple { get; set; }
        [JsonPropertyName("BATCH_REQUESTS_SEC")] public double Tps { get; set; }
    }

    /// <summary>`QueryType = DAILY` — 최근 24시간, 시간대별 집계.</summary>
    public sealed class DailyRow
    {
        [JsonPropertyName("TimeSlot")] public string? TimeSlot { get; set; }
        [JsonPropertyName("Avg_CPU")] public double? Cpu { get; set; }
        [JsonPropertyName("Peak_IO_ms")] public double? PeakIo { get; set; }
        [JsonPropertyName("Avg_PLE")] public double? AvgPle { get; set; }
        [JsonPropertyName("Min_PLE")] public double? MinPle { get; set; }
        [JsonPropertyName("Avg_TempDB_Stall")] public double? TempDb { get; set; }
        [JsonPropertyName("Avg_Log_Stall")] public double? Log { get; set; }
    }

    // ── 차트·표의 재료 ──

    /// <summary>1시간 추이의 한 점.</summary>
    public sealed record MinutePoint(string Label, double Cpu, double Io, double Ple, double Tps)
    {
        public double IoLimit => HanjuSystemMonitor.IoLimit;
        public double PleGood => HanjuSystemMonitor.PleGood;
        public double PleBad => PleCritical;
        public double TpsHigh => 500;
        public double TpsLow => 100;

        /// <summary>가로축 이름표는 HH:mm 다. CHECK_TIME 은 한주 DB 의 벽시계 값이라 표준시 꼬리표가 없다 — 옮기지 않고 글자를 자른다.</summary>
        public static MinutePoint From(MinuteRow row) =>
            new(Clock(row.CheckTime), row.Cpu, row.Io, row.Ple, row.Tps);

        private static string Clock(string? checkTime)
        {
            var time = checkTime?.Split('T') is [_, var rest] ? rest : checkTime;
            return time is { Length: >= 5 } ? time[..5] : string.Empty;
        }
    }

    /// <summary>24시간 추이의 한 칸. 표도 이것을 그대로 쓴다.</summary>
    public sealed record HourPoint(
        string Slot, string Label,
        double Cpu, double PeakIo, double AvgPle, double MinPle, double TempDb, double Log)
    {
        public double IoLimit => HanjuSystemMonitor.IoLimit;
        public double PleGood => HanjuSystemMonitor.PleGood;
        public double PleBad => PleCritical;

        /// <summary>TimeSlot 은 날짜와 시각을 함께 든다(「날짜 21:00」 꼴). 차트 이름표는 시만, 표 칸은 날짜까지 적는다 — 차트는 스물넷이 한 줄에 서야 해서 날짜 자리가 없다.</summary>
        public static HourPoint From(DailyRow row)
        {
            var slot = row.TimeSlot ?? string.Empty;
            var parts = slot.Split(' ');
            var hour = parts.Length > 1 ? parts[1].Split(':')[0] : slot;

            return new(
                parts.Length > 1 ? $"{parts[0]} {hour}시" : slot,
                hour,
                row.Cpu ?? 0,
                row.PeakIo ?? 0,
                row.AvgPle ?? 0,
                row.MinPle ?? 0,
                row.TempDb ?? 0,
                row.Log ?? 0);
        }
    }
}
