using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 마인드맵의 <b>mermaid 글 ↔ 나무</b> 변환을 못박는다.
///
/// <para>[왜 이것만 테스트하나]</para>
///
/// <para>
/// 이 화면에서 <b>손으로 짠 논리는 이 변환 하나뿐</b>이다. 나머지(그리기·
/// 배치·저장)는 maxgraph 와 게이트웨이가 하고 브라우저에서 눈으로 보면
/// 드러나지만, 이쪽은 <b>틀려도 조용하다</b> — 들여쓰기를 한 칸 잘못 읽으면
/// 가지가 엉뚱한 부모에 붙고, 그림은 멀쩡해 보인다. 사람이 「내가 그렇게
/// 적었나」 하고 넘어가는 종류다.
/// </para>
///
/// <para>
/// 게다가 이 글은 <b>바깥과 주고받는 형식</b>이다(mermaid). 우리가 뱉은 것을
/// 남의 도구가 읽고, 남이 적은 것을 우리가 읽는다. 그 약속이 깨지는 것은
/// 화면 하나가 깨지는 것보다 나쁘다.
/// </para>
/// </summary>
public sealed class MindMapModelTests
{
    /// <summary>
    /// 들여쓴 만큼 층이 된다. mermaid 가 층을 정하는 유일한 방법이다.
    /// </summary>
    [Fact]
    public void 들여쓰기가_층을_만든다()
    {
        var model = MindMapModel.FromMermaid("""
            mindmap
              root((중심))
                가지 하나
                  잎 하나
                  잎 둘
                가지 둘
            """);

        Assert.Equal("중심", model.Root.Text);
        Assert.Equal(2, model.Root.Children.Count);

        var first = model.Root.Children[0];
        Assert.Equal("가지 하나", first.Text);
        Assert.Equal(["잎 하나", "잎 둘"], first.Children.Select(c => c.Text));

        Assert.Equal("가지 둘", model.Root.Children[1].Text);
        Assert.Empty(model.Root.Children[1].Children);

        // 뿌리까지 다섯이다 — 화면이 「읽은 마디 수」로 보여 주는 값이다.
        Assert.Equal(5, model.Count);
    }

    /// <summary>
    /// 감싸는 기호가 모양을 정한다. <b>여섯 가지 전부</b> — 하나라도 빠지면
    /// 그 모양으로 그린 마디가 다음 저장에서 네모가 된다.
    /// </summary>
    [Theory]
    [InlineData("a[네모]", MindMapShape.Square)]
    [InlineData("a(둥근)", MindMapShape.Rounded)]
    [InlineData("a((원))", MindMapShape.Circle)]
    [InlineData("a))폭발((", MindMapShape.Bang)]
    [InlineData("a)구름(", MindMapShape.Cloud)]
    [InlineData("a{{육각}}", MindMapShape.Hexagon)]
    [InlineData("그냥 글자", MindMapShape.Default)]
    public void 감싸는_기호가_모양을_정한다(string line, string shape)
    {
        var model = MindMapModel.FromMermaid($"mindmap\n  {line}");

        Assert.Equal(shape, model.Root.Shape);
    }

    /// <summary>
    /// 적어 낸 글을 다시 읽으면 같은 나무가 나온다.
    ///
    /// <para>
    /// <b>이것이 이 형식의 값어치 전부다.</b> 한쪽만 맞으면 「문서에 붙여 넣을
    /// 수는 있는데 되가져오면 뭉개지는」 도구가 된다.
    /// </para>
    /// </summary>
    [Fact]
    public void 글로_적고_다시_읽으면_같다()
    {
        var source = MindMapModel.FromMermaid("""
            mindmap
              root((장례식장 v2))
                설계
                  erd[ERD]
                  flow(업무 흐름)
                개발
                  ))핵심((
                  )나중에(
                  {{보류}}
            """);

        var again = MindMapModel.FromMermaid(source.ToMermaid());

        Assert.Equal(source.ToMermaid(), again.ToMermaid());
        Assert.Equal(source.Count, again.Count);
        Assert.Equal(MindMapShape.Bang, again.Root.Children[1].Children[0].Shape);
        Assert.Equal("나중에", again.Root.Children[1].Children[1].Text);
    }

    /// <summary>
    /// <b>글자에 든 괄호가 모양으로 읽히면 안 된다.</b>
    ///
    /// <para>
    /// 「매출(순)」 같은 우리말 제목에서 바로 걸린다. 적을 때 따옴표로 묶고,
    /// 읽을 때 벗긴다 — 묶지 않으면 되읽기가 엉뚱한 자리에서 잘라 글자가
    /// 반쪽이 되고, 모양까지 바뀐다.
    /// </para>
    /// </summary>
    [Fact]
    public void 괄호가_든_글자도_되읽힌다()
    {
        var model = new MindMapModel
        {
            Root = new MindMapNode { Text = "매출(순)", Shape = MindMapShape.Default },
        };

        model.Root.Children.Add(new MindMapNode { Text = "비고 [임시]", Shape = MindMapShape.Square });

        var again = MindMapModel.FromMermaid(model.ToMermaid());

        Assert.Equal("매출(순)", again.Root.Text);
        Assert.Equal("비고 [임시]", again.Root.Children[0].Text);
        Assert.Equal(MindMapShape.Square, again.Root.Children[0].Shape);
    }

    /// <summary>
    /// 줄바꿈은 <c>&lt;br/&gt;</c> 으로 오간다. mermaid 가 그렇게 적는다.
    /// </summary>
    [Fact]
    public void 줄바꿈은_br_로_오간다()
    {
        var model = new MindMapModel
        {
            Root = new MindMapNode { Text = "두 줄\n짜리", Shape = MindMapShape.Circle },
        };

        var text = model.ToMermaid();

        Assert.Contains("<br/>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("두 줄\n짜리", text, StringComparison.Ordinal);
        Assert.Equal("두 줄\n짜리", MindMapModel.FromMermaid(text).Root.Text);
    }

    /// <summary>
    /// <c>::icon</c> · <c>:::클래스</c> 는 <b>새 마디가 아니라 앞 마디의 꾸밈</b>이다.
    ///
    /// <para>
    /// 마디로 세면 층이 통째로 밀린다 — 그 줄 아래 것들이 아이콘의 자식이 된다.
    /// </para>
    /// </summary>
    [Fact]
    public void 아이콘과_클래스는_마디가_아니다()
    {
        var model = MindMapModel.FromMermaid("""
            mindmap
              root((중심))
                배포
                ::icon(fa fa-rocket)
                :::강조 큼
                  운영 반영
            """);

        Assert.Single(model.Root.Children);

        var deploy = model.Root.Children[0];
        Assert.Equal("배포", deploy.Text);
        Assert.Equal("fa fa-rocket", deploy.Icon);
        Assert.Equal("강조 큼", deploy.ClassName);

        // 아이콘 줄이 층을 먹지 않았다 — 「운영 반영」은 배포의 자식이다.
        Assert.Equal(["운영 반영"], deploy.Children.Select(c => c.Text));
    }

    /// <summary>
    /// 못 읽는 글이 들어와도 <b>던지지 않는다.</b> 사람이 손으로 적는 칸이라
    /// 반쯤 적다 만 상태가 정상이다.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mindmap")]
    [InlineData("%% 주석만 있다")]
    public void 빈_글은_빈_마인드맵이_된다(string? text)
    {
        var model = MindMapModel.FromMermaid(text);

        Assert.Equal(MindMapModel.Empty.Root.Text, model.Root.Text);
        Assert.Empty(model.Root.Children);
    }

    /// <summary>
    /// 저장본(JSON)이 상해 있으면 <b>빈 마인드맵</b>을 준다. 화면이 죽으면
    /// 고칠 길도 함께 없어진다(<see cref="ErdModel.Parse"/> 와 같은 판단).
    /// </summary>
    [Fact]
    public void 상한_저장본은_빈_마인드맵이_된다()
    {
        Assert.Empty(MindMapModel.Parse("{ 이건 JSON 이 아니다").Root.Children);
    }

    /// <summary>
    /// 저장본 자리에 <b>mermaid 글</b>이 들어 있어도 읽는다.
    ///
    /// <para>
    /// 사람이 [프로젝트 DB 속성] 화면에서 값을 손으로 적어 넣을 수 있고, 그때
    /// 적는 것은 십중팔구 JSON 이 아니라 mermaid 다.
    /// </para>
    /// </summary>
    [Fact]
    public void 저장본이_mermaid_글이어도_읽는다()
    {
        var model = MindMapModel.Parse("mindmap\n  root((손으로 적었다))\n    가지");

        Assert.Equal("손으로 적었다", model.Root.Text);
        Assert.Equal(["가지"], model.Root.Children.Select(c => c.Text));
    }

    /// <summary>
    /// <b>마디 수는 저장본에 담기지 않는다.</b> 나무에서 세면 나오는 값이라
    /// 담아 봐야 옛 값이 남을 뿐이다 — 한동안 <c>"count"</c> 가 같이 적혀 나갔다.
    /// </summary>
    [Fact]
    public void 마디_수는_저장본에_담기지_않는다()
    {
        var json = MindMapModel.Empty.ToJson();

        Assert.DoesNotContain("\"count\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"root\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// 접어 둔 가지는 <b>저장본에 남는다.</b> 「지금 보고 있는 상태」가 아니라
    /// 「이 가지는 접어 두는 것이 낫다」는 사람의 판단이기 때문이다.
    /// </summary>
    [Fact]
    public void 접힘은_저장본에_남는다()
    {
        var model = MindMapModel.Empty;
        model.Root.Children.Add(new MindMapNode { Id = "n2", Text = "가지", Collapsed = true });

        var again = MindMapModel.Parse(model.ToJson());

        Assert.True(again.Root.Children[0].Collapsed);
    }
}
