using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>
/// 두 개의 IQueryCollection을 병합하는 헬퍼 클래스
/// </summary>
public static class QueryCollectionMerger
{
    /// <summary>
    /// 두 개의 IQueryCollection을 병합합니다. q2의 키가 q1에 존재하면 덮어씁니다.
    /// </summary>
    /// <param name="q1">첫 번째 쿼리 컬렉션</param>
    /// <param name="q2">두 번째 쿼리 컬렉션 (우선순위 높음)</param>
    /// <returns>병합된 새로운 IQueryCollection</returns>
    public static IQueryCollection Merge(IQueryCollection q1, IQueryCollection q2)
    {
        var dict = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        // 먼저 q1 넣고
        foreach (var kv in q1)
        {
            dict[kv.Key] = kv.Value;
        }

        // q2 값이 있으면 덮어씀
        foreach (var kv in q2)
        {
            dict[kv.Key] = kv.Value;
        }

        return new QueryCollection(dict);
    }
}
