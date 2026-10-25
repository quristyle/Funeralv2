using System.Linq;
using System.Text.Json;

using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>업무 비서 도구 목록의 생김새 — 내부 토큰 유무에 따라 열리는 도구 수가 갈린다.</summary>
public class PortalToolSetTests
{
    [Fact]
    public void 내부_토큰이_없으면_문서_도구_하나만_남는다()
    {
        var tools = PortalToolSet.Tools(internalEnabled: false);
        Assert.Single(tools);
        Assert.Equal("search_documents", tools[0].Name);
    }

    [Fact]
    public void 내부_토큰이_있으면_일곱_가지를_연다()
    {
        var names = PortalToolSet.Tools(internalEnabled: true).Select(t => t.Name).ToArray();
        Assert.Equal(7, names.Length);
        Assert.Contains("find_user", names);
        Assert.Contains("search_requests", names);
        Assert.Contains("search_mails", names);
    }

    [Fact]
    public void 모든_도구가_설명과_스키마를_갖는다()
    {
        foreach (var t in PortalToolSet.Tools(true))
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Description), t.Name);
            Assert.Equal(JsonValueKind.Object, t.Schema.ValueKind);
        }
    }
}
