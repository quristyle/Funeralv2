using System.Text.Json;
using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 다이어그램의 <b>보던 자리</b>(<see cref="ErdViewport"/>)가 JS 와 같은 이름으로
/// 오가는지 못박는다.
///
/// <para>[왜 이것을 테스트하나]</para>
///
/// <para>
/// 이 값은 <b>C# 과 JS 가 글자로 약속한 것</b>이다 — `diagram-viewer.js` 의
/// <c>viewState()</c> 가 <c>{ scale, dx, dy, minimap }</c> 을 만들고 저장은
/// 거기에 <c>tools</c>·<c>pinned</c> 를 얹는다. 한쪽 이름을 고치면
/// <b>아무 데서도 예외가 나지 않고</b> 그 값만 조용히 사라진다. 증상은
/// 「저장했는데 다음에 열면 배율이 안 돌아온다」 하나뿐이라, 원인이 이름으로
/// 보이지 않는다.
/// </para>
///
/// <para>
/// 옛 저장본에는 이 칸이 <b>아예 없다.</b> 그때 기본값으로 읽어 버리면
/// (배율 1 · 미니맵 켬) 열 때마다 방금 맞춰 둔 화면이 흐트러진다. 그래서
/// <c>null</c> 이어야 하고, 그것도 여기서 못박는다.
/// </para>
/// </summary>
public sealed class ErdViewportTests
{
    /// <summary>
    /// JS 가 읽고 쓰는 이름 그대로 나가야 한다. <b>이 글자가 계약이다.</b>
    /// </summary>
    [Fact]
    public void 보던_자리는_JS_와_같은_이름으로_나간다()
    {
        var json = new ErdModel
        {
            View = new ErdViewport
            {
                Scale = 1.75,
                Dx = 33,
                Dy = -12,
                Minimap = false,
                Background = "grid",
                Tools = false,
                Pinned = false,
            },
        }.ToJson();

        using var doc = JsonDocument.Parse(json);
        var view = doc.RootElement.GetProperty("view");

        Assert.Equal(1.75, view.GetProperty("scale").GetDouble());
        Assert.Equal(33, view.GetProperty("dx").GetDouble());
        Assert.Equal(-12, view.GetProperty("dy").GetDouble());
        Assert.False(view.GetProperty("minimap").GetBoolean());
        Assert.Equal("grid", view.GetProperty("background").GetString());
        Assert.False(view.GetProperty("tools").GetBoolean());
        Assert.False(view.GetProperty("pinned").GetBoolean());
    }

    /// <summary>
    /// JS 가 돌려준 모양을 그대로 읽는다(<c>save()</c> 의 <c>view</c>).
    /// </summary>
    [Fact]
    public void JS_가_준_모양을_그대로_읽는다()
    {
        var model = ErdModel.Parse(
            """
            {"entities":[],"relations":[],
             "view":{"scale":0.6,"dx":-40,"dy":120,"minimap":true,"background":"dots",
                     "tools":false,"pinned":true}}
            """);

        Assert.NotNull(model.View);
        Assert.Equal(0.6, model.View!.Scale);
        Assert.Equal(-40, model.View.Dx);
        Assert.Equal(120, model.View.Dy);
        Assert.True(model.View.Minimap);
        Assert.Equal("dots", model.View.Background);
        Assert.False(model.View.Tools);
        Assert.True(model.View.Pinned);
    }

    /// <summary>
    /// <b>옛 저장본은 <c>null</c> 이다.</b> 기본값으로 읽으면 화면을 되돌려
    /// 버린다 — 「열 때마다 배율이 100% 로 튄다」가 된다.
    /// </summary>
    [Fact]
    public void 옛_저장본에는_보던_자리가_없다()
    {
        var model = ErdModel.Parse("""{"entities":[{"id":"t","name":"t"}],"relations":[]}""");

        Assert.Null(model.View);
    }

    /// <summary>
    /// 안 적은 칸은 <b>기본 설정</b>이 된다 — 도구상자 폄 · 미니맵 켬 · 바탕 없음.
    /// </summary>
    [Fact]
    public void 안_적은_칸은_기본_설정이다()
    {
        var model = ErdModel.Parse("""{"entities":[],"relations":[],"view":{"scale":1.2}}""");

        Assert.NotNull(model.View);
        Assert.Equal(1.2, model.View!.Scale);
        Assert.True(model.View.Minimap);
        Assert.Equal("none", model.View.Background);
        Assert.True(model.View.Tools);
        Assert.True(model.View.Pinned);
    }
}
