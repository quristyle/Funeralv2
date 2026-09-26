using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class PushLogs
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        _reason);

    /// <summary>
    /// 한 번에 받아 둘 최대 건수.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>화면이 상한을 정한다.</b> 전에는 클라이언트의 <c>pageSize</c> 기본값
    /// (50)이 조용히 상한이었고, 화면은 그것을 전부라고 그렸다.
    /// </para>
    ///
    /// <para>
    /// 표가 30건씩 나누므로 200 이면 일곱 쪽이다. 더 늘리는 대신 기간으로
    /// 좁히게 한다 — 표 안에서 굴리는 것보다 조건을 좁히는 편이 빠르고,
    /// 넘으면 위에서 그렇게 말해 준다.
    /// </para>
    /// </remarks>
    private const int Cap = 200;

    /// <summary>
    /// 조회 기간. <b>기본이 최근 이레다.</b>
    /// </summary>
    /// <remarks>
    /// 이력은 지우지 않으므로 자라기만 한다. 조건 없이 열면 언젠가 상한에
    /// 부딪히고, 그때 사용자가 할 수 있는 일이 없다. 기본값을 두면 화면이
    /// 열리는 순간부터 조회가 유계다.
    /// </remarks>
    private DateTime? _from = DateTime.Today.AddDays(-7);
    private DateTime? _to = DateTime.Today;

    private string? _reason;
    private IReadOnlyList<PushLogDto> _logs = [];
    private int _total;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        (_logs, _total) = await Api.GetPushLogsAsync(Cap, _reason, _from, _to);

        // **잘렸으면 반드시 말한다.** 「전부다」로 읽고 넘어가면 없는 것을
        // 찾게 된다. 조회의 **결과**라 안내 줄이 아니라 토스트로 나간다.
        // 문구는 Q&A 목록과 같게 둔다 — 같은 상황에 다른 말을 하면 다른 일로 읽는다.
        if (_total > _logs.Count)
        {
            Say($"전체 {_total}건 중 {_logs.Count}건입니다. 기간이나 사유로 좁히십시오.",
                NoticeTone.Warning);
        }

        return _logs.Count;
    }, "조건에 맞는 발송 내역이 없습니다.", "발송 이력을 읽지 못했습니다");
}
