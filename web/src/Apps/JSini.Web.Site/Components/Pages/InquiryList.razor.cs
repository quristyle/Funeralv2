using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Site.Api;

namespace JSini.Web.Site.Components.Pages;

public partial class InquiryList
{
    [Inject] private SiteAdminClient Api { get; set; } = default!;

    private ConfirmDialog? _confirm;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        _status is null ? SchSummary.Any : StatusText(_status);

    /// <summary>서버가 쓰는 상태 값. 화면 문구는 <see cref="StatusText" /> 가 만든다.</summary>
    private static readonly string[] Statuses = ["new", "reading", "replied", "closed"];

    private string? _status;
    private IReadOnlyList<InquiryDto> _rows = [];

    /// <summary>체크한 줄. 다시 읽으면 줄 객체가 바뀌어 체크가 맞지 않으므로 조회마다 비운다.</summary>
    private IReadOnlyList<InquiryDto> _picked = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetInquiriesAsync(_status);
        _picked = [];
        return _rows.Count;
    }, "조건에 맞는 문의가 없습니다.", "문의내역을 읽지 못했습니다");

    private async Task SetStatusAsync(InquiryDto q, string status)
    {
        if (await RunAsync(() => Api.SetStatusAsync(q.Id, status),
                $"'{q.Subject}' 를 {StatusText(status)} 으로 바꿨습니다.", "상태를 바꾸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private Task DeleteAsync(InquiryDto q) => Api.DeleteInquiryAsync(q.Id);

    private async Task DeleteInDetailAsync(InquiryDto q)
    {
        if (_confirm is null) return;
        if (await _confirm.AskAsync($"'{q.Subject}' 문의를 삭제하시겠습니까?"))
        {
            if (await RunAsync(() => Api.DeleteInquiryAsync(q.Id), "삭제했습니다.", "삭제하지 못했습니다"))
            {
                await ReloadAsync();
            }
        }
    }

    /// <summary>체크한 것을 한꺼번에 그 상태로 바꾼다. 이미 그 상태인 줄은 빼고 왕복은 하나로 보낸다.</summary>
    private async Task SetStatusPickedAsync(string status)
    {
        var targets = _picked.Where(q => q.Status != status).Select(q => q.Id).ToList();
        if (targets.Count == 0)
        {
            Say($"고른 문의가 이미 모두 {StatusText(status)} 입니다.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.SetStatusManyAsync(targets, status),
                $"{targets.Count}건을 {StatusText(status)} 으로 바꿨습니다.", "상태를 바꾸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>체크한 것을 한꺼번에 지운다. 묻고 나서 한다 — 체크해 둔 여러 건은 무엇이 사라졌는지 되짚을 길이 없다.</summary>
    private async Task DeletePickedAsync()
    {
        var targets = _picked.Select(q => q.Id).ToList();
        if (targets.Count == 0 || _confirm is null) return;

        if (!await _confirm.AskAsync($"고른 문의 {targets.Count}건을 삭제하시겠습니까?")) return;

        if (await RunAsync(() => Api.DeleteManyAsync(targets),
                $"{targets.Count}건을 삭제했습니다.", "삭제하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private static string StatusText(string status) => status switch
    {
        "new" => "접수",
        "reading" => "확인 중",
        "replied" => "답변 완료",
        "closed" => "종료",
        _ => status,
    };
}
