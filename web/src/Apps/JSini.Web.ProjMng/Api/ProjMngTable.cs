using System.Data;
using System.Globalization;
using System.Text.Json;

namespace JSini.Web.ProjMng.Api;

/// <summary>프로시저 결과를 그리드가 쓸 DataTable 로 옮긴 것. 사전 행은 그리드가 FieldName 을 속성에서 찾다 죽으므로 DataTable 을 쓴다 — 타입별 정렬과 변경 추적도 따라온다.</summary>
public sealed class ProjMngTable
{
    private ProjMngTable(DataTable table, IReadOnlyList<string> columnOrder)
    {
        Table = table;
        ColumnOrder = columnOrder;
    }

    /// <summary>그리드에 넘길 표.</summary>
    public DataTable Table { get; }

    /// <summary>컬럼 이름을 프로시저가 준 순서대로. 화면 순서가 곧 이 순서다.</summary>
    public IReadOnlyList<string> ColumnOrder { get; }

    /// <summary>행이 하나도 없는 표.</summary>
    public static ProjMngTable Empty { get; } = new(new DataTable(), []);

    /// <summary>이미 거른 DataTable 을 그대로 감싼다. 전량을 받아 스스로 거르는 화면이 쓴다 — From 을 다시 태우면 같은 자료를 두 번 옮기게 된다.</summary>
    public static ProjMngTable Wrap(DataTable table, IReadOnlyList<string> columnOrder) =>
        new(table, columnOrder);

    /// <summary>저장할 행. 추가되거나 고쳐진 행만 보낸다. 지운 행은 넣지 않는다 — 저장 경로로 보내면 프로시저가 새 행으로 취급한다.</summary>
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

    /// <summary>저장이 끝났음을 표에 알린다. 안 부르면 다음 저장 때 같은 행이 또 나간다.</summary>
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

        // 방금 읽은 것이므로 변경 없음 상태로 시작한다 — 안 하면 모든 행이 Added 로 남아 전량이 다시 나간다.
        table.AcceptChanges();

        return new ProjMngTable(table, order);
    }

    /// <summary>프로시저가 준 .NET 타입 이름을 실제 타입으로 옮긴다. 모르는 타입은 문자열로 둔다.</summary>
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

    /// <summary>JSON 에서 온 값을 컬럼 타입으로 맞춘다. 바꾸지 못하는 값은 DBNull 로 둔다 — 한 칸 때문에 화면이 죽지 않게.</summary>
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

            // 숫자·불리언은 JsonElement 가 직접 꺼낸다 — 문자열로 거치면 로캘에 따라 소수점이 달라진다.
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
