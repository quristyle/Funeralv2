using System.Linq;
using System.Text.Json;

using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>업무 비서 도구 목록의 생김새 — 내부 토큰 유무에 따라 열리는 도구 수가 갈린다.</summary>
public class PortalToolSetTests
{
    /// <summary>토큰이 없으면 다른 서비스에 묻는 도구는 전부 닫힌다. 남는 둘은 이 서비스가
    /// 제 DB·제 색인에서 바로 읽는 것들이다 — 메일 보관함과 사내 문서함.</summary>
    [Fact]
    public void 내부_토큰이_없으면_제_DB_로_답하는_도구만_남는다()
    {
        var names = PortalToolSet.Tools(internalEnabled: false).Select(t => t.Name).ToArray();
        Assert.Equal(["search_gmail", "search_documents"], names);
    }

    [Fact]
    public void 내부_토큰이_있으면_여덟_가지를_연다()
    {
        var names = PortalToolSet.Tools(internalEnabled: true).Select(t => t.Name).ToArray();
        Assert.Equal(8, names.Length);
        Assert.Contains("find_user", names);
        Assert.Contains("search_requests", names);
        Assert.Contains("search_mails", names);
        Assert.Contains("search_gmail", names);
    }

    /// <summary><c>search_gmail</c> 은 <b>본인 메일함만</b> 본다. 모델이 범위를 정할 길을 두면
    /// 안 되므로 계정·사람을 받는 인자가 아예 없어야 한다 — 있으면 남의 메일이 샌다.</summary>
    [Fact]
    public void 메일_도구는_남의_메일함을_가리킬_인자가_없다()
    {
        var gmail = PortalToolSet.Tools(internalEnabled: true).Single(t => t.Name == "search_gmail");
        var props = gmail.Schema.GetProperty("properties");

        foreach (var forbidden in new[] { "userId", "accountId", "owner", "email" })
        {
            Assert.False(props.TryGetProperty(forbidden, out _), forbidden);
        }
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
