using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using JSini.Web.Components.Settings;
using Microsoft.AspNetCore.WebUtilities;

namespace JSini.Web.Components.Layout;

public partial class PushClickRead
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private ILogger<PushClickRead> Log { get; set; } = default!;

    /// <summary>
    /// 주소에 실려 오는 표시 이름. <b>서비스워커와의 약속</b>이라
    /// (<c>push-sw.js</c> 의 <c>withReadMark</c>) 한쪽만 고치면 조용히 끊긴다.
    /// </summary>
    private const string Marker = "pushId";

    /// <summary>
    /// 이미 처리한 열쇠. 같은 값으로 두 번 부르지 않는다 — 표시를 뗀 이동이
    /// 다시 <see cref="OnLocationChanged"/> 를 타므로 방패가 하나 있어야 한다.
    /// </summary>
    private string? _done;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// <b>첫 렌더 뒤에</b> 본다. 알림을 누르면 브라우저가 그 주소를 통째로
    /// 여는 길이라(회로가 새로 붙는다) 대개 여기서 걸린다.
    ///
    /// <para>
    /// 프리렌더 중에 하지 않는 이유는 주소를 바꾸기 때문이다 — 그때의
    /// <c>NavigateTo</c> 는 예외로 흐름을 끊는다.
    /// </para>
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await MarkAsync();
        }
    }

    /// <summary>
    /// 회로 안에서 옮겨 다니는 길도 본다. 지금은 첫 렌더 쪽만 타지만,
    /// 이미 열린 창을 옮기는 방식이 바뀌어도 여기서 받는다.
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        => _ = InvokeAsync(MarkAsync);

    private async Task MarkAsync()
    {
        if (TakeMarker() is not { Length: > 0 } id || id == _done)
        {
            return;
        }

        _done = id;

        try
        {
            await Api.MarkInboxReadAsync(id);
        }
        catch (Exception ex)
        {
            Log.LogDebug(ex, "알림 {Id} 를 읽음으로 찍지 못했다.", id);
        }
    }

    /// <summary>
    /// 주소에서 표시를 <b>꺼내면서 뗀다.</b> 떼는 일을 처리 뒤로 미루지
    /// 않는 이유는 실패해도 표시가 남으면 안 되기 때문이다.
    /// </summary>
    private string? TakeMarker()
    {
        var query = new Uri(Navigation.Uri).Query;

        if (string.IsNullOrEmpty(query)
            || !QueryHelpers.ParseQuery(query).TryGetValue(Marker, out var values))
        {
            return null;
        }

        Navigation.NavigateTo(
            Navigation.GetUriWithQueryParameter(Marker, (string?)null), replace: true);

        return values.ToString() is { Length: > 0 } id ? id : null;
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
