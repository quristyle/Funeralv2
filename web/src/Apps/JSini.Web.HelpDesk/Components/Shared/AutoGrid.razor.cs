using Microsoft.AspNetCore.Components;
using System.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class AutoGrid
{
    /// <summary>그릴 표. <c>JsonTable.From</c> 이 만든 것.</summary>
    [Parameter, EditorRequired] public DataTable Data { get; set; } = JsonTable.Empty;

    [Parameter] public bool Loading { get; set; }

    [Parameter] public int PageSize { get; set; } = 20;

    /// <summary>쪽을 나누지 않고 전부 한 묶음으로 보여 준다(기본 꺼짐). 훑어보는 표에 켠다 — 쪽으로 자르면 몇 쪽에 있었나를 사람이 기억해야 한다.
    /// 휴대폰에서는 켜도 쪽나누기로 돌아간다 — 카드로 펴면 줄 높이가 고르지 않아 가상 스크롤의 어림이 빗나간다.</summary>
    [Parameter] public bool AllRows { get; set; }

    /// <summary>표 전체를 훑는 검색칸을 켠다(기본 꺼짐). 칸 이름이 미리 정해진 표는 칸별 거르개가 더 정확하다.
    /// 켜면 머리줄 건수가 보이는 줄 수와 갈라지므로 머리줄에는 늘 총 건수를 적는다.</summary>
    [Parameter] public bool SearchBox { get; set; }

    /// <summary>칸 하나의 기본 너비(px). 기본값 120.</summary>
    [Parameter] public int? ColumnWidth { get; set; } = 120;

    /// <summary>칸 하나의 최소 너비(px). 칸을 줄일 수 있는 한계. 기본값 50.</summary>
    [Parameter] public int MinColumnWidth { get; set; } = 50;

    /// <summary>칸 이름표(키 → 표시 글자). 여기 적은 순서가 곧 표의 앞쪽 순서다.</summary>
    [Parameter] public IReadOnlyDictionary<string, string>? Captions { get; set; }

    /// <summary>감출 칸(쉼표로 나눔). 내부 키(id · companyId)처럼 볼 일이 없는 것만 넣는다 — 자료가 담긴 칸은 감추지 않는다.</summary>
    [Parameter] public string? HiddenCols { get; set; }

    private HashSet<string> Hidden => string.IsNullOrWhiteSpace(HiddenCols)
        ? []
        : [.. HiddenCols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>이름표에 적힌 칸이 먼저, 나머지는 서버가 준 순서대로.</summary>
    private IEnumerable<string> Order
    {
        get
        {
            var all = Data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            if (Captions is null) return all;

            var named = Captions.Keys.Where(all.Contains).ToList();
            return named.Concat(all.Except(named, StringComparer.Ordinal));
        }
    }

    private string Caption(string name) =>
        Captions is not null && Captions.TryGetValue(name, out var text) ? text : name;

    /// <summary>시각 칸의 꼴. 날짜만 담긴 칸은 날짜만, 시각이 섞인 칸은 시:분:초까지. 시각 칸이 아니면 null 을 돌려줘 DevExpress 기본값으로 그린다.</summary>
    private string? Format(string name)
    {
        if (!Data.Columns.Contains(name) || Data.Columns[name]!.DataType != typeof(DateTime))
        {
            return null;
        }

        var hasTime = Data.Rows.Cast<DataRow>()
            .Select(row => row[name])
            .OfType<DateTime>()
            .Any(value => value.TimeOfDay != TimeSpan.Zero);

        return hasTime ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd";
    }
}
