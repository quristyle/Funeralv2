using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트관리 공통코드. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 이름에 <c>Dev</c> 가 붙은 것은 <b>포털의 공통코드와 다른 표</b>라서다.
/// 이 모듈에는 <see cref="CommonCodes"/>(포털 코드를 읽어 드롭다운을 채우는
/// 것)가 따로 있고, 그 둘을 섞으면 「코드를 고쳤는데 드롭다운이 안 바뀐다」가
/// 된다.
/// </para>
/// </remarks>
public sealed class DevCommonCodeClient(GatewayClient gateway)
{
    private const string Url = "projmng/dev-common-codes";

    /// <summary>묶음만(상위 코드가 빈 줄).</summary>
    public Task<IReadOnlyList<DevCommonCodeDto>> GroupsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<DevCommonCodeDto>($"{Url}?groupsOnly=true", ct);

    /// <summary>그 묶음에 속한 코드.</summary>
    public Task<IReadOnlyList<DevCommonCodeDto>> ChildrenAsync(string parentCode, CancellationToken ct = default)
        => gateway.GetListAsync<DevCommonCodeDto>(
            $"{Url}?parentCode={Uri.EscapeDataString(parentCode)}", ct);

    public Task<DevCommonCodeDto?> CreateAsync(DevCommonCodeDto item, CancellationToken ct = default)
        => gateway.PostAsync<DevCommonCodeDto>(Url, item, ct);

    public Task<DevCommonCodeDto?> UpdateAsync(DevCommonCodeDto item, CancellationToken ct = default)
        => gateway.PutAsync<DevCommonCodeDto>($"{Url}/{item.CmRid}", item, ct);

    /// <summary>
    /// 지운다. 묶음이면 <paramref name="code"/> 를 함께 보내 <b>딸린 코드가
    /// 있는지 서버가 막게</b> 한다.
    /// </summary>
    public Task DeleteAsync(int cmRid, string? code = null, CancellationToken ct = default)
        => gateway.DeleteAsync(
            $"{Url}/{cmRid}" + (string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : $"?code={Uri.EscapeDataString(code)}"), ct);
}

/// <summary>공통코드 한 건. 묶음과 코드가 같은 모양이다.</summary>
public sealed class DevCommonCodeDto
{
    public int CmRid { get; set; }

    public string? CmCd { get; set; }
    public string? CmNm { get; set; }

    /// <summary>상위 코드. <b>비어 있으면 묶음</b>이다.</summary>
    public string? CmPcd { get; set; }

    public string? CmProp { get; set; }
    public string? CmVal { get; set; }
    public string? CmType { get; set; }
    public string? CmVal2 { get; set; }
    public string? CmVal3 { get; set; }

    /// <summary>정렬 순서. 비우면 서버가 999 를 넣는다.</summary>
    public int? CmSrt { get; set; }

    /// <summary>비고. <b>옛 프로시저가 다루지 않던 칸</b>이다.</summary>
    public string? CmRmk { get; set; }
}
