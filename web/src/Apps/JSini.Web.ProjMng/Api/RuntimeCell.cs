using System.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 실행 시점에 칸이 정해지는 표에서 값 하나를 꺼낸다.
/// </summary>
public static class RuntimeCell
{
    /// <summary>표에서 온 한 줄. DataRow 와 DataRowView 를 모두 받는다 — 표를 주면 DevExpress 가 DataView 로 감싸므로, 바로 캐스팅하면 줄을 그리다 회로가 끊긴다.</summary>
    public static DataRow? Row(object? item) => item switch
    {
        DataRowView view => view.Row,
        DataRow row => row,
        _ => null,
    };

    /// <summary>칸 값. 없는 칸이면 빈 글자다. 이름을 여럿 받는다 — PostgreSQL 통로는 소문자, 옛 SQL Server 통로는 파스칼로 주므로 어느 쪽이 올지 모른다.</summary>
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
