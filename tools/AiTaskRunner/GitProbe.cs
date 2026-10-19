using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// 대상 폴더 하나를 들여다본다 — 읽기만 한다. 화면의 「대상 git 상태」 값을 만들며, 서버가 호스트 경로를 볼 수 없어 여기 있다.
/// fetch 는 하지 않는다 — 앞섬·뒤처짐은 이미 받아 둔 원격 추적 참조로만 센다. 실패는 Snapshot.ProbeError 에 담고 예외를 올리지 않는다.
/// </summary>
public sealed class GitProbe(ILogger<GitProbe> logger)
{
    /// <summary>더러운 파일 목록을 앞 몇 줄까지 싣나. 숫자만으로는 무엇이 더러운지 알 수 없고, 전부 실으면 수천 줄이 DB 로 간다.</summary>
    private const int DirtyLines = 20;

    /// <summary>칸을 가르는 글자(git 의 %x1f). 탭이나 | 는 커밋 제목이 품으면 조용히 어긋나므로 쓰지 않는다.</summary>
    private const char Unit = '';

    /// <summary>한 대상에 쓰는 시간의 상한. 넘으면 그 대상만 오류로 남는다.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    public async Task<Snapshot> ProbeAsync(string? path, CancellationToken ct)
    {
        var snap = new Snapshot();

        if (string.IsNullOrWhiteSpace(path))
        {
            snap.ProbeError = "대상 경로가 비어 있습니다.";
            return snap;
        }

        if (!Directory.Exists(path))
        {
            // 「이 장비에는」이 중요하다 — 대상은 장비에 묶일 수 있고(ai_target.runner_nm), 안 묶으면 아무 장비나 집는다.
            snap.ProbeError = "이 장비에 그 경로가 없습니다.";
            return snap;
        }

        snap.PathExists = true;

        // worktree 의 .git 은 폴더가 아니라 파일이다. 폴더만 보면 worktree 에서 저장소가 아닌 것으로 읽힌다.
        var dotGit = Path.Combine(path, ".git");
        snap.IsRepo = Directory.Exists(dotGit) || File.Exists(dotGit);

        if (!snap.IsRepo)
        {
            return snap;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(Budget);

        try
        {
            await ReadStatusAsync(path, snap, budget.Token);
            await ReadHeadAsync(path, snap, budget.Token);
            await ReadExtrasAsync(path, snap, budget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            snap.ProbeError = $"{Budget.TotalSeconds:0}초 안에 다 보지 못했습니다.";
        }
        catch (Exception ex)
        {
            logger.LogDebug("대상 {Path} 를 보지 못했습니다: {Message}", path, ex.Message);
            snap.ProbeError = ex.Message;
        }

        return snap;
    }

    /// <summary>가지·앞섬·뒤처짐과 더러운 칸들. --porcelain=v2 (v1 은 앞섬을 괄호 문장으로 준다), --untracked-files=normal (장비 설정에 따라 기본값이 달라 고정),
    /// --no-optional-locks (평범한 status 는 index.lock 을 잡아 작업을 방해한다).</summary>
    private static async Task ReadStatusAsync(string path, Snapshot snap, CancellationToken ct)
    {
        var r = await RunAsync(path, ct,
            "--no-optional-locks", "status", "--porcelain=v2", "--branch",
            "--untracked-files=normal");

        if (r.ExitCode != 0)
        {
            snap.ProbeError = Head(r.Output, 3);
            return;
        }

        var dirty = new List<string>();

        foreach (var line in r.Output.Split('\n'))
        {
            var s = line.TrimEnd('\r');

            if (s.Length == 0)
            {
                continue;
            }

            if (s[0] == '#')
            {
                ReadBranchHeader(s, snap);
                continue;
            }

            // 1·2 = 추적 중인 파일 변경, u = 충돌, ? = 추적 안 함. ! 는 요청하지 않아 오지 않는다.
            switch (s[0])
            {
                case '1' or '2':
                    // 1 XY … — X 가 색인, Y 가 작업본. 한 파일이 둘 다일 수 있어 배타적으로 세지 않는다.
                    if (s.Length > 3)
                    {
                        if (s[2] != '.')
                        {
                            snap.Staged++;
                        }

                        if (s[3] != '.')
                        {
                            snap.Unstaged++;
                        }
                    }

                    break;

                case 'u':
                    snap.Conflicted++;
                    break;

                case '?':
                    snap.Untracked++;
                    break;

                default:
                    continue;
            }

            if (dirty.Count < DirtyLines)
            {
                dirty.Add(Pretty(s));
            }
        }

        var total = snap.Staged + snap.Unstaged + snap.Untracked + snap.Conflicted;

        snap.DirtyFiles = dirty.Count == 0
            ? null
            : string.Join('\n', dirty)
              + (total > dirty.Count ? $"\n… 외 {total - dirty.Count}건" : string.Empty);
    }

    /// <summary><c>#</c> 로 시작하는 머리줄들. 가지·추적 대상·앞섬/뒤처짐.</summary>
    private static void ReadBranchHeader(string line, Snapshot snap)
    {
        // "# branch.head main" · "# branch.upstream origin/main" · "# branch.ab +3 -1"
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
        {
            return;
        }

        switch (parts[1])
        {
            case "branch.head":
                // 가지에 없으면 git 이 (detached) 를 준다 — 그대로 둔다. 화면이 그 말을 보여 준다.
                snap.Branch = parts[2];
                break;

            case "branch.upstream":
                snap.Upstream = parts[2];
                break;

            case "branch.ab" when parts.Length >= 4:
                snap.Ahead = Num(parts[2]);
                snap.Behind = Num(parts[3]);
                break;
        }
    }

    /// <summary>마지막 커밋. 「언제 것에서 멈춰 있나」에 답한다.</summary>
    private static async Task ReadHeadAsync(string path, Snapshot snap, CancellationToken ct)
    {
        var r = await RunAsync(path, ct,
            "--no-optional-locks", "log", "-1", "--format=%H%x1f%an%x1f%cI%x1f%s");

        if (r.ExitCode != 0)
        {
            // 커밋이 하나도 없는 저장소가 여기로 온다. 오류가 아니다.
            return;
        }

        var f = r.Output.Split(Unit);

        if (f.Length < 4)
        {
            return;
        }

        snap.HeadSha = f[0].Trim();
        snap.HeadAuthor = f[1].Trim();
        snap.HeadSubject = f[3].Trim();

        // %cI 는 오프셋이 붙는다. UTC 로 맞춰 올린다 — 받는 칸과 시스템 규칙이 UTC 이고, 장비 지역 시각으로 보내면 나라마다 값이 달라진다.
        if (DateTimeOffset.TryParse(
                f[2].Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var when))
        {
            snap.HeadDt = when.UtcDateTime;
        }
    }

    /// <summary>원격 주소와 stash 수. stash 는 실행기가 직접 쌓는 곳이라(ParkAsync) 쌓여 있으면 사람이 치워야 한다.</summary>
    private static async Task ReadExtrasAsync(string path, Snapshot snap, CancellationToken ct)
    {
        var remote = await RunAsync(path, ct,
            "--no-optional-locks", "config", "--get", "remote.origin.url");

        if (remote.ExitCode == 0 && remote.Output.Length > 0)
        {
            snap.RemoteUrl = remote.Output.Split('\n')[0].Trim();
        }

        var stash = await RunAsync(path, ct, "--no-optional-locks", "stash", "list");

        if (stash.ExitCode == 0)
        {
            snap.StashCount = stash.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Count(l => l.StartsWith("stash@{", StringComparison.Ordinal));
        }
    }

    /// <summary>porcelain v2 한 줄을 화면용으로 줄인다 — 상태 두 글자와 경로만 남긴다.</summary>
    private static string Pretty(string line)
    {
        if (line[0] == '?')
        {
            return "?? " + line[2..];
        }

        var parts = line.Split(' ');

        if (line[0] == 'u')
        {
            // u XY sub m1 m2 m3 mW h1 h2 h3 path — 열한째 칸부터가 경로다.
            return parts.Length > 10 ? "UU " + string.Join(' ', parts[10..]) : line;
        }

        // 1 XY sub mH mI mW hH hI path             — 아홉째 칸부터
        // 2 XY sub mH mI mW hH hI X<점수> path     — 열째 칸부터 (이름이 바뀐 것)
        var from = line[0] == '2' ? 9 : 8;

        return parts.Length > from
            ? parts[1] + " " + string.Join(' ', parts[from..])
            : line;
    }

    /// <summary><c>+3</c> · <c>-1</c> 처럼 부호가 붙은 수. 크기만 쓴다.</summary>
    private static int Num(string token) =>
        int.TryParse(token.TrimStart('+', '-'), out var n) ? n : 0;

    private static string Head(string text, int lines) =>
        string.Join(' ', text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(lines));

    /// <summary>git 한 번. 셸을 거치지 않고 인자를 하나씩 넣는다. 자격을 묻지 못하게 막고, 시간 예산에 걸리면 죽인다 — 아무도 안 보는 사이에 도는 감시라서다.
    /// Workspace 의 것과 모양이 같지만 private 이라 따로 둔다(둘이 쓰면 복제, 셋째부터 승격).</summary>
    private static async Task<(int ExitCode, string Output)> RunAsync(
        string workDir, CancellationToken ct, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = workDir,
        };

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        // 자격을 물으려 들면 그 자리에서 영영 선다 — 시간 예산으로 죽기는 하나 애초에 묻지 못하게 막는다.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GIT_ASKPASS"] = "true";

        using var p = Process.Start(psi)!;

        try
        {
            var stdout = await p.StandardOutput.ReadToEndAsync(ct);
            var stderr = await p.StandardError.ReadToEndAsync(ct);

            await p.WaitForExitAsync(ct);

            return (p.ExitCode, (stdout.Length > 0 ? stdout : stderr).Trim());
        }
        catch (OperationCanceledException)
        {
            // 예산을 넘겼다. **남겨 두면 그 git 이 계속 돈다.**
            try
            {
                p.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 그 사이에 스스로 끝났다.
            }

            throw;
        }
    }

    /// <summary>본 결과 한 벌. 본 시각은 담지 않는다 — 서버가 받을 때 now() 로 찍는다(이 장비의 시계가 어긋나도 화면이 틀리지 않게).</summary>
    public sealed class Snapshot
    {
        public long TargetKey { get; set; }

        public bool PathExists { get; set; }
        public bool IsRepo { get; set; }

        public string? Branch { get; set; }
        public string? Upstream { get; set; }
        public int Ahead { get; set; }
        public int Behind { get; set; }

        public int Staged { get; set; }
        public int Unstaged { get; set; }
        public int Untracked { get; set; }
        public int Conflicted { get; set; }
        public int StashCount { get; set; }

        public string? HeadSha { get; set; }
        public string? HeadSubject { get; set; }
        public string? HeadAuthor { get; set; }
        public DateTime? HeadDt { get; set; }

        public string? RemoteUrl { get; set; }
        public string? DirtyFiles { get; set; }
        public string? ProbeError { get; set; }
    }
}
