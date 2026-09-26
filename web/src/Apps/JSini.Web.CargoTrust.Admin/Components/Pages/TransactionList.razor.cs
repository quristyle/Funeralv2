using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class TransactionList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    /// <summary>대시보드 바로가기가 싣고 오는 첫 조건.</summary>
    [SupplyParameterFromQuery(Name = "reviewStatus")] public string? ReviewStatusQuery { get; set; }

    /// <inheritdoc cref="ReviewStatusQuery"/>
    [SupplyParameterFromQuery(Name = "paymentStatus")] public string? PaymentStatusQuery { get; set; }

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(CargoAdminCodes.PaymentStatus.Filter, o => o.Value, o => o.Text, _payment),
        SchSummary.NameOf(CargoAdminCodes.ReviewStatus.Filter, o => o.Value, o => o.Text, _review),
        SchSummary.Period(_from, _to));

    /// <summary>
    /// 결제 상태 고르개. 맨 앞이 「그대로」(null)다 — 서버가 null 을 「안 바꾼다」로 읽는다.
    /// 「전체」라고 부르지 않는 이유는 여기가 조건이 아니라 **바꿀 값**이라서다.
    /// </summary>
    private static readonly IReadOnlyList<SchOption> PaymentChoices =
        [new SchOption(null, "그대로"), .. CargoAdminCodes.PaymentStatus.Options];

    private IReadOnlyList<AdminTransaction> _rows = [];

    private string? _keyword;
    private string? _payment;
    private string? _review;
    private DateTime? _from;
    private DateTime? _to;

    private bool _editing;
    private AdminTransaction? _target;
    private AdminTransactionUpdate _form = new();

    protected override Task OnInitializedAsync()
    {
        _review = Known(CargoAdminCodes.ReviewStatus.Options, ReviewStatusQuery);
        _payment = Known(CargoAdminCodes.PaymentStatus.Options, PaymentStatusQuery);

        return ReloadAsync();
    }

    /// <summary>
    /// 주소로 들어온 코드가 고르개에 있는 것일 때만 받는다. 모르는 글자를 그대로 실으면
    /// 고르개는 빈칸인데 서버는 그 글자로 걸러 빈 목록을 준다 — 왜 비었는지 안 보인다.
    /// </summary>
    private static string? Known(IReadOnlyList<SchOption> options, string? code) =>
        options.FirstOrDefault(o => string.Equals(o.Value, code, StringComparison.OrdinalIgnoreCase))?.Value;

    private static bool IsFlagged(AdminTransaction t) =>
        string.Equals(t.ReviewStatus, "FLAGGED", StringComparison.OrdinalIgnoreCase);

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetTransactionsAsync(_keyword, _payment, _review, _from, _to);
        return _rows.Count;
    }, "조건에 맞는 거래가 없습니다.", "거래를 읽지 못했습니다");

    private void Open(AdminTransaction row)
    {
        _target = row;
        _form = new AdminTransactionUpdate
        {
            ReviewStatus = row.ReviewStatus,
            PaymentStatus = null,
            AdminMemo = row.AdminMemo,
        };
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (_target is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.ReviewStatus))
        {
            Say("검증 상태를 고르십시오.", NoticeTone.Warning);
            return;
        }

        var id = _target.TransactionId;
        var body = _form;

        if (await RunAsync(() => Api.UpdateTransactionAsync(id, body), "저장했습니다.", "저장하지 못했습니다"))
        {
            // 닫는 것은 성공했을 때만이다 — 실패했는데 닫으면 적은 메모가 사라진다.
            _editing = false;
            await ReloadAsync();
        }
    }
}
