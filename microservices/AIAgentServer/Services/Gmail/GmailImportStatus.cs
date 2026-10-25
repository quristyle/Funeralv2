namespace AIAgentServer.Services.Gmail;

/// <summary>적재가 어디까지 갔는지 담는 자리. 수십만 통을 읽는 동안 화면이 물어볼 곳이 있어야 한다.
///
/// <para>싱글톤이고 <b>메모리에만</b> 있다 — 서비스를 다시 띄우면 지워진다. 적재 자체는 다시 돌리면
/// 되므로(겹침은 버린다) 이 값을 표에 남기지 않는다. 한 번에 하나만 돈다.</para></summary>
public sealed class GmailImportStatus
{
    private int _running;
    private long _parsed;
    private long _inserted;
    private long _failed;

    public bool Running => Volatile.Read(ref _running) == 1;

    public string? AccountId { get; private set; }

    public string? Path { get; private set; }

    public long Parsed => Interlocked.Read(ref _parsed);

    /// <summary>실제로 표에 들어간 줄. <see cref="Parsed"/> 와의 차이가 <b>겹쳐서 버린 수</b>다.</summary>
    public long Inserted => Interlocked.Read(ref _inserted);

    public long Failed => Interlocked.Read(ref _failed);

    public DateTime? StartedAt { get; private set; }

    public DateTime? FinishedAt { get; private set; }

    public string? Error { get; private set; }

    /// <summary>적재를 시작한다. 이미 돌고 있으면 거짓을 돌려준다 — 둘이 같은 파일을 동시에 읽으면
    /// 겹침 판정만 두 배로 돌 뿐 얻는 것이 없다.</summary>
    public bool TryBegin(string accountId, string path)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return false;

        AccountId = accountId;
        Path = path;
        Interlocked.Exchange(ref _parsed, 0);
        Interlocked.Exchange(ref _inserted, 0);
        Interlocked.Exchange(ref _failed, 0);
        StartedAt = DateTime.UtcNow;
        FinishedAt = null;
        Error = null;
        return true;
    }

    public void CountParsed() => Interlocked.Increment(ref _parsed);

    public void CountInserted(int count) => Interlocked.Add(ref _inserted, count);

    public void CountFailed() => Interlocked.Increment(ref _failed);

    public void Finish(string? error)
    {
        Error = error;
        FinishedAt = DateTime.UtcNow;
        Interlocked.Exchange(ref _running, 0);
    }
}
