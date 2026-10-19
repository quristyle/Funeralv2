using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>브라우저가 서버로 바이트를 흘려보내는 길이 살아 있는가. 허브 메서드 인자 중 DI 에 등록된 타입은 SignalR 이 서비스로 보고 빼 버리는데,
/// 셸의 Autofac 은 byte[] 를 「등록된 것」으로 답한다. 그래서 인자 셋만 기다리는 서버에 넷이 가서 바인딩이 통째로 버려지고, 화면에는 오류 없이 「올리는 중…」에서 멈춘다.</summary>
public sealed class CircuitDataStreamTests
{
    /// <summary>위험이 아직 거기 있는가 — Autofac 이 byte[] 를 서비스라고 답하는 한 아래 설정을 걷으면 안 된다. 언젠가 바뀌면 이 시험이 먼저 알려 준다.</summary>
    [Fact]
    public void Autofac_은_바이트배열을_서비스라고_답한다()
    {
        using var container = new ContainerBuilder().Build();
        var provider = new AutofacServiceProvider(container);

        var probe = (IServiceProviderIsService)provider;

        Assert.True(probe.IsService(typeof(byte[])),
            "Autofac 이 byte[] 를 서비스로 보지 않게 됐다면 이 시험과 "
            + "JSiniWebApp 의 DisableImplicitFromServicesParameters 주석을 다시 쓸 것.");
    }

    /// <summary>암묵적 서비스 인자를 꺼 두었는가. 이 한 줄이 사라져도 빌드와 화면은 멀쩡해 파일을 실제로 올려 봐야 드러나므로 기계가 센다.</summary>
    [Fact]
    public void 회로_허브가_인자를_서비스로_오인하지_않는다()
    {
        var source = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "JSiniWebApp.cs"));

        Assert.Contains("options.DisableImplicitFromServicesParameters = true;", source);
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
