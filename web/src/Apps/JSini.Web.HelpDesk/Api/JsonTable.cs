using System.Data;
using System.Globalization;
using System.Text.Json;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// REST 응답(JSON 배열)을 그리드가 쓸 <see cref="DataTable"/> 로 옮긴 것.
///
/// [왜 사전이 아니라 DataTable 인가]
///
/// DevExpress 그리드는 <c>FieldName</c> 을 리플렉션으로 <b>속성</b>에서 찾는다.
/// <c>Dictionary&lt;string, object?&gt;</c> 를 넘기면 키를 컬럼으로 인식하지 못하고
/// <c>A property with the name '...' is not found</c> 로 죽는다.
/// 프로젝트관리에서 실제로 밟은 함정이고(<c>ProjMngTable</c> 주석 참고),
/// 여기서도 같은 길을 피한다.
///
/// [왜 DTO 를 안 만드나 — 화면이 마흔 개다]
///
/// 헬프데스크 화면 대부분이 "한 엔드포인트를 읽어 표로 보여 준다" 이고, 그
/// 엔드포인트가 마흔 개 넘게 다르다. 화면마다 DTO 를 만들면 클래스가 마흔 개
/// 늘고, 백엔드가 칸을 하나 더할 때마다 <b>화면이 아니라 DTO 를 고치러</b>
/// 가야 한다 — 그런데 그 칸은 대개 그냥 표에 한 줄 더 나오면 되는 것이다.
///
/// 손으로 다듬을 값어치가 있는 화면(요청 목록·상세처럼 매일 보는 것)은 DTO 를
/// 두고, 나머지(보고서·수집 로그처럼 서버가 준 대로 보면 되는 것)는 이 표를 쓴다.
/// <b>둘 중 어느 쪽인지는 화면 주석에 적는다.</b>
///
/// [칸 이름을 한글로 바꾸는 자리]
///
/// JSON 키는 <c>createdAt</c> 같은 영문이다. 그대로 두면 표 머리가 영어가 되므로
/// 화면이 <c>AutoGrid</c> 에 이름표를 넘겨 바꾼다. 넘기지 않은 칸은
/// 키 그대로 나온다 — <b>숨기지 않는다.</b> 숨기면 서버가 새로 준 칸을 아무도
/// 알아채지 못한다.
/// </summary>
public static class JsonTable
{
    /// <summary>행이 하나도 없는 표.</summary>
    public static DataTable Empty { get; } = new();

    /// <summary>
    /// JSON 배열을 표로 옮긴다.
    ///
    /// 컬럼은 <b>모든 행을 훑어</b> 모은다. 첫 행만 보면 뒤쪽 행에만 있는 칸이
    /// 통째로 사라진다 — 서버가 <c>null</c> 인 칸을 빼고 보내는 일이 흔하다.
    /// </summary>
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

    /// <summary>
    /// 칸의 타입을 정한다.
    ///
    /// <b>날짜를 문자열로 두지 않는다.</b> 문자열이면 정렬이 사전순이 되어
    /// <c>2026-01-02</c> 가 <c>2026-1-10</c> 보다 뒤로 간다. ISO 8601 로 읽히는
    /// 값만 날짜로 본다 — 서버가 그 모양으로 준다.
    /// </summary>
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

        // 중첩 객체·배열은 원문 그대로 담는다. 펼쳐서 컬럼으로 만들면 표가
        // 옆으로 한없이 넓어지고, 버리면 화면에서 무엇이 왔는지 알 수 없다.
        JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),

        _ => value.ToString(),
    };

    /// <summary>
    /// ISO 8601 로 보이는가. <c>2026-09-06</c> · <c>2026-09-06T12:00:00Z</c>.
    ///
    /// <c>DateTime.TryParse</c> 만으로 판단하지 않는다 — 그것은 <c>"3"</c> 이나
    /// <c>"1-2"</c> 같은 값도 날짜로 읽어서, 요청 번호 칸이 통째로 날짜가 된다.
    /// </summary>
    private static bool LooksLikeDate(string? text) =>
        text is { Length: >= 10 }
        && text[4] == '-' && text[7] == '-'
        && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
}
