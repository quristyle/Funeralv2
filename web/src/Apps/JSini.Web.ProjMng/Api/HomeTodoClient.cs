using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 할 일. <b>프로시저를 부르지 않는다.</b>
/// </summary>
/// <remarks>
/// 옛 길은 셋이었다 — <c>sp_home_todo_exec</c> · <c>_make</c> · <c>_pay</c>.
/// 등록·수정자는 <b>서버가 게이트웨이 신원으로 채운다</b> — 보내지 않는다.
/// </remarks>
public sealed class HomeTodoClient(GatewayClient gateway)
{
    private const string Url = "projmng/home-todos";

    public Task<IReadOnlyList<HomeTodoDto>> ListAsync(
        string? targetUser = null, string? todoState = null,
        bool? isComplete = null, DateOnly? targetDay = null, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(targetUser)) query.Add($"targetUser={Uri.EscapeDataString(targetUser)}");
        if (!string.IsNullOrWhiteSpace(todoState)) query.Add($"todoState={Uri.EscapeDataString(todoState)}");
        if (isComplete is not null) query.Add($"isComplete={isComplete.Value.ToString().ToLowerInvariant()}");
        if (targetDay is not null) query.Add($"targetDay={targetDay:yyyy-MM-dd}");

        return gateway.GetListAsync<HomeTodoDto>(
            query.Count == 0 ? Url : $"{Url}?{string.Join('&', query)}", ct);
    }

    public Task<HomeTodoDto?> CreateAsync(HomeTodoDto item, CancellationToken ct = default)
        => gateway.PostAsync<HomeTodoDto>(Url, item, ct);

    public Task<HomeTodoDto?> UpdateAsync(HomeTodoDto item, CancellationToken ct = default)
        => gateway.PutAsync<HomeTodoDto>($"{Url}/{item.TodoKey}", item, ct);

    public Task DeleteAsync(long todoKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{todoKey}", ct);

    /// <summary>그 날짜의 되풀이 할 일을 만든다.</summary>
    public Task MakeAsync(DateOnly targetDay, IReadOnlyList<string> users, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/make", new { targetDay, users }, ct);

    /// <summary>사람별 적립 금액.</summary>
    public Task<IReadOnlyList<HomeTodoPayDto>> PayAsync(string? targetUser = null, CancellationToken ct = default)
        => gateway.GetListAsync<HomeTodoPayDto>(
            $"{Url}/pay" + (string.IsNullOrWhiteSpace(targetUser)
                ? string.Empty
                : $"?targetUser={Uri.EscapeDataString(targetUser)}"), ct);
}

/// <summary>할 일 한 건.</summary>
public sealed class HomeTodoDto
{
    public long TodoKey { get; set; }

    public DateOnly? TargetDay { get; set; }
    public string? Title { get; set; }
    public bool IsComplete { get; set; }

    public DateTime? CreDt { get; set; }
    public DateTime? CompDt { get; set; }
    public string? CreId { get; set; }
    public string? CompId { get; set; }
    public DateTime? ModDt { get; set; }
    public string? ModId { get; set; }

    public string? Comments { get; set; }
    public string? TargetUser { get; set; }

    /// <summary>지정 금액. 완료하면 적립된다.</summary>
    public long FixPoint { get; set; }

    public string? TodoState { get; set; }

    /// <summary>상태 이름. 공통코드에서 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TodoStateName { get; set; }
}

/// <summary>사람별 적립 금액.</summary>
public sealed class HomeTodoPayDto
{
    public string? TargetUser { get; set; }
    public long TotalPay { get; set; }
    public long TodayPay { get; set; }
}
