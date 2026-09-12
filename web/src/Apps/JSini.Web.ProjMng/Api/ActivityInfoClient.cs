using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// Glue 서비스 정의. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 <c>sp_dev_activityinfo_exec</c> 였다. 그 프로시저에는 <b>지우는
/// 갈래가 없어서</b> 파일에서 사라진 서비스가 DB 에 영영 남았다.
/// </remarks>
public sealed class ActivityInfoClient(GatewayClient gateway)
{
    private const string Url = "projmng/activity-infos";

    /// <summary>목록. <b>소스로만 거른다</b> — 표에 프로젝트 칸이 없다.</summary>
    public Task<IReadOnlyList<ActivityInfoDto>> ListAsync(string? srcRid, CancellationToken ct = default)
        => gateway.GetListAsync<ActivityInfoDto>(
            string.IsNullOrWhiteSpace(srcRid) ? Url : $"{Url}?srcRid={Uri.EscapeDataString(srcRid)}", ct);

    public Task SaveAsync(ActivityInfoDto item, CancellationToken ct = default)
        => gateway.PostAsync(Url, item, ct);

    public Task DeleteAsync(ActivityInfoDto item, CancellationToken ct = default)
        => gateway.DeleteAsync(
            $"{Url}?srcRid={Uri.EscapeDataString(item.SrcRid ?? string.Empty)}"
            + $"&serviceName={Uri.EscapeDataString(item.ServiceName)}"
            + $"&transitionName={Uri.EscapeDataString(item.TransitionName)}", ct);
}

/// <summary>Glue 서비스 정의 한 줄.</summary>
public sealed class ActivityInfoDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string TransitionName { get; set; } = string.Empty;
    public string? TransitionValue { get; set; }

    /// <summary>자료 접근 객체 이름.</summary>
    public string? Dao { get; set; }

    public string? ProcedureName { get; set; }
    public string? ResultKey { get; set; }
    public string? Activity { get; set; }

    /// <summary><c>sql</c> 이면 <see cref="ActiveContext"/> 에 질의 본문이 있다.</summary>
    public string? ActivityType { get; set; }

    /// <summary>질의 본문. 종류가 <c>proc</c> 이면 비어 있다 — 본문은 DB 에 있다.</summary>
    public string? ActiveContext { get; set; }

    /// <summary>어느 소스에서 훑은 것인가.</summary>
    public string? SrcRid { get; set; }

    /// <summary>질의 본문을 들고 있는 줄인가. 오른쪽 편집기가 이것으로 갈린다.</summary>
    public bool HasQuery =>
        string.Equals(ActivityType, "sql", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ActiveContext);
}
