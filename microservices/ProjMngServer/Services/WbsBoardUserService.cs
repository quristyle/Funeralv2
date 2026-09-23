using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;
using static ProjMngServer.Services.WbsBoardSql;

namespace ProjMngServer.Services;

/// <summary>
/// 개발자 명부와 사용자별 화면 설정 — <c>projmng.wbs_user</c> · <c>wbs_user_pref</c>.
/// </summary>
/// <remarks>
/// <para>
/// [누구인지를 가리는 방법이 바뀌었다]
/// </para>
///
/// <para>
/// 원본은 <b>접속 IP</b> 로 사람을 가려냈다(<c>IpGate.cs</c> — <c>use_ip</c> 와
/// 대조). 사내망 전용이라 그것이 인증이기도 했다. 포털 안에서는 게이트웨이가
/// 로그인 계정을 <c>X-User-Id</c> 로 붙여 주므로 그 길을 통째로 걷어냈다.
/// </para>
///
/// <para>
/// 그래서 IP 대장(<c>use_ip</c>)과 최고관리자·차단 표시(<c>super_yn</c>·
/// <c>block_yn</c>)는 <b>칸으로만 남아 있다.</b> 지우지 않은 까닭은 사내에서
/// 장비 대장으로도 쓰고 있어서다 — MAC·장비번호와 같은 줄에 있다.
/// </para>
/// </remarks>
public sealed class WbsBoardUserService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// 조회에 쓸 칸 목록. 날짜는 글자로 바꿔 내보낸다 — 화면이 그대로 보여 주고,
    /// 시간대를 한 번 거치면 하루가 밀리는 자리가 있다.
    /// </summary>
    private static string SelectCols() =>
        string.Join("\n                 , ", new[] { "bp_id" }.Concat(UserCols).Select(c =>
            DateCols.Contains(c)
                ? $"{c}::text as {Pascal(c)}"
                : $"{c} as {Pascal(c)}"));

    /// <summary><c>notebook_chk_no</c> → <c>NotebookChkNo</c>.</summary>
    private static string Pascal(string col) =>
        string.Concat(col.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    // ──────────────────────────────────────────────────────── 명부

    public async Task<List<WbsBoardUser>> ListAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardUser>($"""
            select {SelectCols()}
              from projmng.wbs_user
             where prj_rid = @prjRid
             order by name, bp_id
            """, new { prjRid });

        return [.. rows];
    }

    /// <returns>넣은 줄 수. 이미 있으면 <c>0</c>.</returns>
    public async Task<int> CreateAsync(int prjRid, string bpId, IDictionary<string, object?> values)
    {
        var cols = new List<string> { "prj_rid", "bp_id" };
        var phs = new List<string> { "@prjRid", "@bpId" };
        var args = new DynamicParameters();
        args.Add("prjRid", prjRid);
        args.Add("bpId", bpId);

        var i = 0;
        foreach (var (key, raw) in values)
        {
            if (!UserColSet.Contains(key)) continue;

            var col = key.ToLowerInvariant();
            var name = $"p{i++}";
            args.Add(name, raw is string s && string.IsNullOrWhiteSpace(s) ? null : raw);
            cols.Add(col);
            phs.Add($"@{name}{Cast(col)}");
        }

        using var db = Open();

        return await db.ExecuteAsync($"""
            insert into projmng.wbs_user ({string.Join(", ", cols)})
            values ({string.Join(", ", phs)})
            on conflict (prj_rid, bp_id) do nothing
            """, args);
    }

    /// <returns>고친 줄 수. 받을 칸이 없으면 <c>-1</c>.</returns>
    public async Task<int> UpdateAsync(int prjRid, string bpId, IDictionary<string, object?> values)
    {
        var sets = new List<string>();
        var args = new DynamicParameters();
        var i = 0;

        foreach (var (key, raw) in values)
        {
            if (!UserColSet.Contains(key)) continue;

            var col = key.ToLowerInvariant();
            var name = $"p{i++}";
            args.Add(name, raw is string s && string.IsNullOrWhiteSpace(s) ? null : raw);
            sets.Add($"{col} = @{name}{Cast(col)}");
        }

        if (sets.Count == 0) return -1;

        args.Add("prjRid", prjRid);
        args.Add("bpId", bpId);

        using var db = Open();

        return await db.ExecuteAsync($"""
            update projmng.wbs_user
               set {string.Join(", ", sets)}
             where prj_rid = @prjRid and bp_id = @bpId
            """, args);
    }

    public async Task<bool> DeleteAsync(int prjRid, string bpId)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "delete from projmng.wbs_user where prj_rid = @prjRid and bp_id = @bpId",
            new { prjRid, bpId });

        return affected > 0;
    }

    // ──────────────────────────────────────────────────────── 화면 설정

    /// <summary>
    /// 설정 이름으로 쓸 수 있는 글자인가. 이 값이 <b>기본키의 일부</b>라
    /// 아무거나 받으면 표가 쓰레기로 찬다.
    /// </summary>
    public static bool KeyOk(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= 60
        && key.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');

    /// <summary>
    /// 로그인 계정으로 설정 주인을 가린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 명부에 이어진 줄이 있으면 <b>사번</b>이 열쇠다 — 사람이 계정을 갈아도
    /// 사번은 그대로라 설정이 따라간다.
    /// </para>
    ///
    /// <para>
    /// 이어진 줄이 없으면 <b>로그인 아이디를 그대로 쓴다.</b> 원본은 그때
    /// 「저장 안 함」을 돌려주고 화면이 브라우저에만 담았는데, 포털에서는
    /// 명부에 없는 사람이 대부분이라(명부는 그 프로젝트 개발자만 있다)
    /// 그러면 설정이 거의 늘 안 남는다.
    /// </para>
    /// </remarks>
    public async Task<string> ResolveOwnerAsync(int prjRid, string loginId)
    {
        using var db = Open();

        var bpId = await db.ExecuteScalarAsync<string?>("""
            select bp_id from projmng.wbs_user
             where prj_rid = @prjRid and login_id = @loginId
             limit 1
            """, new { prjRid, loginId });

        return string.IsNullOrWhiteSpace(bpId) ? loginId : bpId;
    }

    public async Task<WbsBoardPref> GetPrefAsync(int prjRid, string owner, string key)
    {
        using var db = Open();

        var value = await db.ExecuteScalarAsync<string?>("""
            select pref_val from projmng.wbs_user_pref
             where prj_rid = @prjRid and bp_id = @owner and pref_key = @key
            """, new { prjRid, owner, key });

        return new WbsBoardPref { Key = key, Value = value, Who = owner };
    }

    /// <summary><paramref name="value"/> 가 <c>null</c> 이면 지운다.</summary>
    public async Task SetPrefAsync(int prjRid, string owner, string key, string? value)
    {
        using var db = Open();

        if (value is null)
        {
            await db.ExecuteAsync("""
                delete from projmng.wbs_user_pref
                 where prj_rid = @prjRid and bp_id = @owner and pref_key = @key
                """, new { prjRid, owner, key });
            return;
        }

        await db.ExecuteAsync("""
            insert into projmng.wbs_user_pref (prj_rid, bp_id, pref_key, pref_val, updated_at)
                 values (@prjRid, @owner, @key, @value, now())
            on conflict (prj_rid, bp_id, pref_key)
              do update set pref_val = excluded.pref_val, updated_at = now()
            """, new { prjRid, owner, key, value });
    }
}
