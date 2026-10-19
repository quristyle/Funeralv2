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

    /// <summary>한 번에 받아 둘 최대 건수. 표가 20건씩 나누므로 기간으로 좁히게 하고 여기서 늘리지 않는다(발송 이력과 같은 기준).</summary>
    private const int Cap = 200;

    /// <summary>추적 번호. 채워져 있으면 이것만 쓴다(ReloadAsync).</summary>
    private string? _trace;

    /// <summary>조회 기간. 기본은 최근 사흘이다 — 오류 신고는 대개 당일이나 전날 것이고, 넓게 잡으면 같은 오류가 겹쳐 「방금 그 건」을 찾기 어렵다.</summary>
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

    /// <summary>제목 옆에 옅게 붙는 한마디. 상한에 닿았는지를 말한다 — 토스트로 띄우면 줄을 고르는 동안 연달아 뜨므로 제목 옆에 둔다.</summary>
    private string? ListHint => _rows.Count switch
    {
        0 => null,
        Cap => $"{Cap}건까지만 읽었습니다. 기간을 좁히십시오.",
        var n => $"{n}건",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>조회. 추적 번호가 있으면 그것만 쓴다 — 신고가 늦게 올라와 기간이 안 맞는 일이 흔하다. 번호 조회는 스택까지 실려 와서 결과가 하나면 그 자리에서 고른다.</summary>
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

    /// <summary>줄을 골랐을 때 스택을 그 번호로 따로 읽어 온다. 실패해도 고른 줄은 그대로 둔다 — 요청 정보와 메시지는 목록에 이미 있다. DataPage.LoadAsync 는 상세 읽기에 「조회 결과가 없습니다」를 대고 띄우므로 쓰지 않는다.</summary>
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
