namespace AiTaskRunner;

/// <summary>
/// <b>같은 대상을 두 실행이 동시에 건드리지 않게 막는다.</b>
///
/// <para>
/// 실행기는 동시 다섯 건까지 돈다(<c>Runner:MaxParallel</c>). 서로 다른
/// 대상이면 문제가 없지만 <b>같은 대상</b>이면 두 가지가 겹친다.
/// </para>
///
/// <list type="number">
///   <item><description>
///     <b>원본 직접(inplace)</b> — 두 실행이 같은 체크아웃에서 같은 파일을
///     고친다. 서로의 편집을 덮고, 게이트가 커밋할 때 남의 변경까지 함께
///     집어 간다. 실제로 드러난 증상은 「정본이 깨끗하지 않습니다」였는데,
///     그건 <b>병이 아니라 열</b>이다 — 앞 실행이 아직 고치는 중이라 더러웠던
///     것뿐이고, 그 검사를 끄면 대신 결과가 섞인다.
///   </description></item>
///   <item><description>
///     <b>준비 구간</b> — worktree·복사본도 <c>git fetch</c> · <c>git pull</c> ·
///     <c>git worktree add</c> 는 <b>정본에서</b> 돈다. 둘이 겹치면 git 이
///     <c>index.lock</c> 을 잡지 못해 한쪽이 죽는다.
///   </description></item>
/// </list>
///
/// <para>
/// 그래서 잠그는 <b>범위가 둘</b>이다 — 원본 직접은 실행이 끝날 때까지,
/// 나머지는 준비가 끝날 때까지. 뒤엣것은 몇 초라 사실상 안 기다린다.
/// </para>
///
/// <para>
/// <b>막지 않고 기다린다.</b> 예전처럼 오류로 끝내면 사람이 같은 것을 다시
/// 눌러야 하는데, 그때 앞 실행이 아직 돌고 있으면 또 같은 오류가 난다.
/// 줄을 서면 그냥 차례가 온다.
/// </para>
///
/// <para>
/// <b>줄이 길어지면 다른 대상이 늦어질 수 있다.</b> 같은 원본 직접 대상으로
/// 다섯 건이 한꺼번에 들어오면 하나가 돌고 넷이 <c>MaxParallel</c> 자리를 든 채
/// 기다린다 — 그동안 다른 대상은 못 시작한다. 자리를 먼저 잡고 차례를 나중에
/// 받기 때문인데, 순서를 뒤집으려면 집어가기 자체를 대상별로 나눠야 한다.
/// 원본 직접은 예외적인 설정이고(설계 7.3) 막히는 것보다는 늦는 편이 나아서
/// 여기까지 둔다. 실제로 걸리면 그때 집어가기를 손본다.
/// </para>
///
/// <para>
/// <b>장비 안에서만 막는다.</b> 같은 대상을 두 장비가 나눠 갖는 구성은 없다
/// (대상에 장비가 박혀 있다). 그런 구성이 생기면 그때는 서버 쪽에서 집어가기를
/// 막아야 하고, 이 클래스로는 부족하다.
/// </para>
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

        // 같은 폴더를 다르게 적은 경로(뒤 슬래시 · 상대경로)가 서로 다른
        // 자물쇠가 되면 막는 뜻이 없다. 한 모양으로 맞춰서 센다.
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
            // 경로가 이상해도 잠그기는 해야 한다. 적힌 그대로를 열쇠로 쓴다.
            return path.Trim();
        }
    }

    private sealed class Held(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _done;

        public ValueTask DisposeAsync()
        {
            // 두 번 놓으면 상한을 넘겨 던진다. 한 번만 놓는다.
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
