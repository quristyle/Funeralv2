using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>
/// 경량 SVG 모니터링 차트 컴포넌트.
/// DevExpress DxChart 가 클라이언트 JS 메인 스레드에서 8개 차트의 DOM/레이아웃을 동기 연산하여 화면을 얼리던 문제를 해결하기 위해,
/// C# 서버에서 SVG 패스를 마이크로초 단위로 직접 계산하여 브라우저에 내려보낸다.
/// JS 실행 부하 0ms 로 즉시 그려지며, 차트가 그려지는 동안과 1분 자동 갱신 중에도 다른 화면 동작이 전혀 끊기지 않는다.
/// </summary>
public partial class HanjuSysMonChart : ComponentBase
{
    [Parameter] public string Height { get; set; } = "170px";
    [Parameter] public IReadOnlyList<string> Labels { get; set; } = [];
    [Parameter] public IReadOnlyList<SysMonSeries> Series { get; set; } = [];
    [Parameter] public IReadOnlyList<SysMonRefLine>? RefLines { get; set; }
    [Parameter] public double? MinY { get; set; } = 0;
    [Parameter] public double? MaxY { get; set; }
    [Parameter] public double? TickInterval { get; set; }
    [Parameter] public bool ShowLegend { get; set; }
    [Parameter] public string Unit { get; set; } = "";
    [Parameter] public Func<int, string>? TooltipFormatter { get; set; }

    private double ViewBoxHeight => Height.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                                    double.TryParse(Height[..^2], out var h)
                                    ? h : 170;

    private const double MarginLeft = 44;
    private const double MarginRight = 12;
    private double MarginTop => ShowLegend ? 4 : 12;
    private const double MarginBottom = 22;

    private double PlotWidth => Math.Max(10, 500 - MarginLeft - MarginRight);
    private double PlotHeight => Math.Max(10, ViewBoxHeight - MarginTop - MarginBottom);
    private double BottomY => MarginTop + PlotHeight;

    private double EffectiveMin
    {
        get
        {
            var min = MinY ?? 0;
            var allVals = Series.SelectMany(s => s.Values).Concat(RefLines?.Select(r => r.Value) ?? []);
            if (allVals.Any())
            {
                min = Math.Min(min, allVals.Min());
            }
            return min;
        }
    }

    private double EffectiveMax
    {
        get
        {
            var allVals = Series.SelectMany(s => s.Values).Concat(RefLines?.Select(r => r.Value) ?? []);
            double max = allVals.Any() ? allVals.Max() : 100;
            if (MaxY.HasValue)
            {
                max = Math.Max(MaxY.Value, max);
            }
            else
            {
                max = max <= 0 ? 10 : Math.Ceiling(max * 1.1);
            }
            if (max <= EffectiveMin) max = EffectiveMin + 1;
            return max;
        }
    }

    private double YRange => Math.Max(0.0001, EffectiveMax - EffectiveMin);

    private double GetY(double val)
    {
        double clamped = Math.Clamp(val, EffectiveMin, EffectiveMax);
        return MarginTop + PlotHeight - ((clamped - EffectiveMin) / YRange) * PlotHeight;
    }

    private double GetX(int index)
    {
        if (Labels.Count <= 1) return MarginLeft + PlotWidth / 2.0;
        return MarginLeft + (index / (double)(Labels.Count - 1)) * PlotWidth;
    }

    private IReadOnlyList<double> YTicks
    {
        get
        {
            var ticks = new List<double>();
            if (TickInterval.HasValue && TickInterval.Value > 0)
            {
                for (double t = EffectiveMin; t <= EffectiveMax + 0.0001; t += TickInterval.Value)
                {
                    ticks.Add(t);
                }
            }
            else
            {
                int count = 4;
                double step = NiceStep(YRange / count);
                double start = Math.Ceiling(EffectiveMin / step) * step;
                for (double t = start; t <= EffectiveMax + 0.0001; t += step)
                {
                    ticks.Add(t);
                }
                if (!ticks.Contains(EffectiveMin)) ticks.Insert(0, EffectiveMin);
            }
            return ticks;
        }
    }

    private static double NiceStep(double range)
    {
        if (range <= 0) return 1;
        double exp = Math.Floor(Math.Log10(range));
        double frac = range / Math.Pow(10, exp);
        double nice = frac switch
        {
            <= 1.5 => 1,
            <= 3 => 2,
            <= 7 => 5,
            _ => 10
        };
        return nice * Math.Pow(10, exp);
    }

    private IReadOnlyList<int> VisibleXIndices
    {
        get
        {
            int n = Labels.Count;
            if (n == 0) return [];
            if (n <= 7) return Enumerable.Range(0, n).ToList();

            int step = n switch
            {
                <= 15 => 2,
                <= 30 => 4,
                <= 50 => 8,
                _ => 12
            };

            var list = new List<int>();
            for (int i = 0; i < n; i += step)
            {
                list.Add(i);
            }
            if (!list.Contains(n - 1) && (n - 1 - list[^1]) >= step / 2)
            {
                list.Add(n - 1);
            }
            return list;
        }
    }

    private string BuildSplinePath(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return string.Empty;
        if (values.Count == 1)
        {
            double x = GetX(0);
            double y = GetY(values[0]);
            return FormattableString.Invariant($"M {x:0.#} {y:0.#}");
        }

        var pts = new (double X, double Y)[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            pts[i] = (GetX(i), GetY(values[i]));
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"M {pts[0].X:0.#} {pts[0].Y:0.#}");

        for (int i = 0; i < pts.Length - 1; i++)
        {
            var p0 = i > 0 ? pts[i - 1] : pts[i];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i < pts.Length - 2 ? pts[i + 2] : p2;

            double cp1x = p1.X + (p2.X - p0.X) / 6.0;
            double cp1y = p1.Y + (p2.Y - p0.Y) / 6.0;
            double cp2x = p2.X - (p3.X - p1.X) / 6.0;
            double cp2y = p2.Y - (p3.Y - p1.Y) / 6.0;

            cp1y = Math.Clamp(cp1y, MarginTop, BottomY);
            cp2y = Math.Clamp(cp2y, MarginTop, BottomY);

            sb.Append(CultureInfo.InvariantCulture, $" C {cp1x:0.#} {cp1y:0.#}, {cp2x:0.#} {cp2y:0.#}, {p2.X:0.#} {p2.Y:0.#}");
        }

        return sb.ToString();
    }

    private string BuildAreaPath(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return string.Empty;
        var spline = BuildSplinePath(values);
        double lastX = GetX(values.Count - 1);
        double firstX = GetX(0);
        return FormattableString.Invariant($"{spline} L {lastX:0.#} {BottomY:0.#} L {firstX:0.#} {BottomY:0.#} Z");
    }

    private string GetTooltipText(int index)
    {
        if (TooltipFormatter is not null)
        {
            return TooltipFormatter(index);
        }

        string label = (index >= 0 && index < Labels.Count) ? Labels[index] : string.Empty;
        var parts = new List<string>();
        foreach (var s in Series)
        {
            if (index < s.Values.Count)
            {
                double v = s.Values[index];
                string vStr = (v == Math.Truncate(v)) ? v.ToString("0") : v.ToString("0.##");
                parts.Add($"{s.Name}: {vStr}{Unit}");
            }
        }
        return $"{label} · {string.Join(" / ", parts)}";
    }

    private static string FormatNumber(double val)
    {
        if (Math.Abs(val) >= 10000) return (val / 1000).ToString("0k");
        if (val == Math.Truncate(val)) return val.ToString("0");
        return val.ToString("0.#");
    }
}
