using System.Reflection;
using Piral.Blazor.Orchestrator;
using Piral.Blazor.Orchestrator.Loader;
using Piral.Blazor.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public class PiralApiTests
{
    [Fact]
    public void Piral_Orchestrator_Types_Test()
    {
        var orchAssembly = typeof(MfDiscoveryLoaderService).Assembly;
        var sharedAssembly = typeof(MfComponent).Assembly;

        Assert.NotNull(orchAssembly);
        Assert.NotNull(sharedAssembly);

        var exportedTypes = orchAssembly.GetExportedTypes();
        Assert.NotEmpty(exportedTypes);
    }
}
