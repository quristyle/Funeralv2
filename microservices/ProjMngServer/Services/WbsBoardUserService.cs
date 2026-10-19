using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>사용자별 화면 설정 — <c>projmng.wbs_user_pref</c>.</summary>
/// <summary>사용자별 화면 설정(projmng.wbs_user_pref). 개발자 명부는 포털 계정으로 옮겨 여기서는 다루지 않는다.</summary>
/// 사람을 가리는 것은 포털 로그인 계정(X-User-Id)이다 — 사내망 IP 대조는 걷어냈다(docs/projmng-account-merge.md).
public sealed class WbsBoardUserService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    // ──────────────────────────────────────────────────────── 화면 설정

    /// <summary>설정 이름으로 쓸 수 있는 글자인가. 기본키의 일부라 아무거나 받으면 표가 쓰레기로 찬다.</summary>
    public static bool KeyOk(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= 60
        && key.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');

    /// <summary>설정 주인. 로그인 아이디가 곧 열쇠다.</summary>
    /// 이미 사번으로 담긴 설정은 그 사람에게 안 보인다 — 화면 설정을 다시 고르면 되고, 대조표는 두지 않는다.
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
