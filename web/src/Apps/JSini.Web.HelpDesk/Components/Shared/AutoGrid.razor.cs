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
    ///
    /// <para>
    /// <b>휴대폰에서는 켜도 쪽나누기로 돌아간다.</b> 카드로 펴면 줄 높이가
    /// 고르지 않아 가상 스크롤의 어림이 열 배 넘게 빗나가서다 —
    /// 되돌리는 일은 <c>CommGrd</c> 가 한다(그 부품의 <c>ApplyScrollMode</c>).
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
    /// 켜 두면 <b>머리줄의 건수와 보이는 줄 수가 갈라진다.</b> 훑는 검색은
    /// 칸별 거르개와 달라서 <c>CommGrd</c> 가 「총 65건 중 12건」을 적어 주지
    /// 못한다 — 머리줄에 서는 것은 늘 <b>총 건수</b>다.
    /// </para>
    /// </summary>
    [Parameter] public bool SearchBox { get; set; }

    /// <summary>
    /// 칸 하나의 기본 너비(px). 기본값은 120이다.
    /// </summary>
    [Parameter] public int? ColumnWidth { get; set; } = 120;

    /// <summary>
    /// 칸 하나의 최소 너비(px). 칸을 줄일 수 있는 한계치다. 기본값은 50이다.
    /// </summary>
    [Parameter] public int MinColumnWidth { get; set; } = 50;

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
