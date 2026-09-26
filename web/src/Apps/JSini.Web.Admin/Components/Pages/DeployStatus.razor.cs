using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class DeployStatus
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private DeployStatusDto? _status;

    private IReadOnlyList<GithubRunDto> Runs => _status?.Github.Runs ?? [];
    private IReadOnlyList<GithubRunnerDto> Runners => _status?.Github.Runners ?? [];

    /// <summary>옛 화면과 같은 30초. 빌드가 도는 것을 지켜보는 자리다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(30);

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _status = await Api.GetDeployStatusAsync();

        // 조회의 **결과**다 — 안내 줄이 아니라 토스트로 나간다.
        // **자동 조회(`RefreshAsync`)에서는 띄우지 않는다.** 몇 초마다
        // 같은 말이 다시 떠서 화면을 덮는다.
        if (_status?.Github.Error is { Length: > 0 } error)
        {
            Say($"GitHub 을 읽지 못했습니다 — {error}", NoticeTone.Warning);
        }

        return Runs.Count + Runners.Count;
    }, "배포 이력을 받지 못했습니다.", "배포 현황을 읽지 못했습니다");

    /// <summary>자동 조회. 안내 줄을 건드리지 않는다.</summary>
    protected override async Task RefreshAsync() =>
        _status = await Api.GetDeployStatusAsync();

    /// <summary>결과 문구. 아직 안 끝난 실행은 <c>conclusion</c> 이 비어 있다.</summary>
    private static string ConclusionText(GithubRunDto run) => run.Conclusion?.ToLowerInvariant() switch
    {
        "success" => "성공",
        "failure" => "실패",
        "cancelled" => "취소",
        "skipped" => "건너뜀",
        null or "" => string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? "알 수 없음"
            : "진행 중",
        _ => run.Conclusion!,
    };

    private static string ConclusionClass(GithubRunDto run) => run.Conclusion?.ToLowerInvariant() switch
    {
        "success" => "jsini-badge--on",
        "failure" => "jsini-badge--off",
        null or "" => "jsini-badge--warn",
        _ => "",
    };
}
