using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class ServerPingMonitor
{
    private const string TargetIp = "192.168.219.229";
    private const string EventCode = "SERVER_PING";

    [Inject] private AdminClient Api { get; set; } = default!;

    private ServerPingResultDto? PingResult { get; set; }
    private NotifyPolicyPreviewDto? _policyPreview;
    private bool _checking;

    /// <summary>10초마다 상태를 다시 확인한다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(10);

    private string CardTone => PingResult?.IsAlive switch
    {
        true => "up",
        false => "down",
        null => "warn",
    };

    private string BadgeText => PingResult?.IsAlive switch
    {
        true => "정상 응답",
        false => "응답 없음",
        null => "확인 중",
    };

    private string BadgeClass => PingResult?.IsAlive switch
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
            PingResult = await SafeAsync(ct => Api.GetServerPingStatusAsync(TargetIp, 2000, ct), PingResult);
            _policyPreview = await SafeAsync(ct => Api.GetNotifyRecipientsAsync(EventCode, ct), _policyPreview);
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
            await LoadOneAsync(
                () => Api.GetServerPingStatusAsync(TargetIp, 2000),
                v => PingResult = v,
                "내부 장비 Ping 상태를 조회하지 못했습니다.",
                "내부 장비 Ping 조회 실패");

            _policyPreview = await SafeAsync(ct => Api.GetNotifyRecipientsAsync(EventCode, ct), _policyPreview);
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
