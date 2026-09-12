using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 소스 정보와 그 상세. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 <c>sp_dev_srcinfo_exec</c>(뿌리)와 <c>sp_dev_srcinfo_dtl_exec</c>
/// (상세)였다. 상세 주소가 <c>/{srcRid}/details</c> 아래인 것은 <b>상세가 홀로
/// 서지 않기 때문</b>이다 — 늘 어느 소스의 상세다.
/// </remarks>
public sealed class SourceInfoClient(GatewayClient gateway)
{
    private const string Url = "projmng/source-infos";

    /// <summary>소스 뿌리 목록. 프로젝트로 좁힐 수 있다.</summary>
    public Task<IReadOnlyList<SourceInfoDto>> ListAsync(int? prjRid = null, CancellationToken ct = default)
        => gateway.GetListAsync<SourceInfoDto>(
            Url + (prjRid is null ? string.Empty : $"?prjRid={prjRid}"), ct);

    public Task<SourceInfoDto?> CreateAsync(SourceInfoDto item, CancellationToken ct = default)
        => gateway.PostAsync<SourceInfoDto>(Url, item, ct);

    public Task<SourceInfoDto?> UpdateAsync(SourceInfoDto item, CancellationToken ct = default)
        => gateway.PutAsync<SourceInfoDto>($"{Url}/{item.SrcRid}", item, ct);

    /// <summary>그 소스의 상세.</summary>
    public Task<IReadOnlyList<SourceInfoDetailDto>> DetailsAsync(int srcRid, CancellationToken ct = default)
        => gateway.GetListAsync<SourceInfoDetailDto>($"{Url}/{srcRid}/details", ct);

    public Task<SourceInfoDetailDto?> CreateDetailAsync(
        int srcRid, SourceInfoDetailDto item, CancellationToken ct = default)
        => gateway.PostAsync<SourceInfoDetailDto>($"{Url}/{srcRid}/details", item, ct);

    public Task<SourceInfoDetailDto?> UpdateDetailAsync(
        int srcRid, SourceInfoDetailDto item, CancellationToken ct = default)
        => gateway.PutAsync<SourceInfoDetailDto>($"{Url}/{srcRid}/details/{item.SrcDtlRid}", item, ct);

    public Task DeleteDetailAsync(int srcRid, int srcDtlRid, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{srcRid}/details/{srcDtlRid}", ct);
}

/// <summary>소스 뿌리 한 건.</summary>
public sealed class SourceInfoDto
{
    public int SrcRid { get; set; }
    public int? PrjRid { get; set; }

    /// <summary>조인해서 오는 값. <b>고칠 수 없다.</b></summary>
    public string? PrjName { get; set; }

    /// <inheritdoc cref="PrjName"/>
    public string? PrjNick { get; set; }

    public string? SrcOs { get; set; }
    public string? SrcPath { get; set; }
    public string? SrcNick { get; set; }
    public string? SrcType { get; set; }
    public string? SrcLang { get; set; }
    public string? SrcComm { get; set; }
    public string? SrcUiRoot { get; set; }
    public string? PrjNamespace { get; set; }

    /// <summary>상세에서 끌어온 URL 패턴 하나. <b>읽기 전용</b>이다.</summary>
    public string? UrlPattern { get; set; }
}

/// <summary>소스 상세 한 줄.</summary>
public sealed class SourceInfoDetailDto
{
    public int SrcDtlRid { get; set; }
    public int? SrcRid { get; set; }

    public string? SrcExtend { get; set; }

    /// <summary>패턴 묶음. <c>url</c> 이면 위쪽 목록의 URL 패턴으로 올라간다.</summary>
    public string? SrcPatternGrp { get; set; }

    public string? UrlPattern { get; set; }
    public string? SrcPatternComment { get; set; }

    /// <summary><b>옛 화면에서는 고칠 수 없던 칸</b>이다 — update 에 빠져 있었다.</summary>
    public string? SrcPatternNullvalue { get; set; }
}
