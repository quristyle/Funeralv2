using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class TodoMonitor
{
    [Inject] private HomeTodoClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(SchSummary.Day(_targetDate), _userName);

    /// <summary>고른 사람의 이름. 고르개가 코드만 올려 주어 따로 받아 둔다.</summary>
    private string? _userName;

    private IReadOnlyList<HomeTodoPayDto> _pay = [];
    private IReadOnlyList<HomeTodoDto> _todos = [];

    private DateTime? _targetDate = DateTime.Today;
    private string? _userCode;

    private string TodoHint => $"{_todos.Count(t => t.IsComplete)} / {_todos.Count}건 완료";

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        // 나란히 읽는다. 서로 기다릴 이유가 없다(머리말).
        var pay = Api.PayAsync(_userCode);
        var todos = Api.ListAsync(
            _userCode,
            targetDay: _targetDate is null ? null : DateOnly.FromDateTime(_targetDate.Value));

        await Task.WhenAll(pay, todos);

        _pay = pay.Result;
        _todos = todos.Result;

        return _pay.Count + _todos.Count;
    }, "조회 결과가 없습니다.", "정산 현황을 읽지 못했습니다");
}
