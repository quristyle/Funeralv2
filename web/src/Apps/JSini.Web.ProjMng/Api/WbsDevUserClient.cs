using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// WBS 대시보드의 사용자별 화면 설정 — <c>projmng/wbs-board/prefs</c>.
/// </summary>
/// <remarks>
/// <para>
/// [개발자 명부는 여기 없다]
/// </para>
///
/// <para>
/// 이 클라이언트가 <c>…/dev-users</c>(사번 · 직급 · 장비 대장 · 계정 발급
/// 현황)도 불렀다. 2026-09-23 에 그 속성들이 <b>포털 계정</b>으로 옮겨 가고
/// (계정관리의 접는 구역 넷) 명부와 그 화면이 없어졌다 — 같은 사람이 두 곳에
/// 있고 어긋나면 어느 쪽이 맞는지 알 방법이 없었다.
/// 경위는 <c>docs/projmng-account-merge.md</c>.
/// </para>
///
/// <para>
/// 설정의 주인은 서버가 로그인 계정으로 가린다 — 화면이 누구인지 보내지 않는다.
/// </para>
/// </remarks>
public sealed class WbsDevUserClient(GatewayClient gateway)
{
    private const string PrefUrl = "projmng/wbs-board/prefs";

    public Task<WbsPrefDto?> GetPrefAsync(int prjRid, string key, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsPrefDto>($"{PrefUrl}/{Uri.EscapeDataString(key)}?prjRid={prjRid}", ct);

    /// <summary><paramref name="value"/> 에 <c>null</c> 을 주면 지운다.</summary>
    public Task SetPrefAsync(int prjRid, string key, object? value, CancellationToken ct = default)
        => gateway.PutAsync($"{PrefUrl}/{Uri.EscapeDataString(key)}?prjRid={prjRid}", value, ct);
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
