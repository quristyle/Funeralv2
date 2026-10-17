using System.Data;

using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>실행기가 대 봐야 할 실행 한 건.</summary>
/// <param name="RunKey">이 실행.</param>
/// <param name="TargetPath">대상 위치 — <b>경로를 재는 뿌리</b>다.</param>
/// <param name="TargetKind"><c>repo</c> 면 git 에게도 물을 수 있다.</param>
/// <param name="WorkspacePath">
/// 그때 일하던 자리. <b>아직 안 치워졌으면 거기부터 본다</b> — 올리지 못한
/// 새 파일은 대상 저장소에는 아직 없다.
/// </param>
/// <param name="GitBranch">
/// 그때 쓰던 가지. <b>대상 저장소의 작업본은 늘 뒤처져 있어서</b>
/// 거기만 보면 방금 만든 파일이 「없는 경로」가 된다
/// (<c>AiTaskRunner.FileProbe.IndexAsync</c>).
/// </param>
/// <param name="Paths">대 볼 경로들(추정 줄의 날것 그대로).</param>
public sealed record AiRunFileCheckJob(
    long RunKey,
    string? TargetPath,
    string? TargetKind,
    string? WorkspacePath,
    string? GitBranch,
    IReadOnlyList<string> Paths);

/// <summary>실행기가 하나를 대 보고 돌려주는 답.</summary>
/// <param name="Raw">물어본 그대로의 경로. <b>이것으로 줄을 찾는다.</b></param>
/// <param name="Path">찾았으면 <b>대상 위치에서 본 온 경로</b>. 못 찾았으면 빈 글자.</param>
/// <param name="State"><c>found</c> · <c>missing</c> · <c>ambiguous</c>.</param>
public sealed class AiRunFileCheckResult
{
    public string? Raw { get; set; }

    public string? Path { get; set; }

    public string? State { get; set; }
}

/// <summary>실행 한 건에 대한 답 묶음.</summary>
public sealed class AiRunFileCheckReport
{
    public long RunKey { get; set; }

    public List<AiRunFileCheckResult>? Results { get; set; }
}

/// <summary>
/// 「처리 파일」의 <b>추정 줄을 실제 파일에 대 보는</b> 일의 서버 쪽 절반.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 서버가 직접 못 하나.</b> ProjMngServer 는 컨테이너 안에서 돌고
/// 대상 경로(<c>/home/lee/Funeralv2</c> 따위)는 호스트의 것이다. 「대상의
/// git 상태」가 실행기를 거치는 것과 똑같은 까닭이다
/// (<c>AiRunnerController</c> 의 그 묶음 머리말).
/// </para>
/// <para>
/// <b>왜 필요한가 (2026-10-17).</b> 결과문에서 뽑은 경로는 모델이 글을 읽고
/// 적은 것이라, 운영에 쌓인 것을 훑어 보면 <c>AiChangedFiles.Merge</c>(메서드
/// 이름) · <c>projmng.ai_task_run.files_merged_at</c>(DB 칸 이름)처럼
/// <b>파일이 아닌 것</b>이 섞여 있고, <c>web/AiAskPanel.razor</c> ·
/// <c>Pages/AiDashboard.razor</c> 처럼 <b>중간 마디가 빠진 것</b>도 흔하다.
/// 그대로 두면 「처리 파일」 탭이 <b>없는 경로를 멀쩡한 얼굴로</b> 세운다.
/// </para>
/// <para>
/// <b>할 일은 글자로 센다.</b> 따로 칸을 두지 않고 <c>changed_files</c> 안에
/// <c>?</c> 표가 남아 있는 줄이 있으면 아직 안 대 본 건이다 — 한 번 대 보면
/// 그 표가 <c>*</c>·<c>!</c>·<c>~</c> 로 바뀌므로 <b>스스로 멎는다</b>.
/// 대상이 이 장비에 없어 끝내 답이 안 오는 건만 남는데, 그것은 <b>기한</b>
/// (<see cref="Days"/>)으로 끊는다.
/// </para>
/// </remarks>
public sealed class AiRunFileCheckService(IConfiguration configuration, ILogger<AiRunFileCheckService> logger)
{
    /// <summary>한 바퀴에 내주는 실행 수. 실행기가 저장소 목록을 그만큼 훑는다.</summary>
    private const int MaxRuns = 10;

    /// <summary>
    /// 이만큼 지난 건은 더 안 묻는다. <b>끝내 답이 안 오는 건</b>(대상이 다른
    /// 장비에 있거나 지워진 건)이 영원히 돌지 않게 하는 울타리다.
    /// </summary>
    private const int Days = 14;

    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// <b>이 장비가 대 볼 수 있는</b> 실행들. 거르는 규칙은 집어가기와 같다 —
    /// 대상에 장비가 박혀 있으면 그 장비만 본다.
    /// </summary>
    public async Task<List<AiRunFileCheckJob>> PendingAsync(string runnerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return [];
        }

        using var db = Open();

        // 추정 표(`?`)가 붙은 줄이 하나라도 있는 실행만. 줄 앞머리는
        // `M?\t` 처럼 **한 글자 + 물음표 + 탭**이다.
        var rows = await db.QueryAsync<Row>(new CommandDefinition("""
            SELECT r.run_key        AS RunKey,
                   r.changed_files  AS ChangedFiles,
                   r.workspace_path AS WorkspacePath,
                   r.git_branch     AS GitBranch,
                   g.target_path    AS TargetPath,
                   g.target_kind    AS TargetKind
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t   ON t.task_key   = r.task_key
              JOIN projmng.ai_target g ON g.target_key = t.target_key
             WHERE r.finished_at IS NOT NULL
               AND r.finished_at > now() - make_interval(days => @Days)
               AND r.changed_files ~ '(^|\n)[^\t]*\?\t'
               AND (g.runner_nm IS NULL OR g.runner_nm = @runnerName)
             ORDER BY r.run_key DESC
             LIMIT @MaxRuns
            """, new { runnerName, Days, MaxRuns }, cancellationToken: ct));

        var jobs = new List<AiRunFileCheckJob>();

        foreach (var row in rows)
        {
            var paths = AiChangedFiles.Parse(row.ChangedFiles)
                .Where(f => f.Check == AiChangedFileCheck.Pending)
                .Select(f => f.Path)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (paths.Count > 0)
            {
                jobs.Add(new AiRunFileCheckJob(
                    row.RunKey, row.TargetPath, row.TargetKind,
                    row.WorkspacePath, row.GitBranch, paths));
            }
        }

        return jobs;
    }

    /// <summary>
    /// 실행기가 대 본 결과를 새긴다. <b>바뀐 실행의 수</b>를 돌려준다.
    /// </summary>
    /// <remarks>
    /// <b>그 사이에 목록이 바뀌었을 수 있다</b>(따라잡기가 다시 합쳤다든가).
    /// 그래서 지금 적혀 있는 글을 다시 읽어 그 위에 새긴다 — 실행기가 보내 온
    /// 글을 통째로 덮어쓰지 않는다. 날것 경로가 사라진 줄은 그냥 안 맞는다.
    /// </remarks>
    public async Task<int> ApplyAsync(
        string runnerName, IReadOnlyList<AiRunFileCheckReport> reports, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || reports.Count == 0)
        {
            return 0;
        }

        using var db = Open();
        var saved = 0;

        foreach (var report in reports)
        {
            var map = new Dictionary<string, (string Path, AiChangedFileCheck Check)>(StringComparer.Ordinal);

            foreach (var r in report.Results ?? [])
            {
                if (r.Raw is not { Length: > 0 } raw)
                {
                    continue;
                }

                map[raw] = (r.Path ?? string.Empty, r.State switch
                {
                    "found" => AiChangedFileCheck.Found,
                    "ambiguous" => AiChangedFileCheck.Ambiguous,
                    _ => AiChangedFileCheck.Missing,
                });
            }

            if (map.Count == 0)
            {
                continue;
            }

            var current = await db.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
                "SELECT changed_files FROM projmng.ai_task_run WHERE run_key = @RunKey",
                new { report.RunKey }, cancellationToken: ct));

            // 경로만 적힌 목록이지만 **가리는 손길은 거친다** — 실행기가 펴 준
            // 온 경로에 작업공간 이름이 섞여 들어올 수 있다(합치는 자리와 같다).
            if (SecretMask.Apply(AiChangedFiles.ApplyChecks(current, map)) is not { Length: > 0 } next)
            {
                continue;
            }

            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE projmng.ai_task_run SET changed_files = @next WHERE run_key = @RunKey
                """, new { report.RunKey, next }, cancellationToken: ct));

            saved++;
        }

        if (saved > 0)
        {
            logger.LogInformation(
                "{Runner} 가 대 본 처리 파일 결과로 실행 {Count}건을 고쳤습니다.", runnerName, saved);
        }

        return saved;
    }

    private sealed class Row
    {
        public long RunKey { get; set; }

        public string? ChangedFiles { get; set; }

        public string? WorkspacePath { get; set; }

        public string? GitBranch { get; set; }

        public string? TargetPath { get; set; }

        public string? TargetKind { get; set; }
    }
}
