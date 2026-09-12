using System.Data;
using System.Globalization;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 참여자 — <c>projmng.dev_proj_user_map</c>. <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 옛 길은 둘이었다.
/// </para>
///
/// <list type="bullet">
///   <item>
///     <c>sp_dev_proj_user_map_exec</c> — 「이 사람이 어느 프로젝트에 들어가
///     있나」. 프로젝트 <b>전부</b>를 돌려주고 줄마다 참여 여부(<c>accept_proj</c>)를
///     붙인다. 저장은 그 깃발을 켜면 insert, 끄면 delete 다 —
///     그 뜻 그대로 <see cref="SetAssignmentsAsync"/> 안에 있다.
///   </item>
///   <item>
///     <c>sp_proj_user_map_list</c> — 걸려 있는 짝만. 사람마다 참여 프로젝트
///     수를 함께 센다.
///   </item>
/// </list>
///
/// <para>
/// [고친 것 — 표에 제약이 하나도 없다]
/// </para>
///
/// <para>
/// <c>dev_proj_user_map</c> 에는 <b>기본키도 유일 제약도 없다</b>(확인했다).
/// 그런데 옛 프로시저의 insert 는 이미 있는지 보지 않는다 — 같은 사람을 같은
/// 프로젝트에 두 번 켜면 <b>줄이 둘</b>이 되고, 그러면 참여 프로젝트 수가
/// 부풀고 끄기 한 번으로는 다 안 지워진다.
/// </para>
///
/// <para>
/// <b>2026-09-12 에 그 제약을 걸었다</b> — <c>dev_proj_user_map_pk</c>
/// (<c>deploy/sql/projmng-proj-user-map-pk-2026-09-12.sql</c>). 한동안은
/// <c>WHERE NOT EXISTS</c> 로 좁혀만 두었는데, 그것은 확인과 넣기 사이가
/// 벌어져 있어 <b>두 요청이 겹치면 둘 다 넣는다.</b> 참여를 한 번에 여러 건
/// 보내게 되면서(<see cref="SetAssignmentsAsync"/>) 그 틈이 넓어졌다.
/// 지금은 <c>ON CONFLICT DO NOTHING</c> 이라 판정이 DB 안에 있다.
/// </para>
///
/// <para>
/// [저장하는 길이 하나다]
/// </para>
///
/// <para>
/// 한 줄짜리 <c>SetAssignmentAsync</c> 가 따로 있었는데 없앴다. 체크 하나는
/// 원소 하나짜리 묶음이라 <see cref="SetAssignmentsAsync"/> 가 똑같이 처리한다.
/// 갈래를 둘로 두면 <b>한쪽에만 걸리는 버그</b>가 생긴다 — 실제로 넣기가
/// 한쪽은 <c>WHERE NOT EXISTS</c>, 다른 쪽은 <c>ON CONFLICT</c> 로 갈릴 뻔했다.
/// </para>
///
/// <para>
/// [그대로 둔 것]
/// </para>
///
/// <para>
/// 배정 화면이 <b>프로젝트 전부</b>를 돌려받는 것은 그대로다. 화면이 체크박스
/// 목록이라 「안 들어간 프로젝트」도 보여야 켤 수 있다.
/// </para>
/// </remarks>
public sealed class ProjectUserService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// 「이 사람이 어느 프로젝트에 들어가 있나」 — 프로젝트 전부에 참여 여부를 붙여서.
    /// </summary>
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

    /// <summary>
    /// 참여 한 줄을 넣는다. <b>한 줄짜리와 일괄이 같은 글자를 쓴다</b> —
    /// 갈라 두면 한쪽만 고치는 날이 오고, 그 어긋남은 「어떤 경로로 넣었느냐에
    /// 따라 결과가 다르다」로 나온다.
    /// </summary>
    private const string InsertSql = """
        INSERT INTO projmng.dev_proj_user_map (prj_rid, user_id)
        VALUES (@prjRid, @userId)
        ON CONFLICT (prj_rid, user_id) DO NOTHING
        """;

    private const string DeleteSql = """
        DELETE FROM projmng.dev_proj_user_map
         WHERE prj_rid = @prjRid AND user_id = @userId
        """;

    /// <summary>
    /// 참여를 <b>한 번에 여러 건</b> 켜고 끈다. <b>한 트랜잭션이다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 기준이 프로젝트 하나거나 사람 하나고, 상대 목록이 <c>add</c>·<c>remove</c>
    /// 로 온다. 화면의 축이 그대로 넘어온 것이다.
    /// </para>
    ///
    /// <para>
    /// <b>다 되거나 하나도 안 된다.</b> 열 건 중 셋에서 끊기면 화면이 이미
    /// 반영해 둔 것과 DB 가 어긋나고, 어긋난 것을 사용자가 알 방법이 없다 —
    /// 화면은 「됐다」고 말한 뒤다. 한 줄씩 보내던 때는 실패한 그 줄만
    /// 되돌리면 됐지만 묶어 보내면 그 셈이 성립하지 않는다.
    /// </para>
    ///
    /// <para>
    /// 넣기가 <c>ON CONFLICT DO NOTHING</c> 이라 <b>이미 있는 것을 또 넣어도
    /// 안전하다.</b> 같은 요청을 두 번 보내도 결과가 같다 — 화면이 재전송해도
    /// 참여 수가 부풀지 않는다.
    /// </para>
    /// </remarks>
    /// <param name="prjRid">프로젝트 기준일 때 그 프로젝트. 사람 기준이면 <c>null</c>.</param>
    /// <param name="userId">사람 기준일 때 그 사람. 프로젝트 기준이면 <c>null</c>.</param>
    /// <param name="add">넣을 상대의 열쇠들. 프로젝트 기준이면 아이디, 사람 기준이면 프로젝트 번호.</param>
    /// <param name="remove">뺄 상대의 열쇠들.</param>
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

        // **빼기를 먼저 한다.** 같은 짝이 양쪽에 들어 있는 요청(화면에서 켰다
        // 껐다 한 뒤 한 번에 보낸 것)에서 마지막 뜻은 언제나 「켜기」가 아니라
        // 화면이 지금 보여 주는 상태다. 화면은 그 짝을 한쪽에만 담아 보내므로
        // 실제로는 겹치지 않지만, 겹쳐 오더라도 **넣기가 이기게** 둔다 —
        // 사라지는 쪽보다 남는 쪽이 눈에 띄고 되돌리기도 쉽다.
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

    /// <summary>
    /// 기준 하나와 상대 열쇠 목록을 짝으로 편다.
    /// </summary>
    /// <remarks>
    /// 프로젝트 기준이면 상대가 아이디이고, 사람 기준이면 상대가 프로젝트
    /// 번호를 적은 글자다. <b>못 읽는 번호는 버린다</b> — 던지지 않는 이유는
    /// 그 하나 때문에 나머지 아홉이 안 들어가면 사용자가 무엇이 빠졌는지
    /// 알 수 없기 때문이다. 열쇠는 화면이 목록에서 골라 보내는 값이라
    /// 사람이 손으로 적어 넣는 자리가 없다.
    /// </remarks>
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

    /// <summary>
    /// 걸려 있는 짝의 목록. 사람마다 <b>참여 프로젝트 수</b>를 함께 센다.
    /// </summary>
    /// <remarks>
    /// 그 수는 <b>거르기와 무관한 전체 기준</b>이다 — 프로젝트 하나로 좁혀 봐도
    /// 「이 사람은 원래 몇 개에 들어가 있나」가 보여야 한다. 옛 프로시저도
    /// 같은 뜻으로 짰다(하위 질의를 따로 두었다).
    /// </remarks>
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
