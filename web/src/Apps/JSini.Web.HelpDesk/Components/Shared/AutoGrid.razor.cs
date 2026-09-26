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

    /// <summary>
    /// 칸 이름표. <c>["createdAt"] = "접수일"</c>.
    /// 여기 적은 순서가 곧 표의 앞쪽 순서다.
    /// </summary>
    [Parameter] public IReadOnlyDictionary<string, string>? Captions { get; set; }

    /// <summary>
    /// 감출 칸. 쉼표로 나눈다.
    ///
    /// 내부 키(<c>id</c> · <c>companyId</c>)처럼 사람이 볼 일이 없는 것만 넣는다.
    /// 자료가 담긴 칸은 감추지 않는다 — 위 주석 참고.
    /// </summary>
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
}
