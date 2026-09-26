using Microsoft.AspNetCore.Components;
using System.Diagnostics;

namespace JSini.Web.Shell.Components.Pages;

public partial class Error
{
    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private string? _requestId;

    protected override void OnInitialized() =>
        _requestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
}
