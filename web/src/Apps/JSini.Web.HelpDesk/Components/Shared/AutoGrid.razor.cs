using DevExpress.Blazor;
using JSini.Web.Components.Data;
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
    /// 쪽을 나누지 않고 <b>전부 한 묶음으로</b> 보여 준다(페이저를 감추고
    /// 가상 스크롤). <b>기본은 꺼짐</b>이다.
    ///
    /// <para>
    /// 켜는 쪽은 결과를 <b>훑어보는</b> 표다 — 프로시저 결과처럼 돌린 것을
    /// 한 번에 보고 싶은 자리. 쪽으로 자르면 「몇 쪽에 있었나」를 사람이
    /// 기억해야 한다. 표가 높이를 받는 자리에 있어야 한다(가상 스크롤은
    /// 바깥이 정해 준 높이 안에서 굴린다).
    /// </para>
    /// </summary>
    [Parameter] public bool AllRows { get; set; }

    /// <summary>
    /// 표 전체를 훑는 검색칸을 켠다. <b>기본은 꺼짐</b>이다.
    ///
    /// <para>
    /// 칸 이름이 미리 정해진 표는 켤 이유가 없다 — 거기서는 칸별 거르개가
    /// 더 정확하다. 켜는 쪽은 <b>칸이 무엇인지 미리 알 수 없는</b> 표다
    /// (프로시저 결과처럼 돌릴 때마다 칸이 바뀌는 것).
    /// </para>
    ///
    /// <para>
    /// 켜 두면 화면이 든 건수와 보이는 줄 수가 갈라진다. 건수를 적는 자리는
    /// <b>총 건수</b>라고 적어야 한다.
    /// </para>
    /// </summary>
    [Parameter] public bool SearchBox { get; set; }

    /// <summary>
    /// 칸 하나의 최소 너비(px). 다 더한 것이 판보다 넓으면 표가 가로로 구른다.
    /// 칸 이름이 길거나 짧은 표만 손본다 — 왜 두는지는 화면 머리말에 있다.
    /// </summary>
    [Parameter] public int MinColumnWidth { get; set; } = 120;

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

    /// <summary>휴대폰(≤767px)인가. <c>DxLayoutBreakpoint</c> 가 채운다.</summary>
    private bool _isPhone;

    private void OnPhoneChanged(bool phone)
    {
        if (_isPhone == phone)
        {
            return;
        }

        _isPhone = phone;
        StateHasChanged();
    }
}
