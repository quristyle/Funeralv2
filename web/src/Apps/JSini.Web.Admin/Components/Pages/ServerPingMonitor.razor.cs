using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class ServerPingMonitor
{
    private const string TargetGridIp = "192.168.219.229";
    private const string TargetJinSmIp = "192.168.219.224";
    private const string EventCode = "SERVER_PING";

    [Inject] private AdminClient Api { get; set; } = default!;

    private ServerPingResultDto? GridResult { get; set; }
    private ServerPingResultDto? JinSmResult { get; set; }
    private NotifyPolicyPreviewDto? _policyPreview;
    private bool _checking;

    /// <summary>10초마다 상태를 다시 확인한다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(10);

    private static string GetCardTone(ServerPingResultDto? result) => result?.IsAlive switch
    {
        true => "up",
        false => "down",
        null => "warn",
    };

    private static string GetBadgeText(ServerPingResultDto? result) => result?.IsAlive switch
    {
        true => "정상 응답",
        false => "응답 없음",
        null => "확인 중",
    };

    private static string GetBadgeClass(ServerPingResultDto? result) => result?.IsAlive switch
    {
        true => "jsini-badge--on",
        false => "jsini-badge--err",
        null => "jsini-badge--warn",
    };

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    protected override async Task RefreshAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var gridTask = SafeAsync(ct => Api.GetServerPingStatusAsync(TargetGridIp, 2000, ct), GridResult);
            var jinSmTask = SafeAsync(ct => Api.GetServerPingStatusAsync(TargetJinSmIp, 2000, ct), JinSmResult);
            var policyTask = SafeAsync(ct => Api.GetNotifyRecipientsAsync(EventCode, ct), _policyPreview);

            GridResult = await gridTask;
            JinSmResult = await jinSmTask;
            _policyPreview = await policyTask;
        }
        finally
        {
            _checking = false;
        }
    }

    private async Task ReloadAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var gridTask = Api.GetServerPingStatusAsync(TargetGridIp, 2000);
            var jinSmTask = Api.GetServerPingStatusAsync(TargetJinSmIp, 2000);
            var policyTask = SafeAsync(ct => Api.GetNotifyRecipientsAsync(EventCode, ct), _policyPreview);

            await Task.WhenAll(gridTask, jinSmTask, policyTask);

            GridResult = await gridTask;
            JinSmResult = await jinSmTask;
            _policyPreview = await policyTask;
        }
        catch (Exception ex)
        {
            Say("내부 장비 Ping 상태를 조회하지 못했습니다: " + ex.Message, NoticeTone.Error);
        }
        finally
        {
            _checking = false;
        }
    }

    private void GoToNotifyPolicy()
    {
        Navigation.NavigateTo("/admin/system/notify-policy");
    }

    private static async Task<T> SafeAsync<T>(Func<CancellationToken, Task<T>> load, T fallback)
    {
        try
        {
            return await load(CancellationToken.None);
        }
        catch
        {
            return fallback;
        }
    }
}
