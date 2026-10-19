using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>마인드맵의 mermaid 글 ↔ 나무 변환을 못 박는다. 이 화면에서 손으로 짠 논리는 이 변환 하나뿐이고, 틀려도 조용하다(들여쓰기를 잘못 읽으면 가지가 엉뚱한 부모에 붙고 그림은 멀쩡해 보인다).
/// 이 글은 바깥과 주고받는 형식이라 그 약속이 깨지는 것이 화면 하나가 깨지는 것보다 나쁘다.</summary>
public sealed class MindMapModelTests
{
    /// <summary>들여쓴 만큼 층이 된다. mermaid 가 층을 정하는 유일한 방법이다.</summary>
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

    /// <summary>감싸는 기호가 모양을 정한다 — 여섯 가지 전부를 지켜야 한다. 하나라도 빠지면 그 모양의 마디가 다음 저장에서 네모가 된다.</summary>
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

    /// <summary>적어 낸 글을 다시 읽으면 같은 나무가 나온다 — 이 형식의 값어치 전부다. 한쪽만 맞으면 되가져오면 뭉개지는 도구가 된다.</summary>
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

    /// <summary>글자에 든 괄호가 모양으로 읽히면 안 된다(「매출(순)」 같은 제목). 적을 때 따옴표로 묶고 읽을 때 벗긴다.</summary>
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

    /// <summary>::icon · :::클래스 는 새 마디가 아니라 앞 마디의 꾸밈이다 — 마디로 세면 그 아래 것들이 층째 밀린다.</summary>
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

    /// <summary>못 읽는 글이 들어와도 던지지 않는다 — 사람이 손으로 적는 칸이라 반쯤 적다 만 상태가 정상이다.</summary>
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

    /// <summary>저장본(JSON)이 상해 있으면 빈 마인드맵을 준다 — 화면이 죽으면 고칠 길도 함께 없어진다.</summary>
    [Fact]
    public void 상한_저장본은_빈_마인드맵이_된다()
    {
        Assert.Empty(MindMapModel.Parse("{ 이건 JSON 이 아니다").Root.Children);
    }

    /// <summary>저장본 자리에 mermaid 글이 들어 있어도 읽는다 — 사람이 값을 손으로 적어 넣을 때 적는 것은 십중팔구 mermaid 다.</summary>
    [Fact]
    public void 저장본이_mermaid_글이어도_읽는다()
    {
        var model = MindMapModel.Parse("mindmap\n  root((손으로 적었다))\n    가지");

        Assert.Equal("손으로 적었다", model.Root.Text);
        Assert.Equal(["가지"], model.Root.Children.Select(c => c.Text));
    }

    /// <summary>마디 수는 저장본에 담지 않는다 — 나무에서 세면 나오는 값이라 담아 봐야 옛 값이 남는다.</summary>
    [Fact]
    public void 마디_수는_저장본에_담기지_않는다()
    {
        var json = MindMapModel.Empty.ToJson();

        Assert.DoesNotContain("\"count\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"root\"", json, StringComparison.Ordinal);
    }

    /// <summary>접어 둔 가지는 저장본에 남는다 — 지금 보는 상태가 아니라 「이 가지는 접어 두는 것이 낫다」는 사람의 판단이기 때문이다.</summary>
    [Fact]
    public void 접힘은_저장본에_남는다()
    {
        var model = MindMapModel.Empty;
        model.Root.Children.Add(new MindMapNode { Id = "n2", Text = "가지", Collapsed = true });

        var again = MindMapModel.Parse(model.ToJson());

        Assert.True(again.Root.Children[0].Collapsed);
    }
}
