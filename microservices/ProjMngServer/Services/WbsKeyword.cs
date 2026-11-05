namespace ProjMngServer.Services;

/// <summary>
/// 일정 낱말 찾기. 묻는 말과 적힌 말은 끝이 다르다 — 「똥싼날」로 묻는데 일정표에는 「똥싸기」로 적혀 있다.
/// 그대로 든 줄이 하나도 없을 때만 <see cref="Relax"/> 로 낱말마다 앞 두 글자(끝 받침 ㄴ·ㄹ·ㅁ·ㅆ 을 뗀 것까지)로
/// 넓혀 다시 본다. 처음부터 넓히면 「개발일정」이 「개발」 든 줄을 전부 끌어온다.
/// </summary>
public static class WbsKeyword
{
    /// <summary>낱말이 칸 하나에라도 그대로 들었나(대소문자 무시 — SQL 의 ILIKE 와 같다).</summary>
    public static bool Contains(string keyword, params string?[] fields) =>
        fields.Any(f => f?.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>띄어 쓴 낱말마다 넓힌 후보들. 한글 두 글자로 시작하지 않는 낱말(영문·숫자·한 글자)은 그대로 둔다.</summary>
    public static IReadOnlyList<string[]> Relax(string keyword) =>
        [.. keyword.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Variants)];

    /// <summary>
    /// 넓힌 낱말이 <b>모두</b> 걸리면(낱말마다 후보 하나면 된다) 걸린 후보들을 이어 돌려주고, 하나라도 빠지면 null.
    /// 돌려준 글은 비서가 「무엇으로 넓혀 찾았나」를 밝히는 데 쓴다.
    /// </summary>
    public static string? RelaxedMatch(IReadOnlyList<string[]> tokens, params string?[] fields)
    {
        if (tokens.Count == 0) return null;

        var hits = new List<string>(tokens.Count);
        foreach (var variants in tokens)
        {
            var hit = variants.FirstOrDefault(v => Contains(v, fields));
            if (hit is null) return null;
            hits.Add(hit);
        }

        return string.Join(' ', hits);
    }

    private static string[] Variants(string token)
    {
        if (token.Length < 2 || !IsSyllable(token[0]) || !IsSyllable(token[1])) return [token];

        var head = token[..2];
        var bare = DropTail(head[1]);
        return bare == head[1] ? [head] : [head, $"{head[0]}{bare}"];
    }

    private static bool IsSyllable(char c) => c is >= '가' and <= '힣';

    /// <summary>끝 받침이 꾸밈·시제 꼴(ㄴ·ㄹ·ㅁ·ㅆ)이면 뗀다 — 싼→싸, 갈→가, 했→하. 「장」의 ㅇ 같은 것은 낱말 몸이라 둔다.</summary>
    private static char DropTail(char c)
    {
        var jong = (c - 0xAC00) % 28;
        return jong is 4 or 8 or 16 or 20 ? (char)(c - jong) : c;
    }
}
