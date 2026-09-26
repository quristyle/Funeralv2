using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Settings;

public partial class LocationMap
{
    /// <summary>찍을 자리의 위도(10진 도).</summary>
    [Parameter, EditorRequired] public double Lat { get; set; }

    /// <summary>찍을 자리의 경도(10진 도).</summary>
    [Parameter, EditorRequired] public double Lon { get; set; }

    /// <summary>
    /// 판에 담을 상자의 반지름(도). 기본 <c>0.004</c> 는 대략 <b>사방 800m</b> 다.
    /// </summary>
    /// <remarks>
    /// 동네 이름이 읽힐 만큼은 가깝고, 큰길 하나는 들어올 만큼은 넓은 값이다.
    /// 더 조이면 건물만 보여 어디인지 알 수 없고, 넓히면 점이 어느 동네에
    /// 찍혔는지 가늠이 안 된다.
    /// </remarks>
    [Parameter] public double Span { get; set; } = 0.004;

    /// <summary>
    /// 끼워 넣기 판 주소. <b>숫자는 반드시 불변 문화권으로 찍는다</b> —
    /// 소수점이 쉼표인 문화권에서 그리면 상자 네 값이 통째로 어긋난다.
    /// </summary>
    private string EmbedUrl => FormattableString.Invariant(
        $"https://www.openstreetmap.org/export/embed.html?bbox={Lon - Span:0.######}%2C{Lat - Span:0.######}%2C{Lon + Span:0.######}%2C{Lat + Span:0.######}&layer=mapnik&marker={Lat:0.######}%2C{Lon:0.######}");

    /// <summary>같은 자리를 큰 지도에서 여는 주소.</summary>
    private string LinkUrl => FormattableString.Invariant(
        $"https://www.openstreetmap.org/?mlat={Lat:0.######}&mlon={Lon:0.######}#map=16/{Lat:0.######}/{Lon:0.######}");

    /// <summary>판 아래 적어 두는 좌표. 지도가 안 떠도 이것은 남는다.</summary>
    private string CoordText => FormattableString.Invariant($"{Lat:0.####}, {Lon:0.####}");
}
