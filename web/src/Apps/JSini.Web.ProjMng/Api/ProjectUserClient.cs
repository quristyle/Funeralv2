using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>프로젝트 참여자. 프로시저를 부르지 않는다. 배정 토글(sp_dev_proj_user_map_exec)과 짝 목록(sp_proj_user_map_list)을 나눈 것은 보는 각도가 둘이라서이고, 화면은 ParticipationBoard 에서 축만 뒤집어 다룬다.</summary>
public sealed class ProjectUserClient(GatewayClient gateway)
{
    private const string Url = "projmng/project-users";

    /// <summary>프로젝트 전부에 이 사람의 참여 여부를 붙여 받는다. 안 들어간 프로젝트도 온다 — 그래야 켤 수 있다.</summary>
    public Task<IReadOnlyList<ProjectAssignmentDto>> AssignmentsAsync(
        string? userId, int? prjRid = null, CancellationToken ct = default)
    {
        var query = $"?userId={Uri.EscapeDataString(userId ?? string.Empty)}"
                    + (prjRid is null ? string.Empty : $"&prjRid={prjRid}");

        return gateway.GetListAsync<ProjectAssignmentDto>($"{Url}/assignments{query}", ct);
    }

    /// <summary>참여를 한 번에 여러 건 켜고 끈다(한 트랜잭션). 기준은 프로젝트나 사람 둘 중 하나여야 한다 — 둘 다 주면 add 의 값이 무엇인지 알 수 없어 서버가 거절한다. 한 건짜리도 이 길로 간다.</summary>
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
