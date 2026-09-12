namespace ProjMngServer.Models;

/// <summary>할 일 한 건 — <c>projmng.home_todo</c>.</summary>
public sealed class HomeTodo
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
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

    /// <summary>누구의 할 일인가.</summary>
    public string? TargetUser { get; set; }

    /// <summary>지정 금액. 완료하면 이 값이 쌓인다.</summary>
    public long FixPoint { get; set; }

    public string? TodoState { get; set; }

    /// <summary>상태 이름. 공통코드(<c>TODO_STATE</c>)에서 조인해 온다 — <b>읽기 전용</b>.</summary>
    public string? TodoStateName { get; set; }
}

/// <summary>사람별 적립 금액.</summary>
public sealed class HomeTodoPay
{
    public string? TargetUser { get; set; }

    /// <summary>완료한 것의 합계 — 전체 기간.</summary>
    public long TotalPay { get; set; }

    /// <summary>오늘 이후분만.</summary>
    public long TodayPay { get; set; }
}
