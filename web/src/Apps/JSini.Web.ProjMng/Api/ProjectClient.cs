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

    /// <summary>
    /// 이 프로젝트가 쓰는 RAG 카테고리 열쇠. 이름은 안 온다 — 화면이
    /// <see cref="RagCategoryClient"/> 로 따로 읽어 맞춘다.
    /// </summary>
    /// 저장할 때 <b>여기 담긴 것이 곧 전부</b>다 — 비워 보내면 전부 풀린다.
    /// <c>null</c> 이면 서버가 연결을 손대지 않는다(카테고리 목록을 못 읽었을 때 쓴다).
    public List<string>? RagCateIds { get; set; }

    /// <summary>편집 폼의 카테고리 고르개가 묶이는 자리. <see cref="RagCateIds"/> 와 같은 값이다.
    /// DevExpress 편집기는 팝업 안에서 순수한 멤버 접근인 검증 표현식을 요구해서, 형을 맞춘 창을 따로 둔다.</summary>
    public IEnumerable<string> RagCates
    {
        get => RagCateIds ?? [];
        set => RagCateIds = [.. value];
    }

    /// <summary>표에 한 칸으로 보여 줄 카테고리 이름들. <b>서버가 아니라 화면이 채운다</b> —
    /// 이름은 AI 서버에 있고 여기로는 열쇠만 오기 때문이다.</summary>
    public string RagCateText { get; set; } = string.Empty;
}
