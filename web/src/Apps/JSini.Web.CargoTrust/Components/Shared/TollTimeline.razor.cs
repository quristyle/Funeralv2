using System.Globalization;
using JSini.Web.CargoTrust.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class TollTimeline
{
    [Parameter, EditorRequired] public DateTime Entry { get; set; }
    [Parameter, EditorRequired] public DateTime Exit { get; set; }
    [Parameter] public IReadOnlyList<TollNightSegmentInfo> Segments { get; set; } = [];

    /// <summary>
    /// 토막마다 <c>left</c> · <c>width</c> 를 백분율로 만든다.
    ///
    /// <para>
    /// 서버가 준 토막은 이미 운행 구간 안으로 잘려 있지만, 떠내려온 값이 밖을
    /// 가리켜도 막대가 깨지지 않게 0~100 으로 다시 한 번 가둔다.
    /// </para>
    /// </summary>
    private IEnumerable<string> Bands
    {
        get
        {
            var total = (Exit - Entry).TotalMinutes;
            if (total <= 0) yield break;

            foreach (var segment in Segments)
            {
                var start = Clamp((segment.FromKst - Entry).TotalMinutes / total * 100);
                var end = Clamp((segment.ToKst - Entry).TotalMinutes / total * 100);
                if (end <= start) continue;

                yield return string.Create(CultureInfo.InvariantCulture,
                    $"left:{start:0.##}%;width:{end - start:0.##}%");
            }
        }
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}
