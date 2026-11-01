using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class WbsBarChart
{
    [Parameter] public IReadOnlyList<WbsBarItem> Items { get; set; } = [];

    /// <summary>고른 막대의 열쇠. 같은 막대를 다시 누르면 화면이 푼다.</summary>
    [Parameter] public string? Active { get; set; }

    [Parameter] public EventCallback<string> OnPick { get; set; }

    /// <summary>겹친 막대의 이름(말풍선). 모듈별 현황은 겹쳐 볼 잣대를 바꾼다.</summary>
    [Parameter] public string DoneLabel { get; set; } = "완료";

    private int Max => Items.Count == 0 ? 1 : Math.Max(1, Items.Max(i => i.Value));

    // 소수점을 점으로 고정한다 — 문화권이 쉼표면 style 의 width 가 통째로 무시된다.
    private static string Pct(int part, int whole) =>
        whole == 0 ? "0" : (part * 100.0 / whole).ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>막대 하나. <see cref="Key"/> 가 상세 목록으로 넘길 값이고 <see cref="Label"/> 은 보이는 글자다.</summary>
public sealed record WbsBarItem(string Key, string Label, int Value, int Done, string? Sub = null);
