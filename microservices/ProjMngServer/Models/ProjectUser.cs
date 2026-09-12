namespace ProjMngServer.Models;

/// <summary>
/// 프로젝트 한 건과 <b>이 사람이 거기 들어가 있는지</b>.
/// </summary>
/// <remarks>
/// 배정 화면은 체크박스 목록이라 <b>안 들어간 프로젝트도 와야 한다</b> —
/// 그래야 켤 수 있다. 그래서 「참여 목록」이 아니라 「프로젝트 목록 + 깃발」이다.
/// </remarks>
public sealed class ProjectAssignment
{
    /// <summary>이 사람이 이 프로젝트에 들어가 있는가.</summary>
    public bool Accepted { get; set; }

    public int PrjRid { get; set; }
    public string? PrjName { get; set; }
    public string? PrjDesc { get; set; }
    public DateOnly? PrjSdt { get; set; }
    public DateOnly? PrjEdt { get; set; }
}

/// <summary>걸려 있는 사람-프로젝트 짝 한 줄.</summary>
public sealed class ProjectUserRow
{
    /// <summary>포털 로그인 아이디.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 이 사람이 참여한 프로젝트 수.
    /// <b>거르기와 무관한 전체 기준</b>이다(서비스 머리말).
    /// </summary>
    public int InvCnt { get; set; }

    public int PrjRid { get; set; }
    public string? PrjName { get; set; }
}


/// <summary>
/// 참여를 <b>한 번에 여러 건</b> 켜고 끄는 요청.
/// </summary>
/// <remarks>
/// <para>
/// 기준이 <see cref="PrjRid"/> 이거나 <see cref="UserId"/> 둘 중 하나다 —
/// 「이 프로젝트에 이 사람들」이거나 「이 사람을 이 프로젝트들에」. 화면의
/// 축이 그대로 넘어온 것이고, <b>둘 다 주거나 둘 다 비우면 거절한다</b>.
/// 둘 다 주면 상대 목록을 어느 쪽으로 읽어야 할지 알 수 없다.
/// </para>
///
/// <para>
/// <see cref="Add"/> 와 <see cref="Remove"/> 에 담기는 것은 <b>상대의 열쇠</b>다.
/// 프로젝트 기준이면 아이디, 사람 기준이면 프로젝트 번호를 글자로 적은 것.
/// 한 요청이 한 트랜잭션이라 <b>다 되거나 하나도 안 된다</b>.
/// </para>
/// </remarks>
public sealed class ProjectAssignmentBulkRequest
{
    /// <summary>프로젝트 기준일 때 그 프로젝트.</summary>
    public int? PrjRid { get; set; }

    /// <summary>사람 기준일 때 그 사람.</summary>
    public string? UserId { get; set; }

    /// <summary>넣을 상대들.</summary>
    public List<string> Add { get; set; } = [];

    /// <summary>뺄 상대들.</summary>
    public List<string> Remove { get; set; } = [];
}

/// <summary>일괄 배정 결과.</summary>
public sealed class ProjectAssignmentBulkResult
{
    /// <summary>실제로 들어간 줄 수. 이미 있던 것은 세지 않는다.</summary>
    public int Added { get; set; }

    /// <summary>실제로 지워진 줄 수.</summary>
    public int Removed { get; set; }
}
