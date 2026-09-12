using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트 속성. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 <c>sp_dev_proj_prop_exec</c> 였고 <b>지우는 갈래가 없었다.</b>
/// </remarks>
public sealed class ProjectPropClient(GatewayClient gateway)
{
    private const string Url = "projmng/project-props";

    /// <summary>목록. 셋 다 비우면 전부 온다.</summary>
    public Task<IReadOnlyList<ProjectPropDto>> ListAsync(
        string? prjRid = null, string? propCd = null, string? propType = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(prjRid)) query.Add($"prjRid={Uri.EscapeDataString(prjRid)}");
        if (!string.IsNullOrWhiteSpace(propCd)) query.Add($"propCd={Uri.EscapeDataString(propCd)}");
        if (!string.IsNullOrWhiteSpace(propType)) query.Add($"propType={Uri.EscapeDataString(propType)}");

        return gateway.GetListAsync<ProjectPropDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    /// <summary>넣거나 고친다. <b>열쇠 셋이 같으면 고친다.</b></summary>
    public Task<ProjectPropDto?> SaveAsync(ProjectPropDto item, CancellationToken ct = default)
        => gateway.PostAsync<ProjectPropDto>(Url, item, ct);

    public Task DeleteAsync(ProjectPropDto item, CancellationToken ct = default)
        => gateway.DeleteAsync(
            $"{Url}?prjRid={Uri.EscapeDataString(item.PrjRid ?? string.Empty)}"
            + $"&propCd={Uri.EscapeDataString(item.PropCd ?? string.Empty)}"
            + $"&propType={Uri.EscapeDataString(item.PropType ?? string.Empty)}", ct);
}

/// <summary>프로젝트에 딸린 속성 한 줄.</summary>
public sealed class ProjectPropDto
{
    /// <summary>프로젝트 번호. <b>글자다</b> — 표가 그렇게 생겼다.</summary>
    public string? PrjRid { get; set; }

    /// <summary>속성 이름. 사람이 붙인 이름이 그대로 열쇠다.</summary>
    public string? PropCd { get; set; }

    /// <summary>속성 값. 도형 JSON 처럼 긴 글이 들어온다.</summary>
    public string? PropVal { get; set; }

    public string? PropComm { get; set; }
    public string? PropUseYn { get; set; }

    /// <summary>속성 갈래(<c>USE_CASE</c> …).</summary>
    public string? PropType { get; set; }
}
