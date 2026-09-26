using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class TransactionListPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>거래처 상세의 「내가 등록한 거래」에서 넘어올 때.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "companyId")]
    public long? CompanyIdQuery { get; set; }

    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(CargoCodes.PaymentStatusOptions, o => o.Value, o => o.Text, _status),
        SchSummary.Period(_from, _to),
        _companyId is null ? null : SchSummary.NameOf(_companies, o => o.Value, o => o.Text, _companyId, empty: string.Empty));

    private string? _status;
    private DateTime? _from;
    private DateTime? _to;
    private string? _companyId;

    private List<SchOption> _companies = [new(null, SchSummary.Any)];
    private IReadOnlyList<MyTransaction> _rows = [];

    private PaymentPopup? _payment;
    private ReviewPopup? _review;
    private TransactionEditPopup? _edit;
    private ConfirmDialog? _confirm;

    protected override Task OnInitializedAsync()
    {
        if (CompanyIdQuery is not null)
        {
            _companyId = CompanyIdQuery.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ReloadAsync();
    }

    private Task ResetAsync()
    {
        _status = null;
        _from = null;
        _to = null;
        _companyId = null;
        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetMyTransactionsAsync(
            _status,
            _from is null ? null : DateOnly.FromDateTime(_from.Value),
            _to is null ? null : DateOnly.FromDateTime(_to.Value),
            long.TryParse(_companyId, out var id) ? id : null);

        CollectCompanies(_rows);
        return _rows.Count;
    }, "조건에 맞는 거래가 없습니다.", "내 거래를 읽지 못했습니다");

    /// <summary>
    /// 읽어 온 거래에서 거래처를 모아 고르개에 **보탠다**. 거른 결과로 갈아 끼우면
    /// 한 거래처로 거른 순간 고르개에 그 하나만 남아 다른 곳으로 옮길 수 없다.
    /// </summary>
    private void CollectCompanies(IEnumerable<MyTransaction> rows)
    {
        foreach (var t in rows)
        {
            var value = t.CompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (_companies.All(c => c.Value != value))
            {
                _companies.Add(new SchOption(value, t.CompanyName));
            }
        }

        // 콤보가 새 목록을 알아채도록 참조를 바꾼다.
        _companies = [.. _companies];
    }

    private static string DelayText(MyTransaction t) =>
        t.OverdueDays is > 0 ? $"{t.OverdueDays}일 지남"
        : t.DelayDays is > 0 ? $"{t.DelayDays}일 늦음"
        : "-";

    /// <summary>
    /// 후기를 연다. 이미 쓴 것이 있으면 그 글을 채워야 하는데 목록에는 유무만 있어서
    /// 상세를 한 번 읽는다. 없으면 읽지 않는다.
    /// </summary>
    private async Task OpenReviewAsync(MyTransaction t)
    {
        var summary = $"{t.CompanyName} · {CargoCodes.Day(t.TransportDate)} · {CargoCodes.Won(t.Amount)}";

        if (!t.HasReview)
        {
            _review?.Open(t.TransactionId, summary);
            return;
        }

        TransactionDetail? detail = null;
        if (await RunAsync(async () => detail = await Api.GetTransactionAsync(t.TransactionId),
                okMessage: string.Empty, failMessage: "후기를 읽지 못했습니다"))
        {
            _review?.Open(t.TransactionId, summary, detail?.Review?.Content);
        }
    }

    private async Task DeleteAsync(MyTransaction t)
    {
        // 무엇이 함께 사라지는지 적는다 — 이 거래가 거래처 통계에서 빠진다.
        var ok = await _confirm!.AskAsync(
            $"{t.CompanyName} · {CargoCodes.Day(t.TransportDate)} · {CargoCodes.Won(t.Amount)} 거래를 지웁니다."
            + "\n지운 거래는 거래처 통계에서도 빠집니다.");

        if (!ok)
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteTransactionAsync(t.TransactionId), "거래를 지웠습니다.", "거래를 지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
