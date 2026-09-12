using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 시스템 질의. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 <c>sp_devsqlresp_base_exec</c> 였고, 그것은 <b>이름표만</b> 다뤘다.
/// 실제 SQL 을 담은 표는 어느 화면에서도 열 수 없었다.
/// </remarks>
public sealed class DbLogicClient(GatewayClient gateway)
{
    private const string Url = "projmng/db-logics";

    /// <summary>이름표 목록. 딸린 질의 수가 함께 온다.</summary>
    public Task<IReadOnlyList<DbLogicBaseDto>> ListAsync(CancellationToken ct = default)
        => gateway.GetListAsync<DbLogicBaseDto>(Url, ct);

    /// <summary>이름표를 넣거나 고친다. <b>이름이 곧 열쇠다.</b></summary>
    public Task<DbLogicBaseDto?> SaveAsync(DbLogicBaseDto item, CancellationToken ct = default)
        => gateway.PostAsync<DbLogicBaseDto>(Url, item, ct);

    public Task DeleteAsync(string dslCd, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{Uri.EscapeDataString(dslCd)}", ct);

    /// <summary>이름표에 딸린 DB 종류별 질의.</summary>
    public Task<IReadOnlyList<DbLogicQueryDto>> QueriesAsync(string dslCd, CancellationToken ct = default)
        => gateway.GetListAsync<DbLogicQueryDto>($"{Url}/{Uri.EscapeDataString(dslCd)}/queries", ct);

    public Task<DbLogicQueryDto?> CreateQueryAsync(
        string dslCd, DbLogicQueryDto item, CancellationToken ct = default)
        => gateway.PostAsync<DbLogicQueryDto>($"{Url}/{Uri.EscapeDataString(dslCd)}/queries", item, ct);

    public Task<DbLogicQueryDto?> UpdateQueryAsync(
        string dslCd, DbLogicQueryDto item, CancellationToken ct = default)
        => gateway.PutAsync<DbLogicQueryDto>(
            $"{Url}/{Uri.EscapeDataString(dslCd)}/queries/{item.DslId}", item, ct);

    public Task DeleteQueryAsync(string dslCd, long dslId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{Uri.EscapeDataString(dslCd)}/queries/{dslId}");
}

/// <summary>시스템 질의의 이름표. 무엇을 하는 질의인지만 담는다.</summary>
public sealed class DbLogicBaseDto
{
    /// <summary>질의 이름. <b>열쇠다</b> — 화면과 서버가 이 글자로 질의를 찾는다.</summary>
    public string DslCd { get; set; } = string.Empty;

    public string? Comm { get; set; }

    /// <summary>목록 순서. 비우면 999.</summary>
    public int? Sort { get; set; }

    /// <summary>등록된 DB 종류 수. <b>읽기 전용</b>.</summary>
    public int QueryCount { get; set; }
}

/// <summary>DB 종류 하나에 대한 실제 SQL.</summary>
public sealed class DbLogicQueryDto
{
    public long DslId { get; set; }

    /// <summary>DB 종류(<c>POSTGRESQL</c> · <c>MSSQL</c> · <c>MYSQL</c> · <c>EDB</c>).</summary>
    public string? DslType { get; set; }

    public string? DslCd { get; set; }
    public string? DslQuery { get; set; }
    public string? Comm { get; set; }
}
