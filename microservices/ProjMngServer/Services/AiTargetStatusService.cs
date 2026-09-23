using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// AI 작업 대상의 git 상태 — <c>projmng.ai_target_status</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>이 서비스는 git 을 부르지 않는다.</b> 부를 수가 없다 — ProjMngServer 는
/// 컨테이너 안에서 돌고 대상 경로는 호스트의 것이다(같은 이유로
/// <see cref="AiTargetService.ValidatePath"/> 도 realpath 를 쓰지 않는다).
/// 실제로 들여다보는 것은 호스트에 상주하는 실행기고, 여기는 그것이 적어
/// 두고 간 값을 받아 두었다가 화면에 내주는 자리다.
/// </para>
/// <para>
/// 그래서 하는 일이 셋뿐이다 — <b>읽기</b>(화면) · <b>쓰기</b>(실행기) ·
/// <b>「지금 봐 달라」 표시</b>(화면 → 실행기).
/// </para>
/// </remarks>
public sealed class AiTargetStatusService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// 화면이 읽는 목록 — 대상 전부와, 있으면 그 상태.
    /// </summary>
    /// <remarks>
    /// <b>바깥 조인이다.</b> 아직 한 번도 안 본 대상이 목록에서 사라지면
    /// 「실행기가 안 떠 있다」가 「대상이 없다」로 보인다 — 이 화면이 답해야
    /// 하는 물음 중 하나가 바로 그것이다.
    /// </remarks>
    public async Task<List<AiTargetStatusRow>> ListAsync(bool onlyEnabled = false)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTargetStatusRow>("""
            SELECT t.target_key        AS TargetKey,
                   t.target_nm         AS TargetNm,
                   t.target_kind       AS TargetKind,
                   t.target_path       AS TargetPath,
                   t.default_ref       AS DefaultRef,
                   t.isolation_mode    AS IsolationMode,
                   t.runner_nm         AS TargetRunnerNm,
                   t.allow_push        AS AllowPush,
                   t.is_enabled        AS IsEnabled,
                   t.running_run_key   AS RunningRunKey,

                   s.runner_nm         AS RunnerNm,
                   COALESCE(s.path_exists, false) AS PathExists,
                   COALESCE(s.is_repo,     false) AS IsRepo,
                   s.branch            AS Branch,
                   s.upstream          AS Upstream,
                   COALESCE(s.ahead,      0) AS Ahead,
                   COALESCE(s.behind,     0) AS Behind,
                   COALESCE(s.staged,     0) AS Staged,
                   COALESCE(s.unstaged,   0) AS Unstaged,
                   COALESCE(s.untracked,  0) AS Untracked,
                   COALESCE(s.conflicted, 0) AS Conflicted,
                   COALESCE(s.stash_count,0) AS StashCount,
                   s.head_sha          AS HeadSha,
                   s.head_subject      AS HeadSubject,
                   s.head_author       AS HeadAuthor,
                   s.head_dt           AS HeadDt,
                   s.remote_url        AS RemoteUrl,
                   s.dirty_files       AS DirtyFiles,
                   s.probe_error       AS ProbeError,
                   s.probed_at         AS ProbedAt,
                   s.probe_req_dt      AS ProbeReqDt
              FROM projmng.ai_target t
              LEFT JOIN projmng.ai_target_status s ON s.target_key = t.target_key
             WHERE t.is_deleted = false
               AND (@onlyEnabled = false OR t.is_enabled = true)
             ORDER BY t.is_enabled DESC, t.target_nm
            """, new { onlyEnabled });

        return [.. rows];
    }

    /// <summary>
    /// 실행기가 물어보는 목록 — <b>이 장비가 볼 수 있는 대상</b>.
    /// </summary>
    /// <remarks>
    /// 거르는 규칙이 집어가기(<see cref="AiRunService.ClaimAsync"/>)와 같다 —
    /// <b>장비가 맞는 것만</b>이다. 다른 장비의 경로를 여기서 들여다보면
    /// 「경로가 없습니다」가 대상 수만큼 적히고, 그 줄들은 전부 거짓이다.
    /// </remarks>
    public async Task<List<AiTargetProbe>> ProbeListAsync(string runnerName)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTargetProbe>("""
            SELECT t.target_key  AS TargetKey,
                   t.target_nm   AS TargetNm,
                   t.target_path AS TargetPath,
                   t.target_kind AS TargetKind,
                   (s.probe_req_dt IS NOT NULL) AS ProbeRequested,
                   s.probed_at   AS ProbedAt
              FROM projmng.ai_target t
              LEFT JOIN projmng.ai_target_status s ON s.target_key = t.target_key
             WHERE t.is_deleted = false
               AND t.is_enabled = true
               AND (t.runner_nm IS NULL OR t.runner_nm = @runnerName)
             ORDER BY t.target_key
            """, new { runnerName });

        return [.. rows];
    }

    /// <summary>
    /// 실행기가 보고한 스냅샷을 넣는다. 대상당 한 줄이라 덮어쓴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>본 시각은 서버가 찍는다</b>(<c>now()</c>). 실행기가 보낸 시각을
    /// 그대로 쓰면 그 장비의 시계가 어긋났을 때 화면의 「3분 전」이 「2시간
    /// 뒤」가 된다 — 지금은 같은 장비지만 실행기는 늘어날 수 있는 쪽이다.
    /// </para>
    /// <para>
    /// 넣으면서 <c>probe_req_dt</c> 를 지운다. 「지금 확인」의 답이 이 줄이다.
    /// </para>
    /// </remarks>
    public async Task<int> ReportAsync(string runnerName, IReadOnlyList<AiTargetStatus> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        using var db = Open();

        var done = 0;

        foreach (var s in items)
        {
            // 없는 대상(지워졌다)이 오면 외래키가 막는다. 한 건이 막혀
            // 나머지 보고가 통째로 사라지지 않게 건별로 넣는다.
            try
            {
                done += await db.ExecuteAsync("""
                    INSERT INTO projmng.ai_target_status
                         ( target_key, runner_nm, path_exists, is_repo,
                           branch, upstream, ahead, behind,
                           staged, unstaged, untracked, conflicted, stash_count,
                           head_sha, head_subject, head_author, head_dt,
                           remote_url, dirty_files, probe_error,
                           probed_at, probe_req_dt, cre_dt )
                    SELECT @TargetKey, @runnerName, @PathExists, @IsRepo,
                           @Branch, @Upstream, @Ahead, @Behind,
                           @Staged, @Unstaged, @Untracked, @Conflicted, @StashCount,
                           @HeadSha, @HeadSubject, @HeadAuthor, @HeadDt,
                           @RemoteUrl, @DirtyFiles, @ProbeError,
                           now(), NULL, now()
                     WHERE EXISTS ( SELECT 1 FROM projmng.ai_target
                                     WHERE target_key = @TargetKey AND is_deleted = false )
                    ON CONFLICT (target_key) DO UPDATE
                       SET runner_nm    = EXCLUDED.runner_nm,
                           path_exists  = EXCLUDED.path_exists,
                           is_repo      = EXCLUDED.is_repo,
                           branch       = EXCLUDED.branch,
                           upstream     = EXCLUDED.upstream,
                           ahead        = EXCLUDED.ahead,
                           behind       = EXCLUDED.behind,
                           staged       = EXCLUDED.staged,
                           unstaged     = EXCLUDED.unstaged,
                           untracked    = EXCLUDED.untracked,
                           conflicted   = EXCLUDED.conflicted,
                           stash_count  = EXCLUDED.stash_count,
                           head_sha     = EXCLUDED.head_sha,
                           head_subject = EXCLUDED.head_subject,
                           head_author  = EXCLUDED.head_author,
                           head_dt      = EXCLUDED.head_dt,
                           remote_url   = EXCLUDED.remote_url,
                           dirty_files  = EXCLUDED.dirty_files,
                           probe_error  = EXCLUDED.probe_error,
                           probed_at    = now(),
                           probe_req_dt = NULL,
                           mod_dt       = now()
                    """, new
                {
                    s.TargetKey, runnerName, s.PathExists, s.IsRepo,
                    Branch = Cut(s.Branch, 200),
                    Upstream = Cut(s.Upstream, 200),
                    s.Ahead, s.Behind,
                    s.Staged, s.Unstaged, s.Untracked, s.Conflicted, s.StashCount,
                    HeadSha = Cut(s.HeadSha, 64),
                    HeadSubject = Cut(s.HeadSubject, 500),
                    HeadAuthor = Cut(s.HeadAuthor, 200),
                    s.HeadDt,
                    RemoteUrl = Cut(s.RemoteUrl, 500),
                    DirtyFiles = Cut(s.DirtyFiles, 4000),
                    ProbeError = Cut(s.ProbeError, 2000),
                });
            }
            catch (PostgresException)
            {
                // 그 대상이 방금 지워졌다. 다음 바퀴에는 목록에서도 빠진다.
            }
        }

        return done;
    }

    /// <summary>
    /// 「지금 확인」. <b>표시만 하고 끝난다</b> — 실제로 보는 것은 실행기다.
    /// </summary>
    /// <returns>
    /// 표시를 남겼으면 참. 없는 대상이면 거짓이다.
    /// </returns>
    /// <remarks>
    /// 즉시 답을 줄 수 없는 동작이라 화면이 그렇게 말해야 한다 — 실행기가
    /// 다음 바퀴에 이것을 보고 온다.
    /// </remarks>
    public async Task<bool> RequestProbeAsync(long targetKey)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync("""
            INSERT INTO projmng.ai_target_status (target_key, probe_req_dt, cre_dt)
            SELECT @targetKey, now(), now()
             WHERE EXISTS ( SELECT 1 FROM projmng.ai_target
                             WHERE target_key = @targetKey AND is_deleted = false )
            ON CONFLICT (target_key) DO UPDATE
               SET probe_req_dt = now(), mod_dt = now()
            """, new { targetKey });

        return affected > 0;
    }

    /// <summary>
    /// 칸 길이에 맞춰 자른다.
    /// </summary>
    /// <remarks>
    /// <b>자르지 않으면 보고 한 건이 통째로 실패한다.</b> 커밋 제목은 길이
    /// 제한이 없고, 더러운 파일 목록은 수천 줄이 될 수 있다. 실행기 쪽에서도
    /// 한 번 자르지만 여기가 DB 바로 앞이라 여기가 마지막 관문이다.
    /// </remarks>
    private static string? Cut(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null
        : text.Length <= max ? text
        : text[..max];
}
