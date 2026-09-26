using Microsoft.AspNetCore.Components;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class ReleaseNotes
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private ReleaseTargetListDto? _targets;
    private IReadOnlyList<ReleaseRunDto> _runs = [];

    private bool _showingRun;
    private ReleaseRunDto? _run;
    private string RunHeader => _run is null ? "진행 기록" : $"{_run.TargetName} · {_run.RequestedAt:yyyy-MM-dd HH:mm}";

    /// <summary>도는 것을 지켜볼 때의 간격. 옛 화면은 2초였다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(3);

    /// <summary>대기·진행 중인 실행이 하나라도 있는가. 시계를 켜고 끄는 조건이다.</summary>
    private bool AnyRunning => _runs.Any(r =>
        string.Equals(r.Status, "running", StringComparison.OrdinalIgnoreCase)
        || string.Equals(r.Status, "queued", StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();

        if (AnyRunning)
        {
            StartAutoRefresh();
        }
    }

    /// <summary>
    /// 자동 조회. 도는 것이 없어지면 **시계를 끈다** — 다 끝난 화면이
    /// 3초마다 게이트웨이를 두드릴 이유가 없다.
    /// </summary>
    protected override async Task RefreshAsync()
    {
        // 둘을 나란히. 서로 기다릴 이유가 없다 — 차례로 부르면 왕복이 둘 쌓인다.
        // 자동 조회라 3초마다 그 값을 낸다.
        var targets = Api.GetReleaseTargetsAsync();
        var runs = Api.GetReleaseRunsAsync();

        await Task.WhenAll(targets, runs);

        _targets = targets.Result;
        _runs = runs.Result;

        // 열어 둔 진행 기록도 함께 맞춘다 — 그 창을 보고 있는 중일 것이다.
        if (_showingRun && _run is not null)
        {
            _run = await Api.GetReleaseRunAsync(_run.Id) ?? _run;
        }

        if (!AnyRunning)
        {
            Dispose();
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 둘을 나란히. 이유는 RefreshAsync 와 같다.
        var targets = Api.GetReleaseTargetsAsync();
        var runs = Api.GetReleaseRunsAsync();

        await Task.WhenAll(targets, runs);

        _targets = targets.Result;
        _runs = runs.Result;

        // 조회를 누른 뒤에 도는 것이 생겼으면 그때부터 지켜본다.
        if (AnyRunning)
        {
            StartAutoRefresh();
        }

        // 대상이 없을 때만 "없습니다" 다. 실행 기록은 처음에 비어 있는 것이
        // 정상이라 그것으로 판단하면 안 된다.
        return _targets?.Items.Count ?? 0;
    }, "걸 수 있는 배포 대상이 없습니다. 서버 설정을 확인하십시오.", "배포 현황을 읽지 못했습니다");

    private async Task ShowRunAsync(string id)
    {
        _run = null;
        _showingRun = true;

        await LoadOneAsync(
            () => Api.GetReleaseRunAsync(id),
            run => _run = run,
            "그 실행을 찾지 못했습니다.",
            "진행 기록을 읽지 못했습니다");
    }

    /// <summary>요청부터 끝까지 걸린 시간. 아직 도는 중이면 지금까지.</summary>
    private static string Elapsed(ReleaseRunDto r)
    {
        var from = r.StartedAt ?? r.RequestedAt;
        var to = r.FinishedAt ?? DateTime.UtcNow;
        var span = to - from;

        return span < TimeSpan.Zero ? "-"
            : span.TotalMinutes < 1 ? $"{span.TotalSeconds:F0}초"
            : $"{(int)span.TotalMinutes}분 {span.Seconds}초";
    }

    private static string StatusText(string status) => status?.ToLowerInvariant() switch
    {
        "queued" => "대기",
        "running" => "도는 중",
        "succeeded" => "성공",
        "failed" => "실패",
        "timeout" => "시간 초과",
        _ => status ?? "-",
    };

    private static string StatusClass(string status) => status?.ToLowerInvariant() switch
    {
        "succeeded" => "jsini-badge--on",
        "running" or "queued" => "jsini-badge--warn",
        _ => "jsini-badge--off",
    };
}
