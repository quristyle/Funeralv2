using Microsoft.AspNetCore.Components;
using JSini.Web.Components;

namespace JSini.Web.Shell.Components;

public partial class Routes
{
    [Inject] private PortalModuleRegistry ModuleRegistry { get; set; } = default!;
}
