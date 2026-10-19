using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

/// <summary>모델이 부를 수 있는 웹 도구들. 설명문(Description)이 곧 사용 설명서라 모델은 그 글만 보고 언제 부를지 정한다.
/// 검색과 본문 가져오기를 나눠 두어, 요약으로 답할 수 있는 질문은 검색만 하게 한다.</summary>
[McpServerToolType]
public sealed class WebSearchTools
{
    private readonly WebSearchService _search;
    private readonly WebFetchService _fetch;

    public WebSearchTools(WebSearchService search, WebFetchService fetch)
    {
        _search = search;
        _fetch = fetch;
    }

    [McpServerTool(Name = "web_search")]
    [Description("웹을 검색한다. 최신 정보, 바깥 세상의 사실, 모르는 고유명사를 확인할 때 쓴다. "
               + "제목·주소·요약을 돌려준다. 답에 쓴 내용은 반드시 주소를 함께 밝혀야 한다.")]
    public async Task<object> SearchAsync(
        [Description("검색어. 사용자의 말 그대로보다 핵심 낱말로 다듬어 넣는 편이 잘 찾는다.")]
        string query,
        [Description("받을 결과 수. 기본 5, 최대 10.")]
        int count = 5,
        CancellationToken ct = default)
    {
        var results = await _search.SearchAsync(query, Math.Clamp(count, 1, 10), ct);

        return new
        {
            query,
            count = results.Count,
            results = results.Select(r => new { r.Title, r.Url, r.Snippet }),
        };
    }

    [McpServerTool(Name = "web_fetch")]
    [Description("주소 하나를 열어 본문 글자를 가져온다. 검색 요약만으로 모자랄 때, "
               + "검색으로 찾은 주소를 넣어 부른다. 광고·메뉴를 걷어낸 본문만 돌려준다.")]
    public async Task<object> FetchAsync(
        [Description("가져올 페이지 주소(http/https).")]
        string url,
        [Description("가져올 최대 글자 수. 기본 6000.")]
        int maxChars = 6000,
        CancellationToken ct = default)
    {
        var page = await _fetch.FetchAsync(url, Math.Clamp(maxChars, 500, 20000), ct);

        return new
        {
            url = page.Url,
            title = page.Title,
            text = page.Text,
            truncated = page.Truncated,
        };
    }
}
