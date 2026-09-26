using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using System.Data;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class RuntimeColumns
{
    /// <summary>
    /// 이벤트 뒤에 다시 그리지 않는다(머리말). 기본 구현이 부르는
    /// <c>StateHasChanged</c> 가 여기서는 예외가 된다.
    /// </summary>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);

    /// <summary>칸 이름을 읽을 표.</summary>
    [Parameter, EditorRequired] public DataTable Table { get; set; } = default!;

    /// <summary>
    /// 감출 칸. 본문처럼 <b>길어서 표를 망가뜨리는 칸</b>을 뺄 때 쓴다.
    /// </summary>
    [Parameter] public IReadOnlyCollection<string>? Hidden { get; set; }

    /// <summary>
    /// 칸 순서. 서버가 준 순서를 그대로 쓰려면 넘긴다. 비우면 표의 순서다.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? Order { get; set; }

    /// <summary>
    /// 칸 안에서 바로 고칠 칸. <b>편집 창을 열지 않는다.</b>
    /// 설명을 줄줄이 손보는 화면(테이블 관리)을 위한 것이다.
    /// </summary>
    [Parameter] public IReadOnlyCollection<string>? Editable { get; set; }

    private IEnumerable<string> Names
    {
        get
        {
            var names = Order is { Count: > 0 }
                ? Order.Where(Table.Columns.Contains)
                : Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName);

            return Hidden is null
                ? names
                : names.Where(n => !Hidden.Contains(n, StringComparer.OrdinalIgnoreCase));
        }
    }

    private bool IsEditable(string name) =>
        Editable is not null && Editable.Contains(name, StringComparer.OrdinalIgnoreCase);

    private GridTextAlignment Align(string name) =>
        Table.Columns[name]?.DataType == typeof(string)
            ? GridTextAlignment.Left
            : GridTextAlignment.Center;
}
