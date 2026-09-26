using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class TodoList
{
    [Inject] private HomeTodoClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Day(_targetDate),
        _userName,
        _completeName,
        _todoStateName);

    /// <summary>고른 값의 이름들. 고르개가 코드만 올려 주어 따로 받아 둔다.</summary>
    private string? _userName;

    private string? _completeName;

    private string? _todoStateName;

    private IReadOnlyList<HomeTodoDto> _rows = [];

    private DateTime? _targetDate = DateTime.Today;
    private string? _userCode;
    private string? _completeYn;
    private string? _todoState;

    private string Hint => $"{_rows.Count(t => t.IsComplete)} / {_rows.Count}건 완료";

    /// <summary>
    /// 되풀이를 만들 수 있는가. <b>사람과 날짜가 있어야 한다</b> —
    /// 누구 것을 만들지 모르면 만들 수 없다(머리말).
    /// </summary>
    private bool CanMake => _targetDate is not null && !string.IsNullOrWhiteSpace(_userCode);

    private bool? CompleteFilter => _completeYn switch
    {
        "Y" => true,
        "N" => false,
        _ => null,
    };

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(
            _userCode, _todoState, CompleteFilter,
            _targetDate is null ? null : DateOnly.FromDateTime(_targetDate.Value));

        return _rows.Count;
    }, "할일이 없습니다.", "할일을 읽지 못했습니다");

    /// <summary>새 할 일은 고른 날짜·사람에 붙는다.</summary>
    private void FillNew(HomeTodoDto t)
    {
        t.TargetDay = _targetDate is null ? DateOnly.FromDateTime(DateTime.Today)
                                          : DateOnly.FromDateTime(_targetDate.Value);
        t.TargetUser = _userCode;
    }

    private async Task SaveAsync((HomeTodoDto Item, bool IsNew) e)
    {
        if (e.IsNew)
        {
            await Api.CreateAsync(e.Item);
        }
        else
        {
            await Api.UpdateAsync(e.Item);
        }
    }

    private Task DeleteAsync(HomeTodoDto t) => Api.DeleteAsync(t.TodoKey);

    /// <summary>그 날짜·사람의 되풀이 할 일을 만든다. 만든 뒤 바로 다시 읽는다.</summary>
    private async Task MakeAsync()
    {
        if (!CanMake)
        {
            return;
        }

        var made = await RunAsync(
            () => Api.MakeAsync(DateOnly.FromDateTime(_targetDate!.Value), [_userCode!]),
            "되풀이 할일을 만들었습니다.", "할일을 만들지 못했습니다");

        if (made)
        {
            await SearchAsync();
        }
    }
}
