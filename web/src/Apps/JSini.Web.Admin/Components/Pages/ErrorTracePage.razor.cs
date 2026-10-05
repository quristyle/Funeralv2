using Microsoft.AspNetCore.Components;
using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Admin.Components.Pages;

public partial class ErrorTracePage
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>한 번에 받아 둘 최대 건수. 서버 상한도 같은 값 언저리다(500).</summary>
    /// <remarks>
    /// 표가 20건씩 나누므로 200 이면 열 쪽이다. 더 늘리는 대신 기간으로
    /// 좁히게 한다 — 발송 이력(<c>PushLogs</c>)과 같은 기준이다.
    /// </remarks>
    private const int Cap = 200;

    /// <summary>
    /// 추적 번호. <b>채워져 있으면 이것만 쓴다</b>(아래 <see cref="ReloadAsync"/>).
    /// </summary>
    private string? _trace;

    /// <summary>
    /// 조회 기간. <b>기본이 최근 사흘이다.</b>
    /// </summary>
    /// <remarks>
    /// 발송 이력은 이레인데 여기는 사흘로 둔다. 오류 신고는 대개 당일이나
    /// 전날 것이고, 기간을 넓게 잡으면 같은 오류가 수십 줄 겹쳐 나와
    /// 「방금 그 건」을 찾기가 오히려 어려워진다.
    /// </remarks>
    private DateTime? _from = AppTime.TodayDate.AddDays(-3);
    private DateTime? _to = AppTime.TodayDate;

    private string? _keyword;

    private IReadOnlyList<PortalErrorDto> _rows = [];

    /// <summary>표에서 고른 줄. 스택은 여기 없다 — 목록 응답에서 빠져 온다.</summary>
    private PortalErrorDto? _picked;

    /// <summary>고른 줄을 번호로 다시 집어 온 것. <b>이쪽에만 스택이 있다.</b></summary>
    private PortalErrorDto? _detail;

    private bool _detailBusy;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => string.IsNullOrWhiteSpace(_trace)
        ? SchSummary.Of(SchSummary.Period(_from, _to), _keyword)
        : SchSummary.Of(_trace);

    /// <summary>
    /// 제목 옆에 옅게 붙는 한마디. <b>상한에 닿았는지</b>를 여기서 말한다.
    /// </summary>
    /// <remarks>
    /// 조회의 결과라 토스트로 띄울 수도 있지만, 이 화면은 줄을 고르고 상세를
    /// 읽는 동안 조건을 계속 바꾸게 되어 토스트가 연달아 뜬다. 제목 옆에
    /// 붙여 두면 **지금 보고 있는 목록이 전부인지**가 늘 같은 자리에 있다.
    /// </remarks>
    private string? ListHint => _rows.Count switch
    {
        0 => null,
        Cap => $"{Cap}건까지만 읽었습니다. 기간을 좁히십시오.",
        var n => $"{n}건",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// 조회. <b>추적 번호가 있으면 그것만 쓴다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 번호를 들고 온 사람에게 기간이 맞아야 한다고 요구하면 안 된다 — 신고가
    /// 늦게 올라오는 일이 흔하고(「지난주에 났는데요」), 기본 기간은 사흘이다.
    /// 번호가 있으면 기간을 아예 보지 않는 쪽이 맞다.
    /// </para>
    ///
    /// <para>
    /// 번호 조회는 <b>스택까지 실려 온다.</b> 그래서 결과가 하나면 그 자리에서
    /// 골라 둔다 — 번호를 치고 조회한 사람이 다음에 할 일이 그것뿐이다.
    /// </para>
    /// </remarks>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _picked = null;
        _detail = null;

        if (!string.IsNullOrWhiteSpace(_trace))
        {
            _rows = await Api.GetPortalErrorAsync(_trace);

            if (_rows.Count > 0)
            {
                _picked = _rows[0];

                // 번호 조회의 응답에는 스택이 이미 들어 있다. 다시 읽지 않는다.
                _detail = _rows[0];
            }

            return _rows.Count;
        }

        _rows = await Api.GetPortalErrorsAsync(_keyword, _from, _to, Cap);
        return _rows.Count;
    },
    "조건에 맞는 오류 기록이 없습니다.",
    "오류 기록을 읽지 못했습니다");

    /// <summary>
    /// 줄을 골랐을 때. 스택을 그 번호로 따로 읽어 온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 실패해도 <b>고른 줄은 그대로 둔다.</b> 요청 정보와 메시지는 목록에 이미
    /// 와 있어서, 스택을 못 읽었다고 상세를 통째로 비우면 있는 것까지 잃는다.
    /// </para>
    ///
    /// <para>
    /// <see cref="DataPage.LoadAsync"/> 를 쓰지 않는다 — 그쪽은 표의 조회를
    /// 감싸는 자리라 여기서 부르면 「조회 결과가 없습니다」가 상세 읽기에
    /// 대고 뜬다.
    /// </para>
    /// </remarks>
    private async Task PickAsync(PortalErrorDto? row)
    {
        _picked = row;
        _detail = null;

        if (row is null) return;

        // 번호로 조회해 온 줄은 스택을 이미 들고 있다.
        if (!string.IsNullOrEmpty(row.Detail))
        {
            _detail = row;
            return;
        }

        _detailBusy = true;

        try
        {
            var found = await Api.GetPortalErrorAsync(row.TraceId);

            // 같은 번호로 여러 줄이 잡혀도 **고른 그 줄**을 집는다.
            _detail = found.FirstOrDefault(e => e.Id == row.Id) ?? found.FirstOrDefault();
        }
        catch (ApiException)
        {
            Say("스택을 읽지 못했습니다.", NoticeTone.Warning);
        }
        finally
        {
            _detailBusy = false;
        }
    }
}
