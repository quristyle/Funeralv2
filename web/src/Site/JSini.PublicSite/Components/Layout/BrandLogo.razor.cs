using Microsoft.AspNetCore.Components;

namespace JSini.PublicSite.Components.Layout;

public partial class BrandLogo
{
    /// <summary>어두운 배경 위에 놓을 때 켠다 (녹아웃).</summary>
    [Parameter] public bool Knockout { get; set; }

    /// <summary>픽셀 높이. 가로 조합은 60 유닛 높이라 이 값이 그대로 높이가 된다.</summary>
    [Parameter] public int Height { get; set; } = 28;
}
