using System.Text.Json;
using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>다이어그램의 보던 자리(ErdViewport)가 JS 와 같은 이름으로 오가는지 못박는다. 이 이름은 C# 과 JS 가 글자로 약속한 것이라, 한쪽을 고치면 예외 없이 그 값만 조용히 사라진다.
/// 옛 저장본에는 칸이 없으므로 기본값으로 읽지 않고 null 이어야 한다.</summary>
public sealed class ErdViewportTests
{
    /// <summary>JS 가 읽고 쓰는 이름 그대로 나가야 한다 — 이 글자가 계약이다.</summary>
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

    /// <summary>JS 가 돌려준 모양을 그대로 읽는다(save() 의 view).</summary>
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
