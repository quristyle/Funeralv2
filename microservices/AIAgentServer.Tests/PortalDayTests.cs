using System;

using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 업무 비서 도구가 받는 "yyyy-MM-dd" 한국 날짜를 UTC 조회 경계로 바꾸는 자리.
/// </summary>
/// 끝 경계를 "그날 23:59:59" 로 잡으면 그 뒤 소수초에 찍힌 자료가 조용히 빠진다 — 그래서
/// 끝은 포함이 아니라 <b>다음 날 0시를 담은 제외 경계</b>여야 한다(NotificationServer 의
/// StartOfDayUtc(to.AddDays(1)) 과 같은 규칙). 모델이 만든 날짜 글이 형식을 어기면 엉뚱한
/// 범위를 조용히 돌려주는 대신 그 자리에서 터뜨려야, 안으로 들어가 틀린 0건으로 둔갑하지 않는다.
public class PortalDayTests
{
    [Fact]
    public void 한국_날짜의_시작은_전날_UTC_15시다()
        => Assert.Equal(new DateTime(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc), PortalDay.StartUtc("2026-10-17"));

    [Fact]
    public void 한국_날짜의_끝은_다음날_UTC_15시다_배제_경계()
        => Assert.Equal(new DateTime(2026, 10, 17, 15, 0, 0, DateTimeKind.Utc), PortalDay.EndExclusiveUtc("2026-10-17"));

    [Fact]
    public void 시작과_끝_모두_Kind가_Utc다()
    {
        Assert.Equal(DateTimeKind.Utc, PortalDay.StartUtc("2026-10-17").Kind);
        Assert.Equal(DateTimeKind.Utc, PortalDay.EndExclusiveUtc("2026-10-17").Kind);
    }

    [Fact]
    public void UTC_를_한국_시각_글로_바꾼다()
        => Assert.Equal("2026-10-17 00:30", PortalDay.ToKorean(new DateTime(2026, 10, 16, 15, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void 형식이_틀린_날짜는_그_자리에서_터뜨린다()
        => Assert.Throws<ArgumentException>(() => PortalDay.StartUtc("2026/10/17"));

    [Fact]
    public void 빈_날짜_문자열도_터뜨린다()
        => Assert.Throws<ArgumentException>(() => PortalDay.EndExclusiveUtc(""));
}
