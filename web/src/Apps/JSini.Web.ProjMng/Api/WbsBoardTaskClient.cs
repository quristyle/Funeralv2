using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// WBS 대시보드의 화면별 일감 — <c>projmng/wbs-board/tasks</c>.
/// </summary>
/// <remarks>
/// 원장 1 : N 일감이고 <see cref="WbsBoardTaskDto.ActivityId"/> 로 붙는다.
/// 상세 목록의 「일감」 칸은 <see cref="CountsAsync"/> 로 <b>한 번에</b> 받는다 —
/// 줄마다 물으면 200번이 넘는다.
/// </remarks>
public sealed class WbsBoardTaskClient(GatewayClient gateway)
{
    private const string Url = "projmng/wbs-board/tasks";

    public Task<IReadOnlyList<WbsBoardTaskCountDto>> CountsAsync(
        int prjRid, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardTaskCountDto>($"{Url}/counts?prjRid={prjRid}", ct);

    public Task<IReadOnlyList<WbsBoardTaskDto>> ListAsync(
        int prjRid, string activityId, CancellationToken ct = default)
        => gateway.GetListAsync<WbsBoardTaskDto>(
            $"{Url}?prjRid={prjRid}&activityId={Uri.EscapeDataString(activityId)}", ct);

    public Task CreateAsync(int prjRid, WbsBoardTaskDto item, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}?prjRid={prjRid}", item, ct);

    /// <summary>
    /// 담은 칸만 고친다. <b>안 담은 칸은 그대로 남는다</b> — 메모만 고칠 때
    /// 완료 표시가 딸려 지워지면 안 된다.
    /// </summary>
    public Task UpdateAsync(
        int prjRid, int taskId, IReadOnlyDictionary<string, object?> patch,
        CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{taskId}?prjRid={prjRid}", patch, ct);

    public Task DeleteAsync(int prjRid, int taskId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{taskId}?prjRid={prjRid}", ct);
}

/// <summary>화면별 일감 건수.</summary>
public sealed class WbsBoardTaskCountDto
{
    public string? ActivityId { get; set; }
    public int Cnt { get; set; }
    public int Done { get; set; }
}

/// <summary>일감 한 줄.</summary>
public sealed class WbsBoardTaskDto
{
    public int TaskId { get; set; }
    public string? ActivityId { get; set; }

    /// <summary>구분(설계 · 개발 · 시험 …). 자유 글자다.</summary>
    public string? TaskDiv { get; set; }

    public string? Memo { get; set; }

    /// <summary><c>o</c> 면 완료. <b>빈 값이 미완료</b>다.</summary>
    public string? DoneYn { get; set; }

    public int SortOrder { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }

    /// <summary>화면에서 켜고 끄는 값. 저장할 때 <see cref="DoneYn"/> 으로 옮긴다.</summary>
    public bool Done
    {
        get => DoneYn == "o";
        set => DoneYn = value ? "o" : null;
    }
}
