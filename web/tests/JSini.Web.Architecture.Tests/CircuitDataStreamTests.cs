using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 브라우저가 서버로 <b>바이트를 흘려보내는 길</b>이 살아 있는가.
///
/// <para>
/// [무엇이 죽어 있었나 — 2026-09-25]
/// </para>
///
/// <para>
/// 화면에서 고른 파일은 회로(SignalR)를 타고 조각으로 올라온다
/// (<c>ReceiveJSDataChunk(흐름번호, 조각번호, <b>바이트</b>, 오류)</c>).
/// 첨부도, 요청 본문에 넣는 그림도, <c>DxHtmlEditor</c> 가 제 글을 서버로
/// 돌려주는 일도 전부 이 길 하나다.
/// </para>
///
/// <para>
/// 그런데 SignalR 은 허브 메서드의 인자 중 <b>DI 에 등록된 타입이면 사람이
/// 보낸 값이 아니라 서비스로 보고 빼 버린다</b>(암묵적 <c>[FromServices]</c>,
/// .NET 8 부터). 셸의 DI 는 Piral.Blazor 때문에 <b>Autofac</b> 이고,
/// Autofac 은 배열을 컬렉션으로 풀어 주는 규칙 때문에 <c>byte[]</c> 를
/// <b>언제나 「등록된 것」이라고 답한다.</b> 그래서 서버는 인자 셋만
/// 기다리는데 브라우저는 넷을 보냈고, 바인딩이 통째로 버렸다 —
/// <c>Invocation provides 4 argument(s) but target expects 3</c>.
/// </para>
///
/// <para>
/// <b>화면에는 오류가 한 줄도 안 뜬다.</b> 「올리는 중…」에서 멈춘 채 1분을
/// 기다리다 회로가 끊기고, 쓰던 글이 날아간다. 그래서 눈으로는 「그림 넣기가
/// 안 된다」로만 보였다.
/// </para>
/// </summary>
public sealed class CircuitDataStreamTests
{
    /// <summary>
    /// <b>위험이 아직 거기 있는가.</b> Autofac 이 <c>byte[]</c> 를 서비스라고
    /// 답하는 한 아래 설정을 걷으면 안 된다. 언젠가 Autofac 이 이 답을 바꾸면
    /// 이 시험이 먼저 알려 준다.
    /// </summary>
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

    /// <summary>
    /// 그래서 <b>암묵적 서비스 인자를 꺼 두었는가.</b>
    /// </summary>
    /// <remarks>
    /// 이 한 줄이 사라져도 빌드는 통과하고 화면도 뜬다. 파일을 실제로 하나
    /// 올려 봐야만 드러나므로 기계가 센다.
    /// </remarks>
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
