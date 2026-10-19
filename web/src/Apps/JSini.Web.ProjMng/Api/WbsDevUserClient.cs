using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>WBS 대시보드의 사용자별 화면 설정. 개발자 명부는 여기 없다 — 그 속성들은 포털 계정으로 옮겨 갔다(docs/projmng-account-merge.md). 설정의 주인은 서버가 로그인 계정으로 가리므로 화면이 누구인지 보내지 않는다.</summary>
public sealed class WbsDevUserClient(GatewayClient gateway)
{
    private const string PrefUrl = "projmng/wbs-board/prefs";

    public Task<WbsPrefDto?> GetPrefAsync(int prjRid, string key, CancellationToken ct = default)
        => gateway.GetOneAsync<WbsPrefDto>($"{PrefUrl}/{Uri.EscapeDataString(key)}?prjRid={prjRid}", ct);

    /// <summary>value 에 null 을 주면 지운다.</summary>
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
