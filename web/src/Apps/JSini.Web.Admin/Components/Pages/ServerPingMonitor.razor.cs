using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class ServerPingMonitor
{
    private const string EventCode = "SERVER_PING";

    [Inject] private AdminClient Api { get; set; } = default!;

    private List<ServerPingDeviceStatusDto> _devices = [];
    private NotifyPolicyPreviewDto? _policyPreview;
    private bool _checking;
    private bool _initialLoaded;

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
            var statusTask = SafeAsync(ct => Api.GetAllServerPingStatusAsync(ct), (IReadOnlyList<ServerPingDeviceStatusDto>)_devices);
            var policyTask = SafeAsync(ct => Api.GetNotifyRecipientsAsync(EventCode, ct), _policyPreview);

            var items = await statusTask;
            if (items != null && items.Count > 0)
            {
                _devices = items.ToList();
            }
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
            var statusTask = Api.GetAllServerPingStatusAsync();
            var policyTask = SafeAsync(ct => Api.GetNotifyRecipientsAsync(EventCode, ct), _policyPreview);

            await Task.WhenAll(statusTask, policyTask);

            var items = await statusTask;
            _devices = items?.ToList() ?? [];
            _policyPreview = await policyTask;
            _initialLoaded = true;
        }
        catch (Exception ex)
        {
            Say("내부 장비 Ping 상태를 조회하지 못했습니다: " + ex.Message, NoticeTone.Error);
        }
        finally
        {
            _checking = false;
            _initialLoaded = true;
        }
    }

    private async Task ReloadSingleDeviceAsync(ServerPingDeviceStatusDto device)
    {
        try
        {
            var res = await Api.GetServerPingStatusAsync(device.Target.Ip, device.Target.TimeoutMs);
            if (res != null)
            {
                device.PingResult = res;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Say($"'{device.Target.Name}' 상태 확인 실패: {ex.Message}", NoticeTone.Warning);
        }
    }

    private void GoToManage()
    {
        Navigation.NavigateTo("/admin/status/server-ping-manage");
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
