using System.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 실행 시점에 칸이 정해지는 표에서 값 하나를 꺼낸다.
/// </summary>
public static class RuntimeCell
{
    /// <summary>
    /// 표에서 온 한 줄. <b>`DataRow` 와 `DataRowView` 를 모두 받는다.</b>
    ///
    /// <para>
    /// 자료로 <see cref="DataTable"/> 을 주면 DevExpress 가 <c>DataView</c> 로
    /// 감싼다. 그래서 셀 템플릿과 선택 알림에 오는 것은 <see cref="DataRow"/>
    /// 가 아니라 <see cref="DataRowView"/> 다. 바로 캐스팅하면 **줄을 그리다
    /// 던지고 회로가 끊긴다** — 그 화면만 깨지는 것이 아니라 그때부터 아무
    /// 단추도 안 눌린다(테이블 관리에서 실제로 그랬다).
    /// </para>
    /// </summary>
    public static DataRow? Row(object? item) => item switch
    {
        DataRowView view => view.Row,
        DataRow row => row,
        _ => null,
    };

    /// <summary>
    /// 칸 값. 없는 칸이면 빈 글자다.
    ///
    /// <para>
    /// 이름을 여럿 받는다 — 같은 값을 PostgreSQL 통로는 소문자로, 옛
    /// SQL Server 통로는 파스칼로 준다. 어느 쪽이 올지는 등록된 질의에 달렸다.
    /// </para>
    /// </summary>
    public static string Of(DataRow? row, params string[] names)
    {
        if (row is null)
        {
            return string.Empty;
        }

        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name))
            {
                continue;
            }

            var text = row[name]?.ToString();

            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return string.Empty;
    }
}
