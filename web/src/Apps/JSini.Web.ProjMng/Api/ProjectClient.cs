using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트 목록. <b>프로시저를 부르지 않는다</b> — 평범한 REST 다.
/// </summary>
/// <remarks>
/// <para>
/// 옛 길은 <c>ProjMngClient.DbContAsync("sp_dev_proj_exec", …)</c> 였고,
/// 돌아오는 것은 칸 메타와 <c>DataTable</c> 이었다. 그래서 화면이
/// <c>DynamicGrid</c> 를 써야 했다 — 칸을 미리 알 수 없으니 그릴 방법이
/// 그것뿐이었다.
/// </para>
///
/// <para>
/// 지금은 타입 있는 목록이 온다. 그래서 화면이 <c>CommGrd</c> 를 쓴다 —
/// 나머지 예순 화면과 같은 부품, 같은 아래 띠, 같은 편집 창이다.
/// </para>
/// </remarks>
public sealed class ProjectClient(GatewayClient gateway)
{
    private const string Url = "projmng/projects";

    /// <summary>전체 목록.</summary>
    public Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default)
        => gateway.GetListAsync<ProjectDto>(Url, ct);

    /// <summary>새 프로젝트. 번호와 일시는 서버가 정한다.</summary>
    public Task<ProjectDto?> CreateAsync(ProjectDto item, CancellationToken ct = default)
        => gateway.PostAsync<ProjectDto>(Url, item, ct);

    /// <summary>고친다.</summary>
    public Task<ProjectDto?> UpdateAsync(ProjectDto item, CancellationToken ct = default)
        => gateway.PutAsync<ProjectDto>($"{Url}/{item.PrjRid}", item, ct);
}

/// <summary>
/// 프로젝트 한 건. 서버의 <c>ProjMngServer.Models.Project</c> 와 짝이다.
/// </summary>
/// <remarks>
/// <para>
/// 칸 이름을 DB 그대로(<c>PrjRid</c> · <c>PrjSdt</c>) 둔 이유는, 이 표를 보는
/// 사람이 프로시저와 표를 함께 보는 개발자이기 때문이다. 화면에 나가는 이름은
/// 그리드의 <c>Caption</c> 이 정한다.
/// </para>
/// </remarks>
public sealed class ProjectDto
{
    public int PrjRid { get; set; }

    public string? PrjName { get; set; }
    public string? PrjDesc { get; set; }

    /// <summary>시작일. DB 가 <c>date</c> 라 시각이 없다.</summary>
    public DateOnly? PrjSdt { get; set; }

    public DateOnly? PrjEdt { get; set; }

    /// <summary>별칭. 다른 화면의 프로젝트 드롭다운에 이 이름이 나온다.</summary>
    public string? PrjNick { get; set; }

    public string? PrjType { get; set; }

    public int? ProjPay { get; set; }
    public int? PrjUsePay { get; set; }

    /// <summary>정렬 순서. 비우면 서버가 기본값(99999)을 넣는다.</summary>
    public int? PrjSrt { get; set; }

    /// <summary>서버가 채운다. 화면은 보여 주기만 한다.</summary>
    public DateTime? ModDt { get; set; }

    /// <summary>서버가 등록할 때만 채운다 — 고쳐도 안 바뀐다.</summary>
    public DateTime? CreDt { get; set; }
}
