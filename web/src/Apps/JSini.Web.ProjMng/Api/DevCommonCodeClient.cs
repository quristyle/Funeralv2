using JSini.Web.Components.Data;
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
/// <para>
/// [읽기는 통을 거친다 — <b>모두가 나눠 쓴다</b>]
/// </para>
/// <para>
/// 백엔드(<c>DevCommonCodesController</c>)가 <b>부른 사람을 보지 않는다</b> —
/// 같은 묶음은 누가 물어도 같은 줄이 온다. 그래서
/// <see cref="ReferenceData.SharedAsync"/> 다(<see cref="CommonCodes"/> 가
/// <c>PerUserAsync</c> 인 것과 갈린다 — 그쪽 프로시저는 참여한 프로젝트로
/// 좁힌다).
/// </para>
/// <para>
/// <b>왜 담나.</b> <c>AI_MODEL</c> 묶음이 「빠른 지시」·「AI 작업」·「AI 작업
/// 대상」 세 화면의 <b>AI 고르개를 세우는 값</b>인데, 화면이 열릴 때마다
/// 게이트웨이를 한 번씩 거쳤다. 고르개는 그 목록이 올 때까지 <b>회색</b>이라
/// (<c>Enabled</c> 가 건수를 본다) 그 왕복이 그대로 「고를 수 없는 시간」이다.
/// 몇 달에 한 번 바뀌는 표를 사람 수 × 화면 수만큼 읽을 까닭이 없다.
/// </para>
/// <para>
/// <b>고치면 그 자리에서 버린다</b>(<see cref="CreateAsync"/> ·
/// <see cref="UpdateAsync"/> · <see cref="DeleteAsync"/>). 안 버리면 코드를
/// 고친 사람이 10분 동안 옛 목록을 보고 저장이 안 된 줄 안다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 실제로 읽는다.
    /// </summary>
    /// <remarks>
    /// <b><see cref="ApiException"/> 을 여기서 삼키지 않는다.</b> 통은 예외가
    /// 나가면 아무것도 담지 않으므로(<see cref="ReferenceDataStore.GetOrAddAsync"/>)
    /// 실패한 조회가 굳을 걱정이 없고, 삼키면 코드 관리 화면의 「읽지 못했습니다」가
    /// <b>「조회 결과가 없습니다」로 바뀐다</b> — 서버가 죽은 것이 자료가 없는
    /// 것으로 보이는, 이 저장소가 <c>DataPage</c> 머리말에 적어 둔 바로 그 함정이다.
    /// 고르개 하나가 못 읽었다고 회로를 끊으면 안 되는 자리는 부르는 쪽이
    /// 막는다(<see cref="AiModelCodes.GetAsync"/>).
    /// </remarks>
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

    /// <summary>
    /// 지운다. 묶음이면 <paramref name="code"/> 를 함께 보내 <b>딸린 코드가
    /// 있는지 서버가 막게</b> 한다.
    /// </summary>
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
