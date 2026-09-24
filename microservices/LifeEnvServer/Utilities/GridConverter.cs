namespace LifeEnvServer.Utilities;

/// <summary>
/// 위경도 → 기상청 격자(nx · ny) 변환.
/// </summary>
/// <remarks>
/// <para>
/// 기상청 동네예보 API 는 위경도를 받지 않는다. 5km 격자의 <c>nx</c>·<c>ny</c> 만
/// 받으므로, 브라우저가 준 위경도로 날씨를 물으려면 먼저 이 변환을 거쳐야 한다.
/// 공공데이터포털이 배포하는 <c>dfs_xy_conv</c>(Lambert Conformal Conic)를 그대로 옮겼다.
/// </para>
///
/// <para>
/// <b>왜 격자표(<c>ghub.grid_coordinates</c>)에서 가장 가까운 행을 찾지 않나.</b>
/// 그 표는 행정구역 3834 곳의 <b>중심점</b>이다. 중심점이 가까운 동네와 실제로 내가
/// 선 자리가 속한 격자는 다를 수 있고, 표에 위경도가 <c>0</c> 인 행도 섞여 있다
/// (실제로 두 줄). 격자는 계산으로 정확히 나오는 값이라 계산이 맞다 — 표는
/// <b>사람에게 보여 줄 이름</b>을 찾는 데만 쓴다(<see cref="LifeEnvServer.Services.LocalWeatherNotifyService"/>).
/// </para>
///
/// <para>
/// 확인해 보았다: 격자표 3834 행의 위경도를 이 함수에 넣으면 3789 행이 저장된
/// <c>nx</c>·<c>ny</c> 와 <b>정확히 같고</b>, 43 행이 한 칸 차이(중심점을 소수점에서
/// 반올림한 탓), 나머지 둘은 위경도가 <c>0</c> 인 빈 줄이었다.
/// </para>
/// </remarks>
public static class GridConverter
{
    private const double Re = 6371.00877;   // 지구 반경(km)
    private const double Grid = 5.0;        // 격자 간격(km)
    private const double Slat1 = 30.0;      // 투영 위도 1
    private const double Slat2 = 60.0;      // 투영 위도 2
    private const double Olon = 126.0;      // 기준점 경도
    private const double Olat = 38.0;       // 기준점 위도
    private const double Xo = 43.0;         // 기준점 X
    private const double Yo = 136.0;        // 기준점 Y

    private const double DegRad = Math.PI / 180.0;

    // 상수에서만 나오는 값이라 한 번 계산해 둔다.
    private static readonly double Sn;
    private static readonly double Sf;
    private static readonly double Ro;

    static GridConverter()
    {
        var re = Re / Grid;
        var slat1 = Slat1 * DegRad;
        var slat2 = Slat2 * DegRad;
        var olat = Olat * DegRad;

        var sn = Math.Tan(Math.PI * 0.25 + slat2 * 0.5) / Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
        Sn = Math.Log(Math.Cos(slat1) / Math.Cos(slat2)) / Math.Log(sn);

        var sf = Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
        Sf = Math.Pow(sf, Sn) * Math.Cos(slat1) / Sn;

        var ro = Math.Tan(Math.PI * 0.25 + olat * 0.5);
        Ro = re * Sf / Math.Pow(ro, Sn);
    }

    /// <summary>위경도(10진 도)를 격자 좌표로.</summary>
    public static (int Nx, int Ny) ToGrid(double latitude, double longitude)
    {
        var re = Re / Grid;
        var olon = Olon * DegRad;

        var ra = Math.Tan(Math.PI * 0.25 + latitude * DegRad * 0.5);
        ra = re * Sf / Math.Pow(ra, Sn);

        var theta = longitude * DegRad - olon;
        if (theta > Math.PI) theta -= 2.0 * Math.PI;
        if (theta < -Math.PI) theta += 2.0 * Math.PI;
        theta *= Sn;

        var nx = (int)Math.Floor(ra * Math.Sin(theta) + Xo + 0.5);
        var ny = (int)Math.Floor(Ro - ra * Math.Cos(theta) + Yo + 0.5);

        return (nx, ny);
    }

    /// <summary>
    /// 한반도 안의 값인가. <b>변환 전에 본다</b> — 밖의 값을 넣어도 함수는 조용히
    /// 숫자를 내놓고, 그 격자로 기상청에 물으면 자료가 없다는 응답만 온다.
    /// </summary>
    /// <remarks>
    /// 브라우저가 위치를 못 잡았을 때 <c>0, 0</c> 이 오는 일이 실제로 있다
    /// (격자표에도 그런 줄이 둘 있다). 기니만 앞바다의 날씨를 물을 일은 없다.
    /// </remarks>
    public static bool IsInKorea(double latitude, double longitude) =>
        latitude is >= 32.0 and <= 44.0 && longitude is >= 123.0 and <= 133.0;
}
