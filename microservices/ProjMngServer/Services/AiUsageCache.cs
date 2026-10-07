using JSini.Shared.Infrastructure.Time;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// AI CLI 한도 스냅샷을 <b>프로세스 안에</b> 들고 있는 통.
/// </summary>
/// <remarks>
/// <para>
/// [왜 통을 두나 — 표가 이미 「미리 구해 둔 것」인데]
/// </para>
/// <para>
/// 한도는 서버가 그때그때 계산하는 값이 아니다. 실행기가 15분마다 CLI 의
/// <c>/usage</c> 를 읽어 <c>projmng.ai_usage_snapshot</c> 에 올려 두고
/// (<c>RunnerOptions.UsageIntervalMinutes</c>), 서버는 그 표를 읽기만 한다 —
/// 즉 <b>수집은 이미 배치다.</b> 그런데 <b>읽는 쪽</b>은 배치가 아니었다.
/// 「빠른 지시」 화면이 열릴 때마다 DB 까지 한 번씩 갔다.
/// </para>
/// <para>
/// 통에 담으면 <b>보고와 보고 사이에는 DB 를 한 번도 안 본다.</b> 화면이
/// 몇 번 열리든 읽기는 메모리 한 번이다.
/// </para>
/// <para>
/// [시간으로 버리지 않고 <b>보고가 들어올 때</b> 버린다]
/// </para>
/// <para>
/// 「30분마다 다시 읽는다」 같은 주기로 두면 값이 최대 「보고 주기 + 통 주기」만큼
/// 묵는다 — 15 + 30 = 45분이고, 화면은 60분이 넘으면 <b>「오래된 값」</b>이라고
/// 적는다(<c>AiUsageText.Stale</c>). 경계에 바짝 붙는다.
/// </para>
/// <para>
/// 그럴 까닭이 없다. 이 표를 쓰는 사람은 <b>실행기 하나뿐</b>이고
/// (<c>AiUsageService.SaveAsync</c>) 그 자리에서 <see cref="Drop"/> 을 부른다.
/// 그래서 통의 값은 <b>언제나 마지막 보고 그대로</b>다 — 늦는 자리가 새로
/// 생기지 않는다. <see cref="Ttl"/> 은 주기가 아니라 <b>그 호출을 놓쳤을 때를
/// 위한 그물</b>이다(보고 주기보다 넉넉히 둔다).
/// </para>
/// <para>
/// <b>싱글턴이다.</b> scoped 로 두면 요청 하나의 수명이라 아무것도 막지 못한다.
/// </para>
/// </remarks>
public sealed class AiUsageCache
{
    /// <summary>
    /// 그물. <b>주기가 아니다</b> — <see cref="Drop"/> 을 놓쳤을 때 값이 영영
    /// 굳지 않게 하는 상한이다. 보고 주기(15분)보다 넉넉히 둔다.
    /// </summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);

    private sealed record Entry(List<AiUsageSnapshot> Rows, DateTime At);

    private Entry? _entry;

    /// <summary>담아 둔 것. 없거나 묵었으면 <c>null</c> 이다.</summary>
    public List<AiUsageSnapshot>? Get()
        => Volatile.Read(ref _entry) is { } entry && AppTime.UtcNow - entry.At < Ttl
            ? entry.Rows
            : null;

    /// <summary>읽어 온 것을 담는다.</summary>
    public void Put(List<AiUsageSnapshot> rows)
        => Volatile.Write(ref _entry, new Entry(rows, AppTime.UtcNow));

    /// <summary>
    /// 버린다. <b>실행기가 새 보고를 올렸을 때</b> 부른다 — 안 부르면
    /// 방금 올라온 값이 <see cref="Ttl"/> 동안 화면에 안 나온다.
    /// </summary>
    public void Drop() => Volatile.Write(ref _entry, null);
}
