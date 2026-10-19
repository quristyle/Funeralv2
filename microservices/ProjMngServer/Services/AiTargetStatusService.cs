using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>AI 작업 대상의 git 상태 — <c>projmng.ai_target_status</c>.</summary>
/// 대상 경로는 호스트 것이라 git 을 부르지 않고, 실행기가 적어 둔 값을 받아 화면에 내준다. 하는 일은 읽기 · 쓰기(실행기) · 지금 봐 달라 표시 셋뿐이다.
public sealed class AiTargetStatusService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>화면이 읽는 목록. 바깥 조인이라 아직 안 본 대상도 보여서 실행기가 안 떠 있음이 드러난다.</summary>
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

    /// <summary>실행기가 물어보는 목록 — 이 장비가 볼 수 있는 대상. 집어가기와 같은 규칙이다.</summary>
    /// 다른 장비의 경로를 들여다보면 「경로가 없습니다」가 거짓으로 쌓이므로 장비가 맞는 것만 준다.
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

    /// <summary>실행기가 보고한 스냅샷을 넣는다. 대상당 한 줄이라 덮어쓴다.</summary>
    /// 본 시각은 서버가 찍는다 — 실행기 시계가 어긋나도 화면의 「몇 분 전」이 틀리지 않게 한다.
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

    /// <summary>「지금 확인」. 표시만 남기고 끝난다 — 실제로 보는 것은 실행기다.</summary>
    /// <returns>표시를 남겼으면 참, 없는 대상이면 거짓.</returns>
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

    /// <summary>칸 길이에 맞춰 자른다.</summary>
    /// 자르지 않으면 보고 한 건이 통째로 실패한다 — 여기가 DB 앞의 마지막 관문이다.
    private static string? Cut(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null
        : text.Length <= max ? text
        : text[..max];
}
