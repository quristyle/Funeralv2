using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트 DB 접속. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 셋이었다 — <c>sp_projdblist</c> · <c>sp_projdbsave</c> ·
/// <c>sp_projdbdel</c>. **비밀번호는 목록에 오지 않는다**(서버가 안 보낸다).
/// 저장할 때 비워 두면 기존 값이 지켜진다.
/// </remarks>
public sealed class ProjectDbClient(GatewayClient gateway)
{
    private const string Url = "projmng/project-dbs";

    public Task<IReadOnlyList<ProjectDbDto>> ListAsync(int? prjRid = null, CancellationToken ct = default)
        => gateway.GetListAsync<ProjectDbDto>(
            Url + (prjRid is null ? string.Empty : $"?prjRid={prjRid}"), ct);

    public Task<ProjectDbDto?> CreateAsync(ProjectDbDto item, CancellationToken ct = default)
        => gateway.PostAsync<ProjectDbDto>(Url, item, ct);

    public Task<ProjectDbDto?> UpdateAsync(ProjectDbDto item, CancellationToken ct = default)
        => gateway.PutAsync<ProjectDbDto>($"{Url}/{item.DbRid}", item, ct);

    public Task DeleteAsync(int dbRid, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{dbRid}", ct);

    /// <summary>
    /// 끌어 옮긴 차례를 저장한다. <paramref name="dbRids"/> 에는
    /// <b>화면에 보이는 줄 전부를, 보이는 차례대로</b> 담는다.
    /// </summary>
    /// <remarks>
    /// 옮긴 줄 하나만 보내지 않는다 — 서버가 「그 줄들이 지금 차지한 자리」를
    /// 모아 다시 나눠 주는 방식이라, 전부를 알아야 자리 수가 맞는다.
    /// </remarks>
    public Task ReorderAsync(IReadOnlyList<int> dbRids, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/order", dbRids, ct);

    // ── 접속에 딸린 속성 ──────────────────────────────────

    public Task<IReadOnlyList<ProjectDbPropDto>> PropsAsync(int dbRid, CancellationToken ct = default)
        => gateway.GetListAsync<ProjectDbPropDto>($"{Url}/{dbRid}/props", ct);

    public Task<ProjectDbPropDto?> CreatePropAsync(
        int dbRid, ProjectDbPropDto item, CancellationToken ct = default)
        => gateway.PostAsync<ProjectDbPropDto>($"{Url}/{dbRid}/props", item, ct);

    public Task<ProjectDbPropDto?> UpdatePropAsync(
        int dbRid, ProjectDbPropDto item, CancellationToken ct = default)
        => gateway.PutAsync<ProjectDbPropDto>($"{Url}/{dbRid}/props/{item.DbPrid}", item, ct);

    public Task DeletePropAsync(int dbRid, int dbPrid, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{dbRid}/props/{dbPrid}", ct);
}

/// <summary>DB 접속에 딸린 속성 한 줄.</summary>
public sealed class ProjectDbPropDto
{
    public int DbPrid { get; set; }
    public int DbRid { get; set; }

    public string? DbPkey { get; set; }
    public string? DbPvalue { get; set; }

    /// <summary><b>옛 저장이 다루지 않던 칸 둘</b>이다.</summary>
    public string? DbPcomment { get; set; }

    /// <inheritdoc cref="DbPcomment"/>
    public string? DbPtype { get; set; }

    public DateTime? ModDt { get; set; }
    public DateTime? CreDt { get; set; }
}

/// <summary>DB 접속 한 건.</summary>
public sealed class ProjectDbDto
{
    public int DbRid { get; set; }
    public int? PrjRid { get; set; }

    /// <summary>조인해서 오는 값. <b>고칠 수 없다.</b></summary>
    public string? PrjName { get; set; }

    /// <inheritdoc cref="PrjName"/>
    public string? PrjNick { get; set; }

    public string? DbNick { get; set; }
    public string? DbType { get; set; }
    public string? DbIp { get; set; }
    public string? DbPort { get; set; }
    public string? DbDatabase { get; set; }
    public string? DbSchema { get; set; }
    public string? DbId { get; set; }

    /// <summary>
    /// 비밀번호. <b>목록에는 오지 않는다</b>(늘 <c>null</c>).
    /// 비워서 저장하면 기존 값이 그대로 남는다.
    /// </summary>
    public string? DbPwd { get; set; }

    public string? DbCert { get; set; }

    /// <summary><b>옛 저장이 다루지 않던 칸</b>이다.</summary>
    public string? DbComm { get; set; }

    /// <summary>
    /// 보여 줄 차례. <b>작을수록 먼저</b>이고, 비면 맨 뒤로 간다.
    /// 이 표를 읽는 자리 셋(목록 화면 · 고르개 둘)이 모두 이 값을 먼저 본다.
    ///
    /// <para>
    /// 대개 표에서 끌어 옮겨 정하고, 편집 창에서 숫자로 고칠 수도 있다.
    /// <b>비워 저장하면 기존 값이 남는다</b> — 비밀번호와 같은 규칙이다.
    /// </para>
    /// </summary>
    public int? DbSrt { get; set; }
}
