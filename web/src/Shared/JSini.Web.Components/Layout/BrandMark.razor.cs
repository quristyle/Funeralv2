using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class BrandMark
{
    /// <summary>가로 픽셀. 심볼이 84×60 비율이라 높이는 여기서 계산한다.</summary>
    [Parameter] public int Size { get; set; } = 38;
}
