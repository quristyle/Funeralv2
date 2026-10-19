using JSini.Shared.Infrastructure.Time;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>AI CLI 한도 스냅샷을 프로세스 안에 들고 있는 통. 서버가 계산하지 않고, 실행기가 주기적으로 CLI 의 /usage 를 읽어 보낸 값을 담는다.</summary>
public sealed class AiUsageCache
{
    /// <summary>그물. 주기가 아니라 Drop 을 놓쳤을 때 값이 영영 굳지 않게 하는 상한이다. 보고 주기(15분)보다 넉넉히 둔다.</summary>
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

    /// <summary>버린다. 실행기가 새 보고를 올렸을 때 부른다 — 안 부르면 방금 올라온 값이 Ttl 동안 화면에 안 나온다.</summary>
    public void Drop() => Volatile.Write(ref _entry, null);
}
