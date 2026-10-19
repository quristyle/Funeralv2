using System.Drawing;
using System.Text.Json.Serialization;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;
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

    // ── 색 ──
    // 같은 지표가 1시간·24시간 두 차트에 나뉘어 있어 색이 갈리면 같은 것을 보는 것이 안 읽힌다.

    /// <summary>선 색. 흐린 쪽(기준선)은 알파를 넣는다 — ColorTranslator 는 rgba(...) 를 못 읽어 직접 짓는다.</summary>
    private static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    private static Color Faint(string hex, int alpha) =>
        Color.FromArgb(alpha, ColorTranslator.FromHtml(hex));

    private static readonly Color CpuColor = Hex("#42A5F5");
    private static readonly Color CpuFaintColor = Faint("#2196F3", 115);
    private static readonly Color IoColor = Hex("#EF5350");
    private static readonly Color IoFaintColor = Faint("#EF5350", 128);
    private static readonly Color PleColor = Hex("#66BB6A");
    private static readonly Color PleAvgColor = Hex("#2196F3");
    private static readonly Color PleFaintColor = Faint("#4CAF50", 153);
    private static readonly Color TpsColor = Hex("#FFA726");
    private static readonly Color TpsFaintColor = Faint("#FF9800", 115);
    private static readonly Color LogColor = Hex("#AB47BC");

    /// <summary>1분 자동 조회. 스위치를 켠 동안에만 돈다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(60);

    /// <summary>틀을 자리표시로 먼저 세우므로 옮김 표시로 덮지 않는다.</summary>
    protected override bool CoverWhileLoading => false;

    private const string MinuteHeight = "170px";
    private const string HourHeight = "210px";

    private static readonly IReadOnlyList<string> MinuteTitles = ["1H CPU", "1H I/O", "1H Memory", "1H Traffic"];
    private static readonly IReadOnlyList<string> HourTitles =
        ["24H CPU Load (%)", "24H I/O Latency (ms)", "24H Memory Stability", "24H Subsystem Stalls"];

    /// <summary>24시간 차트를 그릴 차례인가. 첫 자료가 그려진 다음 그림에서 켠다 — 여덟 장을 한 그림에 몰면 그리는 동안 브라우저가 멎는다.</summary>
    private bool _showHours;

    private RealtimeRow? _now;
    private IReadOnlyList<MinutePoint> _minutes = [];
    private IReadOnlyList<HourPoint> _hours = [];

    /// <summary>표에 거는 것. 차트와 달리 <b>늦은 시간대부터</b> 센다.</summary>
    private IReadOnlyList<HourPoint> _rows = [];

    private bool _auto;
    private string _updatedAt = "-";

    protected override Task OnInitializedAsync() => ReloadAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_showHours || _hours.Count == 0)
        {
            return;
        }

        // 점수판·1시간 차트가 브라우저에 그려질 틈을 준 뒤 24시간 차트를 얹는다.
        await Task.Delay(120);
        _showHours = true;
        StateHasChanged();
    }

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

    /// <summary>말풍선의 수. 소수가 열두 자리까지 나오는 값이 섞여 있다.</summary>
    private static string Tip(object? value) =>
        value is double d ? d.ToString("0.##") : value?.ToString() ?? "-";

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
    // 기준선을 상수선(DxChartConstantLine)으로 쓰지 않는 까닭은 범례다 — 상수선은 범례에 서지 않아 선만 그어진다.

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
