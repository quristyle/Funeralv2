using System.Data;
using System.Globalization;
using System.Text.Json;

namespace JSini.Web.HelpDesk.Api;

/// <summary>REST 응답(JSON 배열)을 그리드가 쓸 DataTable 로 옮긴다. 사전이 아니라 DataTable 인 까닭: DevExpress 그리드는 FieldName 을 속성에서 찾아 사전 키를 컬럼으로 못 읽고 죽는다.
/// DTO 를 화면마다 만들지 않는다 — 서버가 칸을 하나 더할 때 화면이 아니라 DTO 를 고치게 된다. 이름표는 AutoGrid 로 바꾸고, 안 넘긴 칸은 키 그대로 보여 숨기지 않는다.</summary>
public static class JsonTable
{
    /// <summary>행이 하나도 없는 표.</summary>
    public static DataTable Empty { get; } = new();

    /// <summary>JSON 배열을 표로 옮긴다. 컬럼은 모든 행을 훑어 모은다 — 첫 행만 보면 뒤쪽 행에만 있는 칸이 사라진다(null 칸을 빼고 보내는 일이 흔하다).</summary>
    public static DataTable From(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Array } array)
        {
            return new DataTable();
        }

        var table = new DataTable();

        // ① 컬럼 모으기 — 순서는 처음 나온 순서를 지킨다.
        foreach (var row in array.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) continue;

            foreach (var prop in row.EnumerateObject())
            {
                if (!table.Columns.Contains(prop.Name))
                {
                    table.Columns.Add(prop.Name, TypeOf(prop.Value));
                }
            }
        }

        // ② 값 채우기
        foreach (var row in array.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) continue;

            var dataRow = table.NewRow();

            foreach (var prop in row.EnumerateObject())
            {
                dataRow[prop.Name] = Value(prop.Value, table.Columns[prop.Name]!.DataType);
            }

            table.Rows.Add(dataRow);
        }

        table.AcceptChanges();
        return table;
    }

    /// <summary>칸의 타입을 정한다. 날짜를 문자열로 두지 않는다 — 문자열 정렬은 사전순이라 2026-01-02 가 2026-1-10 보다 뒤로 간다. ISO 8601 로 읽히는 값만 날짜로 본다.</summary>
    private static Type TypeOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => typeof(bool),
        JsonValueKind.Number => value.TryGetInt64(out _) ? typeof(long) : typeof(double),
        JsonValueKind.String when LooksLikeDate(value.GetString()) => typeof(DateTime),
        _ => typeof(string),
    };

    private static object Value(JsonElement value, Type target) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => DBNull.Value,

        JsonValueKind.True or JsonValueKind.False when target == typeof(bool) => value.GetBoolean(),
        JsonValueKind.Number when target == typeof(long) => value.TryGetInt64(out var l) ? l : DBNull.Value,
        JsonValueKind.Number when target == typeof(double) => value.GetDouble(),

        JsonValueKind.String when target == typeof(DateTime) =>
            DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var d) ? d : DBNull.Value,

        // 중첩 객체·배열은 원문 그대로 담는다 — 펼치면 표가 옆으로 한없이 넓어진다.
        JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),

        _ => value.ToString(),
    };

    /// <summary>ISO 8601 로 보이는가(2026-09-06 · 2026-09-06T12:00:00Z). DateTime.TryParse 만으로 판단하지 않는다 — 3 이나 1-2 도 날짜로 읽혀 요청 번호 칸이 날짜가 된다.</summary>
    private static bool LooksLikeDate(string? text) =>
        text is { Length: >= 10 }
        && text[4] == '-' && text[7] == '-'
        && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
}
