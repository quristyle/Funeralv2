using CargoTrustServer.Common;
using CargoTrustServer.Vehicles;
using Xunit;

namespace CargoTrustServer.Tests;

/// <summary>
/// 번호판 해석.
///
/// <para>
/// 바깥 API 를 안 쓰기로 한 자리라, 여기가 틀리면 <b>차종과 사업용 여부가
/// 조용히 틀린 채로</b> 차량에 찍힌다. 그리고 그 둘은 심야할인 대상 자격의
/// 핵심이라 「왜 할인이 안 되지」로만 드러난다.
/// </para>
///
/// <para>
/// 못 읽는 번호판을 억지로 판정하지 않는 것도 여기서 못 박는다 —
/// 틀리게 단정하는 것보다 모른다고 두는 편이 낫다.
/// </para>
/// </summary>
public class PlateNumberTests
{
    // ── 사업용 화물차 ────────────────────────────────────────

    [Theory]
    [InlineData("서울80바1234")]
    [InlineData("서울 80바 1234")]
    [InlineData(" 서울80바1234 ")]
    public void 사업용_화물차는_공백을_어디에_넣든_같이_읽는다(string raw)
    {
        var r = PlateNumber.Read(raw);

        Assert.Equal("서울", r.Region);
        Assert.Equal(80, r.ClassNumber);
        Assert.Equal(VehicleKind.FREIGHT, r.Kind);
        Assert.Equal(PlateUsage.BUSINESS, r.Usage);
        Assert.True(r.IsBusiness);
        Assert.True(r.IsFreight);
        Assert.Equal("서울80바1234", r.Normalized);
    }

    [Theory]
    [InlineData('아')]
    [InlineData('바')]
    [InlineData('사')]
    [InlineData('자')]
    public void 운수사업용_네_글자(char usage)
    {
        Assert.Equal(PlateUsage.BUSINESS, PlateNumber.Read($"경기80{usage}1234").Usage);
    }

    // ── 차종 ─────────────────────────────────────────────────

    [Theory]
    [InlineData("12가3456", VehicleKind.PASSENGER)]   // 01~69 승용
    [InlineData("69가3456", VehicleKind.PASSENGER)]
    [InlineData("70가3456", VehicleKind.VAN)]        // 70~79 승합
    [InlineData("79가3456", VehicleKind.VAN)]
    [InlineData("80바1234", VehicleKind.FREIGHT)]    // 80~97 화물
    [InlineData("97바1234", VehicleKind.FREIGHT)]
    [InlineData("98바1234", VehicleKind.SPECIAL)]    // 98~99 특수
    [InlineData("99바1234", VehicleKind.SPECIAL)]
    public void 앞자리_두_숫자가_차종을_가른다(string plate, VehicleKind expected)
    {
        Assert.Equal(expected, PlateNumber.Read(plate).Kind);
    }

    [Theory]
    [InlineData("123가4567", VehicleKind.PASSENGER)] // 세 자리는 같은 구간의 열 배
    [InlineData("699가4567", VehicleKind.PASSENGER)]
    [InlineData("800바4567", VehicleKind.FREIGHT)]
    public void 세_자리_번호판도_받는다(string plate, VehicleKind expected)
    {
        Assert.Equal(expected, PlateNumber.Read(plate).Kind);
    }

    // ── 사업용이 아닌 것들 ───────────────────────────────────

    [Fact]
    public void 자가용은_비사업용이다()
    {
        var r = PlateNumber.Read("12가3456");

        Assert.Null(r.Region);
        Assert.Equal(PlateUsage.PRIVATE, r.Usage);
        Assert.False(r.IsBusiness);
    }

    [Fact]
    public void 택배와_렌터카는_따로_읽는다()
    {
        Assert.Equal(PlateUsage.DELIVERY, PlateNumber.Read("80배1234").Usage);
        Assert.Equal(PlateUsage.RENTAL, PlateNumber.Read("12허3456").Usage);

        // 렌터카는 사업용이 아니다 — 심야할인 대상이 아니라는 뜻이다.
        Assert.False(PlateNumber.Read("12허3456").IsBusiness);
    }

    [Fact]
    public void 자가용_화물차도_있다()
    {
        // 앞자리는 화물인데 한글이 자가용이다. 둘을 따로 읽어야 이것이 잡힌다 —
        // 「화물차니까 사업용」으로 뭉치면 대상이 아닌 차가 대상이 된다.
        var r = PlateNumber.Read("80가1234");

        Assert.True(r.IsFreight);
        Assert.False(r.IsBusiness);
    }

    // ── 못 읽는 것 ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("외교012345")]        // 외교 번호판
    [InlineData("12-3456")]           // 옛 꼴
    [InlineData("ABC1234")]           // 우리 것이 아니다
    [InlineData("80바123")]           // 일련번호가 모자라다
    [InlineData("00가1234")]          // 구간 밖
    public void 꼴이_안_맞으면_모른다고_둔다(string? raw)
    {
        var r = PlateNumber.Read(raw);

        Assert.True(r.Unreadable || r.Kind == VehicleKind.UNKNOWN);
        // **단정하지 않는다.** 모를 때 false 로 두면 「사업용이 아니다」가 되어
        // 멀쩡한 차가 대상에서 빠진다.
        if (r.Unreadable)
        {
            Assert.Null(r.IsBusiness);
            Assert.Null(r.IsFreight);
            Assert.Null(r.SuggestedClass);
        }
    }

    // ── 제안 차종 ────────────────────────────────────────────

    [Fact]
    public void 화물차면_사종을_제안하되_축수는_모른다()
    {
        // 번호판은 축수를 말해 주지 않는다. 4종(3축)과 5종(4축 이상)을 가를 수
        // 없으므로 흔한 쪽을 제안하고 사람이 고치게 둔다.
        Assert.Equal(VehicleClass.C4, PlateNumber.Read("서울80바1234").SuggestedClass);
        Assert.Equal(VehicleClass.C1, PlateNumber.Read("12가3456").SuggestedClass);
        Assert.Null(PlateNumber.Read("외교012345").SuggestedClass);
    }

    [Fact]
    public void 요약은_사람이_읽을_말이다()
    {
        Assert.Equal("서울 · 화물차 · 사업용(영업용)", PlateNumber.Read("서울80바1234").Summary());
        Assert.Contains("읽지 못했습니다", PlateNumber.Read("ABC1234").Summary());
    }
}
