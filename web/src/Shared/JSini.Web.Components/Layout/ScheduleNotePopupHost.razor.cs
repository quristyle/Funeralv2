using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class ScheduleNotePopupHost : IDisposable
{
    [Inject] private ScheduleNoteReveal Reveal { get; set; } = default!;

    protected override void OnInitialized()
    {
        Reveal.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Reveal.Changed -= OnChanged;
    }
}
