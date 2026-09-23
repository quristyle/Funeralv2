using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 사용자별 화면 설정 — <c>projmng.wbs_user_pref</c>.
/// </summary>
/// <remarks>
/// <para>
/// [개발자 명부는 여기 없다]
/// </para>
///
/// <para>
/// 이 서비스가 <c>projmng.wbs_user</c>(사번 · 직급 · 장비 대장 · 계정 발급
/// 현황)도 다루고 있었다. 2026-09-23 에 그 속성들을 <b>포털 계정</b>으로
/// 옮기고(<c>scom.account_profile_details</c> 의 <c>Dev.*</c>) 명부와 그
/// 화면을 걷어냈다 — 같은 사람이 두 곳에 있고 어긋나면 어느 쪽이 맞는지
/// 알 방법이 없었다. 경위는 <c>docs/projmng-account-merge.md</c>.
/// </para>
///
/// <para>
/// [누구인지를 가리는 방법도 바뀌었다]
/// </para>
///
/// <para>
/// 원본은 <b>접속 IP</b> 로 사람을 가려냈다(<c>IpGate.cs</c> — <c>use_ip</c> 와
/// 대조). 사내망 전용이라 그것이 인증이기도 했다. 포털 안에서는 게이트웨이가
/// 로그인 계정을 <c>X-User-Id</c> 로 붙여 주므로 그 길을 통째로 걷어냈다.
/// </para>
/// </remarks>
public sealed class WbsBoardUserService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

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
    /// 설정 주인. <b>로그인 아이디가 곧 열쇠다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한동안 이 자리가 <c>login_id → bp_id</c> 조회였다 — 명부에 이어진 줄이
    /// 있으면 사번을 열쇠로 삼았다. 명부를 걷어내면서 그 단계가 사라졌다.
    /// </para>
    ///
    /// <para>
    /// <b>이미 사번으로 담긴 설정은 그 사람에게 안 보인다.</b> 화면 설정이라
    /// 다시 고르면 그만이고, 되살리려고 대조표를 두면 명부를 없앤 뜻이 없어진다.
    /// </para>
    /// </remarks>
    public static string ResolveOwner(string loginId) => loginId;

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
