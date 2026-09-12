using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트 참여자. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 <c>sp_dev_proj_user_map_exec</c>(배정 토글) 와
/// <c>sp_proj_user_map_list</c>(짝 목록) 둘이었다. 갈래가 둘인 것은
/// <b>보는 각도가 둘</b>이라서이고, 그것은 그대로 옮겼다.
///
/// <para>
/// 화면은 그 둘을 <b>한 화면에서 축만 뒤집어</b> 다룬다
/// (<c>Components/Shared/ParticipationBoard</c>). 그래도 이쪽 갈래는 그대로다 —
/// 어느 축이든 「상대 전부 + 참여 여부」와 「걸려 있는 짝」이 둘 다 필요하다.
/// </para>
/// </remarks>
public sealed class ProjectUserClient(GatewayClient gateway)
{
    private const string Url = "projmng/project-users";

    /// <summary>
    /// 프로젝트 전부에 이 사람의 참여 여부를 붙여 받는다.
    /// <b>안 들어간 프로젝트도 온다</b> — 그래야 켤 수 있다.
    /// </summary>
    public Task<IReadOnlyList<ProjectAssignmentDto>> AssignmentsAsync(
        string? userId, int? prjRid = null, CancellationToken ct = default)
    {
        var query = $"?userId={Uri.EscapeDataString(userId ?? string.Empty)}"
                    + (prjRid is null ? string.Empty : $"&prjRid={prjRid}");

        return gateway.GetListAsync<ProjectAssignmentDto>($"{Url}/assignments{query}", ct);
    }

    /// <summary>
    /// 참여를 <b>한 번에 여러 건</b> 켜고 끈다. 서버에서 한 트랜잭션이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 기준은 프로젝트나 사람 <b>둘 중 하나</b>다. 둘 다 주거나 둘 다 비우면
    /// 서버가 거절한다 — <c>add</c> 에 담긴 것이 아이디인지 프로젝트 번호인지
    /// 알 수 없기 때문이다.
    /// </para>
    ///
    /// <para>
    /// <b>한 건짜리도 이 길로 간다.</b> 한 줄짜리 통로를 따로 두었다가 없앴다 —
    /// 서버가 어차피 한 건을 한 트랜잭션으로 받고, 갈래를 둘로 두면 한쪽에만
    /// 걸리는 버그가 생긴다.
    /// </para>
    /// </remarks>
    public Task SetAssignmentsAsync(
        int? prjRid,
        string? userId,
        IReadOnlyList<string> add,
        IReadOnlyList<string> remove,
        CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/assignments/bulk", new { prjRid, userId, add, remove }, ct);

    /// <summary>걸려 있는 사람-프로젝트 짝.</summary>
    public Task<IReadOnlyList<ProjectUserDto>> ListAsync(
        int? prjRid = null, string? userId = null, CancellationToken ct = default)
    {
        var query = $"?userId={Uri.EscapeDataString(userId ?? string.Empty)}"
                    + (prjRid is null ? string.Empty : $"&prjRid={prjRid}");

        return gateway.GetListAsync<ProjectUserDto>($"{Url}{query}", ct);
    }
}

/// <summary>프로젝트 한 건과 이 사람의 참여 여부.</summary>
public sealed class ProjectAssignmentDto
{
    public bool Accepted { get; set; }

    public int PrjRid { get; set; }
    public string? PrjName { get; set; }
    public string? PrjDesc { get; set; }
    public DateOnly? PrjSdt { get; set; }
    public DateOnly? PrjEdt { get; set; }
}

/// <summary>걸려 있는 사람-프로젝트 짝 한 줄.</summary>
public sealed class ProjectUserDto
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>참여 프로젝트 수. <b>거르기와 무관한 전체 기준</b>이다.</summary>
    public int InvCnt { get; set; }

    public int PrjRid { get; set; }
    public string? PrjName { get; set; }
}
