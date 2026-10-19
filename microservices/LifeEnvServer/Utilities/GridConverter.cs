namespace LifeEnvServer.Utilities;

/// <summary>
/// 위경도 → 기상청 격자(nx·ny) 변환(dfs_xy_conv 와 같은 Lambert 식). 기상청 API 는 위경도를 받지 않는다.
/// 격자표의 중심점에서 가까운 행을 찾지 않고 계산한다 — 중심점은 실제 격자와 한 칸씩 어긋날 수 있다. 표는 이름을 찾는 데만 쓴다.
/// </summary>
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

    /// <summary>한반도 안의 값인가. 변환 전에 본다 — 밖의 값은 조용히 숫자를 내고, 그 격자로 물으면 자료 없음만 돌아온다.</summary>
    public static bool IsInKorea(double latitude, double longitude) =>
        latitude is >= 32.0 and <= 44.0 && longitude is >= 123.0 and <= 133.0;
}
