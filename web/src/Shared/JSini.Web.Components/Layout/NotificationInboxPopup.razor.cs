using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using JSini.Web.Models;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class NotificationInboxPopup
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public EventCallback OnRead { get; set; }

    private IReadOnlyList<NotificationDto> _notifications = [];

    private async Task OnVisibleChangedAsync(bool visible)
    {
        if (Visible == visible) return;

        Visible = visible;
        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(visible);
        }

        if (visible)
        {
            await LoadUnreadAsync();
        }
    }

    private async Task LoadUnreadAsync()
    {
        try
        {
            _notifications = await Api.GetMyNotificationsAsync(unreadOnly: true);
        }
        catch
        {
            _notifications = [];
        }
    }

    private async Task MarkAllReadAsync()
    {
        try
        {
            await Api.MarkAllInboxReadAsync();
            await LoadUnreadAsync();
            if (OnRead.HasDelegate)
            {
                await OnRead.InvokeAsync();
            }
        }
        catch
        {
        }
    }

    private async Task OpenAsync(NotificationDto n)
    {
        var url = OpenUrl(n.Url);
        if (url == null) return;

        try
        {
            await Api.MarkInboxReadAsync(n.Id);
            if (OnRead.HasDelegate)
            {
                await OnRead.InvokeAsync();
            }
        }
        catch { }

        await OnVisibleChangedAsync(false);
        Navigation.NavigateTo(url);
    }

    private static string? OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        var match = AiTaskListLink().Match(trimmed);
        return match.Success ? $"/projmng/ai/task/{match.Groups["key"].Value}" : trimmed;
    }

    [GeneratedRegex(@"^/projmng/ai/tasks\?(?:task|key)=(?<key>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AiTaskListLink();
}
