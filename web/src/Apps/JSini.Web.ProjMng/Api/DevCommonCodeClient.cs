using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트관리 공통코드(포털 CommonCodes 와 다른 표). 읽기는 참조자료 통에 나눠 담고, 고치면 그 자리에서 통을 버린다.
/// 안 버리면 고친 사람도 옛 목록을 보고 저장이 안 된 줄 안다.
/// </summary>
public sealed class DevCommonCodeClient(GatewayClient gateway, ReferenceData data)
{
    private const string Url = "projmng/dev-common-codes";

    /// <summary>참조자료 통 안에서의 묶음 이름. 고치는 화면이 이 이름으로 버린다.</summary>
    public const string Group = "projmng.dev-common-code";

    /// <summary>묶음만(상위 코드가 빈 줄).</summary>
    public async Task<IReadOnlyList<DevCommonCodeDto>> GroupsAsync(CancellationToken ct = default)
        => await data.SharedAsync<IReadOnlyList<DevCommonCodeDto>>(Group, "groups",
            () => LoadAsync($"{Url}?groupsOnly=true", ct)) ?? [];

    /// <summary>그 묶음에 속한 코드.</summary>
    public async Task<IReadOnlyList<DevCommonCodeDto>> ChildrenAsync(
        string parentCode, CancellationToken ct = default)
        => await data.SharedAsync<IReadOnlyList<DevCommonCodeDto>>(Group, $"children/{parentCode}",
            () => LoadAsync($"{Url}?parentCode={Uri.EscapeDataString(parentCode)}", ct)) ?? [];

    /// <summary>실제로 읽는다. ApiException 은 여기서 삼키지 않는다 — 삼키면 서버가 죽은 것이 「자료 없음」으로 보인다.</summary>
    private async Task<IReadOnlyList<DevCommonCodeDto>?> LoadAsync(string url, CancellationToken ct)
        => await gateway.GetListAsync<DevCommonCodeDto>(url, ct);

    public async Task<DevCommonCodeDto?> CreateAsync(DevCommonCodeDto item, CancellationToken ct = default)
    {
        var created = await gateway.PostAsync<DevCommonCodeDto>(Url, item, ct);
        data.Invalidate(Group);
        return created;
    }

    public async Task<DevCommonCodeDto?> UpdateAsync(DevCommonCodeDto item, CancellationToken ct = default)
    {
        var updated = await gateway.PutAsync<DevCommonCodeDto>($"{Url}/{item.CmRid}", item, ct);
        data.Invalidate(Group);
        return updated;
    }

    /// <summary>지운다. 묶음이면 code 를 함께 보내 딸린 코드가 있으면 서버가 막는다.</summary>
    public async Task DeleteAsync(int cmRid, string? code = null, CancellationToken ct = default)
    {
        await gateway.DeleteAsync(
            $"{Url}/{cmRid}" + (string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : $"?code={Uri.EscapeDataString(code)}"), ct);

        data.Invalidate(Group);
    }
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
