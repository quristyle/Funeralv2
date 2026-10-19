using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>프로젝트 목록 REST 클라이언트. 옛 프로시저(sp_dev_proj_exec)는 칸 메타와 DataTable 을 돌려줬고, 지금은 타입 있는 목록이 온다.</summary>
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

/// <summary>프로젝트 한 건. 서버의 ProjMngServer.Models.Project 와 짝이다. 칸 이름은 DB 그대로 둔다 — 화면 이름은 그리드 Caption 이 정한다.</summary>
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
