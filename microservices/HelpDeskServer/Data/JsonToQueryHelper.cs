using System.Text.Json;
using Microsoft.Extensions.Primitives;

/// <summary>
/// JSON 문자열을 IQueryCollection으로 변환하는 헬퍼 클래스
/// </summary>
public static class JsonToQueryHelper
{
    /// <summary>
    /// 필터, 정렬, 페이징 정보가 포함된 JSON 문자열을 IQueryCollection으로 변환합니다.
    /// </summary>
    /// <param name="json">변환할 JSON 문자열</param>
    /// <returns>변환된 IQueryCollection</returns>
    public static IQueryCollection ConvertJsonToQuery(string json)
    {
        var dict = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name.Equals("filters", StringComparison.OrdinalIgnoreCase))
            {
                // [{ "field":"id", "op":"eq", "value":"10"}]
                foreach (var filter in prop.Value.EnumerateArray())
                {
                    var field = filter.GetProperty("field").GetString();
                    var op = filter.TryGetProperty("op", out var o) ? o.GetString() : "eq";
                    var value = filter.GetProperty("value").ToString();
                    var key = $"{field}_{op}";
                    dict[key] = value;
                }
            }
            else if (prop.Name.Equals("sorts", StringComparison.OrdinalIgnoreCase))
            {
                // [{ "field":"id", "dir":"asc"}]
                var orderParts = new List<string>();
                foreach (var sort in prop.Value.EnumerateArray())
                {
                    var field = sort.GetProperty("field").GetString();
                    var dir = sort.TryGetProperty("dir", out var d) ? d.GetString() : "asc";
                    orderParts.Add($"{field} {dir}");
                }
                dict["orderBy"] = string.Join(",", orderParts);
            }
            else
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    // JSON 배열 값을 여러 개의 StringValues로 변환합니다.
                    // 예: "_or_status_eq": ["1", "2"] -> _or_status_eq=1, _or_status_eq=2
                    var arrayValues = prop.Value.EnumerateArray()
                                                .Select(e => e.ToString())
                                                .ToArray();
                    dict[prop.Name] = new StringValues(arrayValues);
                }
                else
                {
                    dict[prop.Name] = prop.Value.ToString();
                }
            }
        }

        return new QueryCollection(dict);
    }
}
