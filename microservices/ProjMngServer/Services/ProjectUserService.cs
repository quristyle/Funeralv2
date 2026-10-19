using System.Data;
using System.Globalization;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>프로젝트 참여자(<c>projmng.dev_proj_user_map</c>). <b>프로시저를 쓰지 않는다.</b></summary>
public sealed class ProjectUserService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>「이 사람이 어느 프로젝트에 들어가 있나」 — 프로젝트 전부에 참여 여부를 붙여서.</summary>
    /// <param name="userId">비우면 모든 줄이 <c>Accepted = false</c> 로 온다(옛 동작).</param>
    /// <param name="prjRid">프로젝트 하나로 좁힌다.</param>
    public async Task<List<ProjectAssignment>> AssignmentsAsync(string? userId, int? prjRid = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<ProjectAssignment>("""
            SELECT (m.user_id IS NOT NULL) AS Accepted,
                   p.prj_rid   AS PrjRid,
                   p.prj_name  AS PrjName,
                   p.prj_desc  AS PrjDesc,
                   p.prj_sdt   AS PrjSdt,
                   p.prj_edt   AS PrjEdt
              FROM projmng.dev_proj p
              LEFT JOIN projmng.dev_proj_user_map m
                     ON m.prj_rid = p.prj_rid
                    AND @userId <> ''
                    AND m.user_id = @userId
             WHERE (@prjRid IS NULL OR p.prj_rid = @prjRid)
             ORDER BY p.prj_rid
            """, new { userId = userId ?? string.Empty, prjRid });

        return [.. rows];
    }

    /// <summary>참여 한 줄을 넣는다. 한 줄짜리와 일괄이 같은 글자를 써야 한다 — 갈라 두면 어떤 경로로 넣었느냐에 따라 결과가 달라진다.</summary>
    private const string InsertSql = """
        INSERT INTO projmng.dev_proj_user_map (prj_rid, user_id)
        VALUES (@prjRid, @userId)
        ON CONFLICT (prj_rid, user_id) DO NOTHING
        """;

    private const string DeleteSql = """
        DELETE FROM projmng.dev_proj_user_map
         WHERE prj_rid = @prjRid AND user_id = @userId
        """;

    /// <summary>참여를 한 번에 여러 건 켜고 끈다. 한 트랜잭션이라 다 되거나 하나도 안 된다 — 열 건 중 셋에서 끊기면 화면과 DB 가 어긋난다.</summary>
    public async Task<(int Added, int Removed)> SetAssignmentsAsync(
        int? prjRid,
        string? userId,
        IReadOnlyList<string> add,
        IReadOnlyList<string> remove)
    {
        var adds = Pairs(prjRid, userId, add);
        var removes = Pairs(prjRid, userId, remove);

        if (adds.Count == 0 && removes.Count == 0)
        {
            return (0, 0);
        }

        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();

        // 같은 짝이 빼기와 넣기 양쪽에 있으면 넣기가 이긴다 — 사라지는 쪽보다 남는 쪽이 눈에 띄고 되돌리기도 쉽다.
        var removed = 0;

        foreach (var pair in removes)
        {
            removed += await db.ExecuteAsync(new CommandDefinition(
                DeleteSql, new { prjRid = pair.Rid, userId = pair.UserId }, tx));
        }

        var added = 0;

        foreach (var pair in adds)
        {
            added += await db.ExecuteAsync(new CommandDefinition(
                InsertSql, new { prjRid = pair.Rid, userId = pair.UserId }, tx));
        }

        await tx.CommitAsync();

        return (added, removed);
    }

    /// <summary>기준 하나와 상대 열쇠 목록을 짝으로 편다. 못 읽는 번호는 버린다 — 던지면 그 하나 때문에 나머지가 안 들어간다.</summary>
    private static List<(int Rid, string UserId)> Pairs(
        int? prjRid, string? userId, IReadOnlyList<string> others)
    {
        var pairs = new List<(int, string)>(others.Count);

        foreach (var other in others)
        {
            if (string.IsNullOrWhiteSpace(other))
            {
                continue;
            }

            if (prjRid is int rid)
            {
                pairs.Add((rid, other));
            }
            else if (int.TryParse(other, NumberStyles.Integer, CultureInfo.InvariantCulture, out var otherRid))
            {
                pairs.Add((otherRid, userId!));
            }
        }

        return pairs;
    }

    /// <summary>걸려 있는 짝의 목록. 사람마다 참여 프로젝트 수를 함께 세는데, 그 수는 거르기와 무관한 전체 기준이다.</summary>
    public async Task<List<ProjectUserRow>> ListAsync(int? prjRid, string? userId)
    {
        using var db = Open();

        var rows = await db.QueryAsync<ProjectUserRow>("""
            SELECT m.user_id  AS UserId,
                   t.inv_cnt  AS InvCnt,
                   m.prj_rid  AS PrjRid,
                   p.prj_name AS PrjName
              FROM projmng.dev_proj_user_map m
              LEFT JOIN projmng.dev_proj p ON p.prj_rid = m.prj_rid
              JOIN ( SELECT user_id, count(*)::int AS inv_cnt
                       FROM projmng.dev_proj_user_map
                      GROUP BY user_id ) t
                ON t.user_id = m.user_id
             WHERE (@prjRid IS NULL OR m.prj_rid = @prjRid)
               AND (@userId = '' OR m.user_id = @userId)
             ORDER BY m.user_id, m.prj_rid
            """, new { prjRid, userId = userId ?? string.Empty });

        return [.. rows];
    }
}
