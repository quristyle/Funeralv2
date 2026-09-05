using System.Data;
using System.Globalization;
using System.Text.Json;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로시저 결과를 그리드가 쓸 <see cref="DataTable"/> 로 옮긴 것.
///
/// [왜 사전이 아니라 DataTable 인가]
///
/// 처음에는 행을 <c>Dictionary&lt;string, object?&gt;</c> 로 두고 그리드에
/// 그대로 넘겼다. <b>동작하지 않는다</b> — DevExpress 그리드는 <c>FieldName</c> 을
/// 리플렉션으로 <b>속성</b>에서 찾는다. 사전의 키는 속성이 아니므로
/// "A property with the name 'cm_cd' is not found" 로 죽는다.
///
/// DataTable 은 컬럼이 실행 시점에 정해지는 자료를 다루라고 있는 것이고,
/// DevExpress 가 정식으로 지원한다. 덤으로 세 가지가 공짜로 따라온다.
///   · 타입별 정렬·필터 (문자열로 뭉개면 숫자 10 이 2 보다 앞에 온다)
///   · 편집기 — 날짜는 달력, 불리언은 체크박스가 알아서 뜬다
///   · 변경 추적 — <see cref="DataRowState"/> 가 Added·Modified 를 알려 준다.
///     Vue 가 <c>quri_ischange</c> 를 손으로 붙이던 일이 통째로 사라진다.
/// </summary>
public sealed class ProjMngTable
{
    private ProjMngTable(DataTable table, IReadOnlyList<string> columnOrder)
    {
        Table = table;
        ColumnOrder = columnOrder;
    }

    /// <summary>그리드에 넘길 표.</summary>
    public DataTable Table { get; }

    /// <summary>
    /// 컬럼 이름을 프로시저가 준 순서대로. <b>그 순서가 곧 화면 순서다.</b>
    /// <see cref="DataTable.Columns"/> 도 순서를 지키지만, 화면이 숨김 목록을
    /// 걸러 다시 세우기 편하도록 따로 들고 있는다.
    /// </summary>
    public IReadOnlyList<string> ColumnOrder { get; }

    /// <summary>행이 하나도 없는 표.</summary>
    public static ProjMngTable Empty { get; } = new(new DataTable(), []);

    /// <summary>
    /// 이미 만들어 둔 <see cref="DataTable"/> 을 그대로 감싼다.
    ///
    /// <b>화면이 거른 결과를 넘길 때 쓴다.</b> 서버가 이름 조건을 받지 않는
    /// 질의가 있어서(예: <c>tablelist</c>) 그런 화면은 전량을 받아 스스로
    /// 거른다. 그때 <see cref="From"/> 을 다시 태우려면 결과를 와이어 모양으로
    /// 되돌려야 하는데, 그건 같은 자료를 두 번 옮기는 일이다.
    /// </summary>
    public static ProjMngTable Wrap(DataTable table, IReadOnlyList<string> columnOrder) =>
        new(table, columnOrder);

    /// <summary>
    /// 저장할 행. <b>추가되거나 고쳐진 행만</b> 골라 보낸다.
    ///
    /// 지워진 행(<see cref="DataRowState.Deleted"/>)은 넣지 않는다 —
    /// 프로젝트관리의 삭제는 별도 프로시저 호출(<c>delete</c>)이고,
    /// 저장 경로로 지워진 행을 보내면 프로시저가 그것을 새 행으로 취급한다.
    /// </summary>
    public List<Dictionary<string, object?>> ChangedRows()
    {
        var changed = new List<Dictionary<string, object?>>();

        foreach (DataRow row in Table.Rows)
        {
            if (row.RowState is not (DataRowState.Added or DataRowState.Modified))
            {
                continue;
            }

            var payload = new Dictionary<string, object?>();
            foreach (DataColumn column in Table.Columns)
            {
                var value = row[column];
                payload[column.ColumnName] = value == DBNull.Value ? null : value;
            }

            changed.Add(payload);
        }

        return changed;
    }

    /// <summary>고치거나 더한 행이 있는가.</summary>
    public bool HasChanges =>
        Table.Rows.Cast<DataRow>()
            .Any(r => r.RowState is DataRowState.Added or DataRowState.Modified);

    /// <summary>
    /// 저장이 끝났음을 표에 알린다. 변경 표시가 지워진다.
    ///
    /// 안 부르면 다음 저장 때 같은 행이 또 나간다. 저장 뒤 다시 조회하는 화면은
    /// 표를 새로 만들므로 굳이 부르지 않아도 되지만, 다시 조회하지 않는 화면
    /// (<c>ReloadAfterWrite = false</c>)에는 반드시 필요하다.
    /// </summary>
    public void AcceptChanges() => Table.AcceptChanges();

    /// <summary>
    /// 프로시저 결과를 표로 옮긴다.
    /// </summary>
    public static ProjMngTable From(ProjMngResult result)
    {
        var cols = result.Cols;
        if (cols is null || cols.Count == 0)
        {
            return Empty;
        }

        var table = new DataTable();
        var order = new List<string>(cols.Count);

        foreach (var (name, typeName) in cols)
        {
            table.Columns.Add(name, ClrTypeOf(typeName));
            order.Add(name);
        }

        foreach (var source in result.Rows ?? [])
        {
            var row = table.NewRow();
            foreach (DataColumn column in table.Columns)
            {
                row[column] = source.TryGetValue(column.ColumnName, out var raw)
                    ? Convert(raw, column.DataType)
                    : DBNull.Value;
            }
            table.Rows.Add(row);
        }

        // 방금 서버에서 읽은 것이므로 "변경 없음" 상태로 시작해야 한다.
        // 안 하면 모든 행이 Added 로 남아 저장할 때 전량이 다시 나간다.
        table.AcceptChanges();

        return new ProjMngTable(table, order);
    }

    /// <summary>
    /// 프로시저가 준 .NET 타입 이름을 실제 타입으로 옮긴다.
    ///
    /// 모르는 타입은 문자열로 둔다. 화면이 뜨지 않는 것보다 낫다 —
    /// 프로시저가 새 타입을 돌려주기 시작해도 그 컬럼만 문자열로 보일 뿐이다.
    /// </summary>
    private static Type ClrTypeOf(string typeName) => typeName switch
    {
        "System.Boolean" => typeof(bool),
        "System.Int16" => typeof(short),
        "System.Int32" => typeof(int),
        "System.Int64" => typeof(long),
        "System.Decimal" => typeof(decimal),
        "System.Double" => typeof(double),
        "System.Single" => typeof(float),
        "System.DateTime" => typeof(DateTime),
        "System.Guid" => typeof(Guid),
        _ => typeof(string),
    };

    /// <summary>
    /// JSON 에서 온 값을 컬럼 타입으로 맞춘다.
    ///
    /// 행은 <c>Dictionary&lt;string, object?&gt;</c> 로 역직렬화되므로 값이 전부
    /// <see cref="JsonElement"/> 다. 그대로 DataRow 에 넣으면 타입이 안 맞아
    /// <c>ArgumentException</c> 이 난다.
    ///
    /// 바꾸지 못하는 값은 <see cref="DBNull"/> 로 둔다. 한 칸이 이상하다고
    /// 화면 전체가 죽으면 사용자는 무엇이 문제인지 알 수 없다.
    /// </summary>
    private static object Convert(object? raw, Type target)
    {
        if (raw is null)
        {
            return DBNull.Value;
        }

        if (raw is JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return DBNull.Value;
            }

            // 숫자·불리언은 JsonElement 가 직접 꺼내 준다. 문자열로 한 번
            // 돌리면 로캘에 따라 소수점이 달라지는 문제가 생긴다.
            if (target == typeof(bool) && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return element.GetBoolean();
            }

            var text = element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.ToString();

            return FromText(text, target);
        }

        try
        {
            return System.Convert.ChangeType(raw, target, CultureInfo.InvariantCulture) ?? DBNull.Value;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return DBNull.Value;
        }
    }

    private static object FromText(string? text, Type target)
    {
        if (string.IsNullOrEmpty(text))
        {
            return DBNull.Value;
        }

        if (target == typeof(string))
        {
            return text;
        }

        // 프로시저가 불리언을 "true"/"True"/"1" 로 섞어 돌려준다.
        if (target == typeof(bool))
        {
            return text is "1" || bool.TryParse(text, out var flag) && flag;
        }

        try
        {
            return target == typeof(DateTime)
                ? DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None)
                : System.Convert.ChangeType(text, target, CultureInfo.InvariantCulture) ?? DBNull.Value;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return DBNull.Value;
        }
    }
}
