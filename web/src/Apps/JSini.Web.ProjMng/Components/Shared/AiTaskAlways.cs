namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 지시마다 무조건 뒤에 붙는 문구. 「빠른 지시」와 「이어서 지시」가 같은 것을 쓴다 — 복사해 두면 한쪽만 고쳐져 어긋난다.
/// 보여 줄 때는 Strip 으로 걷는다(DB 의 글은 그대로 둔다).
/// </summary>
internal static class AiTaskAlways
{
    /// <summary>늘 따라붙는 문구 그 자체.</summary>
    public const string Text =
        "작업에 생성되는 파일이나 변경된 내용이 실제 작성되었는지 다시 확인하고, "
        + "모든 작업이 끝나면 반드시 main 브랜치에 commit과 push를 수행해라.\n\n"
        + "작업하던 가지에 커밋만 해 두고 끝내지 마라. 고친 것이 origin/main 에 "
        + "들어갔는지 git log origin/main 으로 직접 확인하고, 올리지 못했으면 "
        + "무엇이 막았는지 결과에 적어라.\n\n"
        + "DB 작업이 필요하면 DB 연결정보를 소스에서 확인하여 사용하여 직접 처리하고, "
        + "반영한 결과까지 조회해서 확인해라.\n\n"
        + "셸 명령은 입력을 기다리는 꼴로 적지 마라. `$(find ...)` 처럼 치환으로 만든 "
        + "파일 목록을 grep·cat 에 바로 넘기면, 찾은 것이 없을 때 인자가 통째로 사라져 "
        + "그 명령이 표준입력을 기다리며 안 끝난다. 파일을 뒤질 때는 `grep -r 무늬 경로` 나 "
        + "`find ... -exec grep ... {} +` 를 쓰고, 끝내기 전에 남은 배경 작업이 있는지 "
        + "보고 있으면 멈춰라.";

    /// <summary>문구를 이루는 문단들. Strip 이 뒤에서부터 하나씩 떼어 낸다 — 통째로 견주면 옛 판으로 쓰인 지시는 하나도 안 걷힌다.</summary>
    public static readonly IReadOnlyList<string> Blocks =
        Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

    /// <summary>보여 줄 글 끝에서 문구 문단을 걷는다. 문구만 있던 글은 그대로 돌려준다(「적은 내용이 없다」로 거짓 표시되지 않게).</summary>
    /// <param name="written">보여 줄 글.</param>
    /// <returns>문구를 걷어 낸 글. 걷을 것이 없으면 받은 것을 그대로.</returns>
    public static string? Strip(string? written)
    {
        if (string.IsNullOrWhiteSpace(written))
        {
            return written;
        }

        var left = written.Replace("\r\n", "\n").TrimEnd();

        while (true)
        {
            var block = Blocks.FirstOrDefault(b =>
                left.EndsWith(b, StringComparison.Ordinal)
                && (left.Length == b.Length || left[left.Length - b.Length - 1] == '\n'));

            if (block is null)
            {
                break;
            }

            left = left[..^block.Length].TrimEnd();
        }

        return left.Length == 0 ? written : left;
    }

    /// <summary>사람이 적은 말 뒤에 Text 를 붙인다. 빈 글에는 붙이지 않는다 — 덧붙일 말 없이 보내면 지시가 생겨 버린다. 이미 들어 있으면 또 붙이지 않는다.</summary>
    /// <param name="written">사람이 적은 말.</param>
    /// <returns>문구가 붙은 지시. 적은 말이 비어 있으면 받은 것을 그대로.</returns>
    public static string? Append(string? written)
    {
        if (string.IsNullOrWhiteSpace(written))
        {
            return written;
        }

        return written.Contains(Text, StringComparison.Ordinal)
            ? written
            : written.TrimEnd() + "\n\n" + Text;
    }
}
