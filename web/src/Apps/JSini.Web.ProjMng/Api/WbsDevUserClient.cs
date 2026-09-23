using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// WBS 대시보드의 개발자 명부와 화면 설정 —
/// <c>projmng/wbs-board/dev-users</c> · <c>…/prefs</c>.
/// </summary>
/// <remarks>
/// <para>
/// 원장의 담당자 칸이 <see cref="WbsDevUserDto.BpId"/> 를 가리킨다. <b>이 명부가
/// 비면 대시보드의 사람 이름이 전부 사번으로 보인다.</b>
/// </para>
///
/// <para>
/// 설정의 주인은 서버가 로그인 계정으로 가린다 — 화면이 누구인지 보내지 않는다.
/// </para>
/// </remarks>
public sealed class WbsDevUserClient(GatewayClient gateway)
{
    private const string Url = "projmng/wbs-board/dev-users";
    private const string PrefUrl = "projmng/wbs-board/prefs";

    public Task<IReadOnlyList<WbsDevUserDto>> ListAsync(int prjRid, CancellationToken ct = default)
        => gateway.GetListAsync<WbsDevUserDto>($"{Url}?prjRid={prjRid}", ct);

    public Task CreateAsync(int prjRid, WbsDevUserDto item, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}?prjRid={prjRid}", item, ct);

    public Task UpdateAsync(int prjRid, WbsDevUserDto item, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{Uri.EscapeDataString(item.BpId ?? string.Empty)}?prjRid={prjRid}", item, ct);

    public Task DeleteAsync(int prjRid, string bpId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{Uri.EscapeDataString(bpId)}?prjRid={prjRid}", ct);

    // ──────────────────────────────────────────── 화면 설정

    public Task<WbsPrefDto?> GetPrefAsync(int prjRid, string key, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsPrefDto>($"{PrefUrl}/{Uri.EscapeDataString(key)}?prjRid={prjRid}", ct);

    /// <summary><paramref name="value"/> 에 <c>null</c> 을 주면 지운다.</summary>
    public Task SetPrefAsync(int prjRid, string key, object? value, CancellationToken ct = default)
        => gateway.PutAsync($"{PrefUrl}/{Uri.EscapeDataString(key)}?prjRid={prjRid}", value, ct);
}

/// <summary>
/// 개발자 명부 한 줄.
/// </summary>
/// <remarks>
/// 명부이면서 <b>장비 대장</b>이다 — 노트북·모니터 세 대의 장비번호와 확인번호,
/// MAC, 옷 치수까지 같은 줄에 있다. 포털 계정에 그런 칸이 없어서 표를 통째로
/// 계정 쪽에 넘기지 못했고, 대신 <see cref="LoginId"/> 하나로 잇는다.
/// </remarks>
public sealed class WbsDevUserDto
{
    /// <summary>사번. 프로젝트 안에서 열쇠라 <b>등록한 뒤에는 못 바꾼다</b>.</summary>
    public string? BpId { get; set; }

    /// <summary>포털 계정. 채우면 화면이 포털에서 이름과 얼굴을 가져온다.</summary>
    public string? LoginId { get; set; }

    public string? Name { get; set; }
    public string? PositionNm { get; set; }
    public string? Email { get; set; }
    public string? TelNo { get; set; }
    public string? EmergTelNo { get; set; }
    public string? BirthDt { get; set; }

    public string? Git { get; set; }
    public string? Startkit { get; set; }
    public string? Dxb { get; set; }
    public string? VmConn { get; set; }
    public string? Aipro { get; set; }
    public string? Claudecode { get; set; }
    public string? DevDb { get; set; }
    public string? Wiki { get; set; }
    public string? Projectview { get; set; }
    public string? Svn { get; set; }

    /// <summary>ProjectView 사용자 id(<c>USR-…</c>). 워크플로 담당자를 이 값으로 찾는다.</summary>
    public string? PvUserId { get; set; }

    public string? Notebook { get; set; }
    public string? HubHdmi { get; set; }
    public string? SummerSize { get; set; }
    public string? WinterSize { get; set; }
    public string? MacAddr { get; set; }
    public string? NotebookNo { get; set; }
    public string? NotebookChkNo { get; set; }
    public string? Monitor1No { get; set; }
    public string? Monitor1ChkNo { get; set; }
    public string? Monitor2No { get; set; }
    public string? Monitor2ChkNo { get; set; }
    public string? Monitor3No { get; set; }
    public string? Monitor3ChkNo { get; set; }

    /// <summary>
    /// 사용 IP. 사내에서는 <b>이것이 인증 전부</b>였다. 포털 안에서는
    /// 장비 대장으로만 쓴다 — 권한을 가리지 않는다.
    /// </summary>
    public string? UseIp { get; set; }

    /// <inheritdoc cref="UseIp"/>
    public string? SuperYn { get; set; }

    /// <inheritdoc cref="UseIp"/>
    public string? BlockYn { get; set; }
}

/// <summary>사용자별 화면 설정 한 건.</summary>
public sealed class WbsPrefDto
{
    public string? Key { get; set; }

    /// <summary>담아 둔 값. JSON 글자 그대로다.</summary>
    public string? Value { get; set; }

    /// <summary>누구 몫으로 담겼나.</summary>
    public string? Who { get; set; }
}
