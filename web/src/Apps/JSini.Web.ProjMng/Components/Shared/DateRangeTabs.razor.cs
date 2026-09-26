using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class DateRangeTabs
{
    /// <summary>기준일. 이 날이 속한 프리셋 구간이 곧 조회 구간이다.</summary>
    [Parameter] public DateTime Anchor { get; set; } = DateTime.Today;

    [Parameter] public EventCallback<DateTime> AnchorChanged { get; set; }

    /// <summary>켜져 있는 기간 프리셋.</summary>
    [Parameter] public DateRangePreset Preset { get; set; } = DateRangePreset.Month;

    [Parameter] public EventCallback<DateRangePreset> PresetChanged { get; set; }

    /// <summary>보여 줄 프리셋 탭. 하나만 남기면 탭 없이 이동 버튼만 쓰는 화면이 된다.</summary>
    [Parameter] public IReadOnlyList<DateRangePreset> Presets { get; set; } =
        [DateRangePreset.Day, DateRangePreset.Week, DateRangePreset.Month];

    /// <summary>프리셋·기준일이 바뀔 때마다 계산된 구간을 올린다. 최초 렌더에는 올리지 않는다.</summary>
    [Parameter] public EventCallback<DateRange> RangeChanged { get; set; }

    /// <summary>기준일 직접 선택기를 보인다. 이동 버튼만으로 충분한 화면은 끈다.</summary>
    [Parameter] public bool ShowDateEdit { get; set; } = true;

    /// <summary>지금 구간. 화면이 <c>@@ref</c> 로 조회 파라미터를 꺼낼 때 쓴다.</summary>
    public DateRange Range => DateRange.Of(Preset, Anchor);

    private string Label => Preset switch
    {
        DateRangePreset.Day => Anchor.ToString("yyyy-MM-dd"),
        DateRangePreset.Week => $"{Range.Start:MM.dd} ~ {Range.End:MM.dd}",
        _ => Anchor.ToString("yyyy-MM"),
    };

    private static string TabText(DateRangePreset preset) => preset switch
    {
        DateRangePreset.Day => "오늘",
        DateRangePreset.Week => "주",
        _ => "월",
    };

    private async Task SetPresetAsync(DateRangePreset preset)
    {
        if (preset == Preset)
        {
            return;
        }

        Preset = preset;

        // "오늘" 탭은 이름 그대로 오늘로 돌아온다 — 지난달을 보다가 눌렀을 때
        // 지난달의 같은 날이 나오면 이름이 거짓말이 된다.
        if (preset == DateRangePreset.Day)
        {
            Anchor = DateTime.Today;
            await AnchorChanged.InvokeAsync(Anchor);
        }

        await PresetChanged.InvokeAsync(preset);
        await RangeChanged.InvokeAsync(Range);
    }

    /// <summary>프리셋 단위로 앞뒤 이동. 월은 달 단위로 옮겨야 31일에서 밀리지 않는다.</summary>
    private async Task ShiftAsync(int direction)
    {
        Anchor = Preset switch
        {
            DateRangePreset.Day => Anchor.AddDays(direction),
            DateRangePreset.Week => Anchor.AddDays(direction * 7),
            _ => Anchor.AddMonths(direction),
        };

        await AnchorChanged.InvokeAsync(Anchor);
        await RangeChanged.InvokeAsync(Range);
    }

    private async Task SetAnchorAsync(DateTime date)
    {
        if (date.Date == Anchor.Date)
        {
            return;
        }

        Anchor = date.Date;
        await AnchorChanged.InvokeAsync(Anchor);
        await RangeChanged.InvokeAsync(Range);
    }
}
