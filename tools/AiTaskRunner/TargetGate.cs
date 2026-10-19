namespace AiTaskRunner;

/// <summary>
/// 같은 대상을 두 실행이 동시에 건드리지 않게 막는다. 원본 직접은 실행이 끝날 때까지, 나머지는 준비(fetch·worktree add)가 끝날 때까지 잠근다.
/// 막지 않고 줄을 세운다 — 오류로 끝내면 사람이 같은 것을 다시 눌러야 한다. 줄이 길어지면 다른 대상이 늦을 수 있다(자리를 먼저 잡고 차례를 나중에 받기 때문).
/// 장비 안에서만 막는다. 같은 대상을 두 장비가 나눠 갖는 구성은 없다.
/// </summary>
public sealed class TargetGate
{
    private readonly Dictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>표 자체를 지키는 자물쇠. 잠금을 <b>만드는</b> 순간이 겹칠 수 있다.</summary>
    private readonly object _sync = new();

    /// <summary>
    /// <paramref name="path"/> 대상의 차례를 받는다. 받을 때까지 기다린다.
    /// </summary>
    /// <param name="path">
    /// 대상 경로. 비어 있으면 잠그지 않는다 — 폴더가 없는 대상은 겹칠 자리도 없다.
    /// </param>
    /// <param name="waiting">
    /// 바로 못 받았을 때 한 번 부른다. 사람에게 <b>왜 안 시작하는지</b> 말해 주는
    /// 자리다 — 말하지 않으면 「눌렀는데 아무 일도 없다」로 보인다.
    /// </param>
    public async Task<IAsyncDisposable> HoldAsync(
        string? path, Func<Task>? waiting, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Free.Instance;
        }

        // 같은 폴더를 다르게 적은 경로(뒤 슬래시·상대경로)가 다른 자물쇠가 되면 막는 뜻이 없다 — 한 모양으로 맞춘다.
        var key = Normalize(path);

        SemaphoreSlim gate;

        lock (_sync)
        {
            if (!_locks.TryGetValue(key, out gate!))
            {
                gate = new SemaphoreSlim(1, 1);
                _locks[key] = gate;
            }
        }

        if (!await gate.WaitAsync(0, ct))
        {
            if (waiting is not null)
            {
                await waiting();
            }

            await gate.WaitAsync(ct);
        }

        return new Held(gate);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            // 경로가 이상해도 잠그기는 해야 한다 — 적힌 그대로를 열쇠로 쓴다.
            return path.Trim();
        }
    }

    private sealed class Held(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _done;

        public ValueTask DisposeAsync()
        {
            // 두 번 놓으면 상한을 넘겨 던진다 — 한 번만 놓는다.
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>잠글 것이 없을 때 돌려주는 빈 손잡이.</summary>
    private sealed class Free : IAsyncDisposable
    {
        public static readonly Free Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
