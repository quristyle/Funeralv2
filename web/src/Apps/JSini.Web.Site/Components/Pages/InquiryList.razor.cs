using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Site.Api;

namespace JSini.Web.Site.Components.Pages;

public partial class InquiryList
{
    [Inject] private SiteAdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        _status is null ? SchSummary.Any : StatusText(_status);

    /// <summary>서버가 쓰는 상태 값. 화면 문구는 <see cref="StatusText" /> 가 만든다.</summary>
    private static readonly string[] Statuses = ["new", "reading", "replied", "closed"];

    private string? _status;
    private IReadOnlyList<InquiryDto> _rows = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetInquiriesAsync(_status);
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

    private static string StatusText(string status) => status switch
    {
        "new" => "접수",
        "reading" => "확인 중",
        "replied" => "답변 완료",
        "closed" => "종료",
        _ => status,
    };
}
