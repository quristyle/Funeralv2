using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>AI 작업 대상(projmng.ai_target). 경로 검사가 이 서비스의 존재 이유이며 그것이 ValidatePath 다.</summary>
/// 설계는 docs/ai-task-runner.md 9.5 절.
public sealed class AiTargetService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    /// <summary>대상이 놓일 수 있는 뿌리. 설정 AiTasks:AllowedRoots 로 바꾼다.</summary>
    /// 뿌리를 한 곳으로 모아 두면 실행기 systemd 유닛의 ReadWritePaths 를 다시 고칠 일이 없다.
    /// AllowedRootsOverride 가 있으면 목록을 통째로 갈아 끼운다. 운영의 appsettings.Local.json 은
    /// 환경변수보다 나중에 실려 이기고 배열을 칸 번호로 덮으므로, 같은 열쇠로는 그 목록을 못 걷어낸다.
    /// 빈 칸은 버린다 — 빈 뿌리는 "/" 로 풀려 모든 경로가 통과한다.
    private readonly string[] _allowedRoots =
        ReadRoots(configuration, "AiTasks:AllowedRootsOverride")
        ?? ReadRoots(configuration, "AiTasks:AllowedRoots")
        ?? ["/home/lee", "/srv/ai-targets"];

    private static string[]? ReadRoots(IConfiguration configuration, string key) =>
        configuration.GetSection(key).Get<string[]>()
            ?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToArray()
        is { Length: > 0 } roots
            ? roots
            : null;

    /// <summary>어떤 경우에도 대상이 될 수 없는 곳.</summary>
    /// 뿌리가 넓어진 뒤로는 이 목록이 뒷단속이 아니라 울타리 본체다.
    private static readonly string[] DeniedPaths =
    [
        // 운영 설정·배포 장치
        "/srv/jsini/config", "/srv/jsini/runner", "/srv/jsini/.env",
        // 시스템
        "/etc", "/root", "/var/run", "/usr", "/boot", "/bin", "/sbin", "/lib",
        // 실행기가 쓰는 자리. 작업공간을 대상으로 등록하면 자기 꼬리를 문다
        "/home/lee/ai-workspaces",
    ];

    /// <summary>숨은 폴더(<c>.</c> 로 시작하는 칸)는 대상이 될 수 없다.</summary>
    /// 이름을 하나씩 막지 않고 규칙 하나로 막는다 — 목록으로 두면 새 도구가 점 폴더를 만들 때마다 구멍이 생긴다.
    private static bool HasHiddenSegment(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(seg => seg.StartsWith('.'));

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string Columns = """
        a.target_key       AS TargetKey,
        a.target_nm        AS TargetNm,
        a.target_kind      AS TargetKind,
        a.target_path      AS TargetPath,
        a.repo_url         AS RepoUrl,
        a.default_ref      AS DefaultRef,
        a.credential_ref   AS CredentialRef,
        a.isolation_mode   AS IsolationMode,
        a.max_size_mb      AS MaxSizeMb,
        a.runner_kinds     AS RunnerKinds,
        a.allow_push       AS AllowPush,
        a.push_ref         AS PushRef,
        a.gate_mode        AS GateMode,
        a.runner_nm        AS RunnerNm,
        a.is_enabled       AS IsEnabled,
        a.running_run_key  AS RunningRunKey,
        a.comments         AS Comments,
        a.cre_id           AS CreId,
        a.cre_dt           AS CreDt,
        a.mod_id           AS ModId,
        a.mod_dt           AS ModDt
        """;

    /// <summary>대상 목록. <paramref name="onlyEnabled"/> 가 참이면 화면에서 고를 수 있는 것만.</summary>
    public async Task<List<AiTarget>> ListAsync(bool onlyEnabled = false, long? targetKey = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTarget>($"""
            SELECT {Columns}
              FROM projmng.ai_target a
             -- 널일 수 있는 파라미터에는 형을 붙인다. 안 붙이면 PostgreSQL 이
             -- 형을 못 정해 42P08 로 끊는다 — **비었을 때만** 난다.
             WHERE a.is_deleted = false
               AND (@targetKey::bigint IS NULL OR a.target_key = @targetKey)
               AND (@onlyEnabled = false OR a.is_enabled = true)
             ORDER BY a.is_enabled DESC, a.target_nm
            """, new { targetKey, onlyEnabled });

        return [.. rows];
    }

    public async Task<AiTarget?> GetAsync(long targetKey)
        => (await ListAsync(targetKey: targetKey)).FirstOrDefault();

    public async Task<AiTarget?> CreateAsync(AiTarget item, string? userId)
    {
        Normalize(item);

        var key = await ExecuteScalarAsync("""
            INSERT INTO projmng.ai_target
                 ( target_nm, target_kind, target_path, repo_url, default_ref,
                   credential_ref, isolation_mode, max_size_mb, runner_kinds,
                   allow_push, push_ref, gate_mode, runner_nm, is_enabled, comments,
                   cre_id, cre_dt )
            VALUES ( @TargetNm, @TargetKind, @TargetPath, @RepoUrl, @DefaultRef,
                     @CredentialRef, @IsolationMode, @MaxSizeMb, @RunnerKinds,
                     @AllowPush, @PushRef, @GateMode, @RunnerNm, @IsEnabled, @Comments,
                     @userId, now() )
            RETURNING target_key
            """, new
        {
            item.TargetNm, item.TargetKind, item.TargetPath, item.RepoUrl, item.DefaultRef,
            item.CredentialRef, item.IsolationMode, item.MaxSizeMb, item.RunnerKinds,
            item.AllowPush, item.PushRef, item.GateMode, item.RunnerNm, item.IsEnabled,
            item.Comments, userId,
        });

        return await GetAsync(key);
    }

    public async Task<AiTarget?> UpdateAsync(long targetKey, AiTarget item, string? userId)
    {
        Normalize(item);

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_target
               SET target_nm      = @TargetNm,
                   target_kind    = @TargetKind,
                   target_path    = @TargetPath,
                   repo_url       = @RepoUrl,
                   default_ref    = @DefaultRef,
                   credential_ref = @CredentialRef,
                   isolation_mode = @IsolationMode,
                   max_size_mb    = @MaxSizeMb,
                   runner_kinds   = @RunnerKinds,
                   allow_push     = @AllowPush,
                   push_ref       = @PushRef,
                   gate_mode      = @GateMode,
                   runner_nm      = @RunnerNm,
                   is_enabled     = @IsEnabled,
                   comments       = @Comments,
                   mod_id         = @userId,
                   mod_dt         = now()
             WHERE target_key = @targetKey
               AND is_deleted = false
            """, new
        {
            targetKey, item.TargetNm, item.TargetKind, item.TargetPath, item.RepoUrl,
            item.DefaultRef, item.CredentialRef, item.IsolationMode, item.MaxSizeMb,
            item.RunnerKinds, item.AllowPush, item.PushRef, item.GateMode,
            item.RunnerNm, item.IsEnabled, item.Comments, userId,
        });

        return affected == 0 ? null : await GetAsync(targetKey);
    }

    /// <summary>지운다. 행은 남긴다 — 그 대상으로 돌린 작업 이력이 가리키는 곳을 잃지 않게 한다.</summary>
    public async Task<bool> DeleteAsync(long targetKey, string? userId)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_target
               SET is_deleted = true, mod_id = @userId, mod_dt = now()
             WHERE target_key = @targetKey AND is_deleted = false
            """, new { targetKey, userId });

        return affected > 0;
    }

    /// <summary>대상으로 삼아도 되는 경로인가. 안 되면 이유를 돌려준다.</summary>
    /// .. 을 푼 뒤에 검사한다. 심볼릭 링크는 여기서 풀지 않는다 — 컨테이너에서 보는 경로는 호스트 것이라 realpath 가 틀린다.
    public string? ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "경로가 필요합니다.";
        }

        var p = path.Trim();

        if (!p.StartsWith('/'))
        {
            return "절대 경로여야 합니다. (예: /srv/ai-targets/내폴더)";
        }

        if (p.Contains('\0') || p.Contains('\n'))
        {
            return "경로에 쓸 수 없는 글자가 있습니다.";
        }

        // `..` 를 풀어 실제로 가리키는 곳을 본다. 이것을 먼저 하지 않으면
        // 아래 뿌리 검사가 통째로 뚫린다.
        var resolved = NormalizePath(p);

        foreach (var denied in DeniedPaths)
        {
            if (IsUnder(resolved, denied))
            {
                return $"이 경로는 대상이 될 수 없습니다: {denied}";
            }
        }

        if (HasHiddenSegment(resolved))
        {
            return "숨은 폴더(. 로 시작하는 이름)는 대상이 될 수 없습니다. "
                 + "설정과 자격이 거기 있습니다.";
        }

        if (!_allowedRoots.Any(root => IsUnder(resolved, NormalizePath(root))))
        {
            return $"허용된 뿌리 아래여야 합니다: {string.Join(" · ", _allowedRoots)}";
        }

        return null;
    }

    /// <summary>화면이 안내에 쓰라고 내려 주는 값.</summary>
    public IReadOnlyList<string> AllowedRoots => _allowedRoots;

    /// <summary><c>.</c> · <c>..</c> · 겹친 <c>/</c> 를 정리한다.</summary>
    private static string NormalizePath(string path)
    {
        var parts = new List<string>();

        foreach (var seg in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (seg)
            {
                case ".":
                    break;

                case "..":
                    if (parts.Count > 0)
                    {
                        parts.RemoveAt(parts.Count - 1);
                    }

                    break;

                default:
                    parts.Add(seg);
                    break;
            }
        }

        return "/" + string.Join('/', parts);
    }

    /// <summary>path 가 root 아래(또는 자신)인가. 구분자까지 보아 접두사 착오를 막는다.</summary>
    private static bool IsUnder(string path, string root)
    {
        if (path.Equals(root, StringComparison.Ordinal))
        {
            return true;
        }

        var prefix = root.EndsWith('/') ? root : root + "/";
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>빈 값을 기본값으로 채우고 앞뒤 공백을 턴다.</summary>
    private static void Normalize(AiTarget item)
    {
        item.TargetNm = item.TargetNm?.Trim();
        item.RunnerNm = string.IsNullOrWhiteSpace(item.RunnerNm) ? null : item.RunnerNm.Trim();
        item.TargetPath = item.TargetPath?.Trim();
        item.RepoUrl = string.IsNullOrWhiteSpace(item.RepoUrl) ? null : item.RepoUrl.Trim();

        if (!AiTargetKind.IsValid(item.TargetKind))
        {
            item.TargetKind = AiTargetKind.Repo;
        }

        if (!AiTargetIsolation.IsValid(item.IsolationMode))
        {
            // 종류에 맞는 기본값으로 떨어뜨린다. 폴더에 worktree 는 있을 수 없다.
            item.IsolationMode = item.TargetKind == AiTargetKind.Folder
                ? AiTargetIsolation.Copy
                : AiTargetIsolation.Worktree;
        }

        if (string.IsNullOrWhiteSpace(item.RunnerKinds))
        {
            item.RunnerKinds = "claude";
        }

        if (string.IsNullOrWhiteSpace(item.DefaultRef))
        {
            item.DefaultRef = "main";
        }

        if (string.IsNullOrWhiteSpace(item.GateMode))
        {
            item.GateMode = "build";
        }
    }

    private async Task<long> ExecuteScalarAsync(string sql, object param)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<long>(sql, param);
    }
}
