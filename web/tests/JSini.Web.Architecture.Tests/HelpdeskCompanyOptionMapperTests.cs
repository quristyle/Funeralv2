using System.Text.Json;
using JSini.Web.HelpDesk.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class HelpdeskCompanyOptionMapperTests
{
    [Fact]
    public void 회사명이_달라도_이관_비고의_헬프데스크_ID로_사용처_회사를_찾는다()
    {
        using var portalCompanies = JsonDocument.Parse("""
            [
              { "name": "포털 회사 A", "remark": "helpdesk:company:12" },
              { "name": "포털 회사 B", "remark": "helpdesk:company:34" },
              { "name": "일반 회사", "remark": "다른 용도" },
              { "name": "잘못된 연결", "remark": "helpdesk:company:abc" }
            ]
            """);
        BizOption[] helpdeskOptions =
        [
            new("헬프데스크 회사 12", "12"),
            new("헬프데스크 회사 23", "23"),
            new("헬프데스크 회사 34", "34"),
        ];

        var result = HelpdeskCompanyOptionMapper.ForPortalCompanies(
            helpdeskOptions, portalCompanies.RootElement.EnumerateArray());

        Assert.Equal(["12", "34"], result.Select(option => option.Value));
    }
}
