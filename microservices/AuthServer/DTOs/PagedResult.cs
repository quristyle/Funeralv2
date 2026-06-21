using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace AuthServer.DTOs;

/// <summary>
/// 프론트엔드 그리드(vxe-table) 및 페이징 호환을 위한 공통 목록 래퍼 객체입니다.
/// </summary>
public class PagedResult<T>
{
    [JsonPropertyName("result")]
    public IEnumerable<T> Result { get; set; } = new List<T>();

    [JsonPropertyName("page")]
    public PageInfo Page { get; set; } = new();

    public PagedResult() { }

    public PagedResult(IEnumerable<T> items, int total)
    {
        Result = items;
        Page = new PageInfo
        {
            Total = total,
        };
    }
}

public class PageInfo
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public static class PagedResultExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int? totalCount = null)
    {
        var list = source as ICollection<T> ?? source.ToList();
        return new PagedResult<T>(list, totalCount ?? list.Count);
    }
}