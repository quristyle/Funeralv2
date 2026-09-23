namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 지시마다 <b>무조건</b> 뒤에 붙는 문구. 「빠른 지시」로 새로 보내는 건과
/// 「이어서 지시」가 같은 것을 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// <b>사람이 매번 적지 않아도 되는 것만 둔다.</b> 여기 적힌 둘은 지시마다
/// 달라지지 않는데 빠뜨리면 그 회차가 통째로 헛도는 것들이다 —
/// 고쳤다고 말만 하고 파일은 안 건드린 채 끝나거나, 다 고쳐 놓고
/// 워크트리에만 남겨 둬 다음 회차가 같은 일을 다시 한다.
/// </para>
/// <para>
/// [왜 한곳에 모았나]
/// </para>
/// <para>
/// 한동안 이 문구는 <c>AiTaskActions</c> 안의 private const 였고,
/// <b>「이어서 지시」에만 붙었다.</b> 그래서 같은 사람이 같은 자리에 던진
/// 두 건이 서로 다른 지시를 받았다 — 처음 보낸 건은 커밋·push 를 말해 주지
/// 않아 고친 것이 워크트리에만 남고, 이어서 지시한 건만 올라갔다.
/// 문구를 복사해 두면 한쪽만 고쳐져 그 어긋남이 다시 생기므로 여기 하나만 둔다.
/// </para>
/// <para>
/// [커밋·push 줄이 왜 있나]
/// </para>
/// <para>
/// 실행기는 AI 가 커밋을 안 했어도 게이트에서 한 번 더 커밋한다
/// (<c>PushGate</c>). 그래도 <b>지시에 박아 두는 편이 낫다</b> — 게이트가
/// 집어 가는 것은 「바뀐 파일」뿐이라, AI 가 브랜치를 따로 파거나 워크트리를
/// 옮겨 앉으면 그 자리의 변경은 게이트 눈에 안 보인다. 지시가 먼저 말해 주면
/// 그런 갈림이 애초에 안 생긴다.
/// </para>
/// <para>
/// <b>올리기를 끈 건에도 붙는다.</b> 실제로 <c>main</c> 까지 가는지는 화면의
/// 올리기 칸(<c>ai_task.auto_push</c>)과 게이트가 정하지, 지시 문구가 정하지
/// 않는다 — 끈 건은 커밋만 남고 거기서 멈춘다.
/// </para>
/// <para>
/// [DB 줄이 왜 있나]
/// </para>
/// <para>
/// 작업 환경에는 운영 서버 SSH 가 없어서 스키마를 바꿔야 하는 지시가 오면
/// 「SQL 파일은 만들어 뒀으니 사람이 돌려 달라」로 끝나기 쉽다. 그런데 접속
/// 정보는 소스에 있고 실제로 닿는다 — 그것을 모르는 채로 넘기면 코드만
/// 올라가고 표는 안 바뀐 상태로 배포가 나간다. 그래서 「찾아서 직접 하라」를
/// 지시에 박아 둔다.
/// </para>
/// </remarks>
internal static class AiTaskAlways
{
    /// <summary>늘 따라붙는 문구 그 자체.</summary>
    public const string Text =
        "작업에 생성되는 파일이나 변경된 내용이 실제 작성되었는지 다시 확인하고, "
        + "모든 작업이 끝나면 반드시 main 브랜치에 commit과 push를 수행해라.\n\n"
        + "DB 작업이 필요하면 DB 연결정보를 소스에서 확인하여 사용하여 직접 처리하고, "
        + "반영한 결과까지 조회해서 확인해라.";

    /// <summary>
    /// 사람이 적은 말 뒤에 <see cref="Text"/> 를 붙인다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>빈 글에는 붙이지 않는다.</b> 「이어서 지시」는 덧붙일 말 없이 보낼 수
    /// 있는데, 거기에 이것만 실어 보내면 <b>사람이 아무 말도 안 했는데 지시가
    /// 하나 생긴다.</b>
    /// </para>
    /// <para>
    /// <b>이미 들어 있으면 또 붙이지 않는다.</b> 다시 보내기·이어서 지시로
    /// 같은 글이 몇 번 돌 수 있고, 그때마다 붙으면 지시 끝이 같은 문단으로
    /// 도배된다.
    /// </para>
    /// </remarks>
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
