using System.Collections.Generic;
using System.Linq;

namespace AuthServer.DTOs;

/// <summary>
/// 프론트엔드 그리드(vxe-table) 및 페이징 호환을 위한 공통 목록 래퍼 객체입니다.
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int Total { get; set; }

    public PagedResult() { }

    public PagedResult(IEnumerable<T> items, int total)
    {
        Items = items;
        Total = total;
    }
}

public static class PagedResultExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int? totalCount = null)
    {
        var list = source as ICollection<T> ?? source.ToList();
        return new PagedResult<T>(list, totalCount ?? list.Count);
    }
}