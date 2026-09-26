using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class OpenTransactions
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter, EditorRequired] public long CompanyId { get; set; }

    /// <summary>확인 창에 적을 거래처 이름. 무엇에 적는 것인지 그 자리에서 읽혀야 한다.</summary>
    [Parameter] public string? CompanyName { get; set; }

    /// <summary>
    /// 거래가 바뀌었다. 부르는 화면이 통계를 다시 읽는다 — 결제를 적으면 이
    /// 거래처의 정상률·평균 지연이 그 자리에서 달라진다.
    /// </summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private long _loadedFor;
    private IReadOnlyList<MyTransaction> _rows = [];
    private readonly HashSet<long> _picked = [];
    private int _overdue;

    private string _outcome = PaymentDraft.Received;
    private DateTime? _paidDate = DateTime.Today;

    private PaymentPopup? _payment;
    private ConfirmDialog? _confirm;

    private bool IsReceived => _outcome == PaymentDraft.Received;

    private string Hint => _rows.Count == 0
        ? "아직 못 받은 내 거래"
        : $"{_rows.Count}건 · 남은 금액 {CargoCodes.Won(_rows.Sum(t => t.Outstanding))}";

    private decimal PickedAmount => _rows.Where(t => _picked.Contains(t.TransactionId)).Sum(t => t.Outstanding);

    private bool AllPicked => _rows.Count > 0 && _picked.Count == _rows.Count;

    private string ApplyText => IsReceived ? "고른 것 받음 처리" : $"고른 것 {OutcomeName}";

    private string OutcomeName =>
        PaymentDraft.Outcomes.FirstOrDefault(o => o.Value == _outcome)?.Text ?? "처리";

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == CompanyId)
        {
            return;
        }

        _loadedFor = CompanyId;
        _picked.Clear();
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var rows = await Api.GetMyTransactionsAsync(companyId: CompanyId, open: true);

        // 늦은 것부터 — 미수금 화면과 같은 차례다. 서버는 운송일 내림차순으로 주는데
        // 여기서 먼저 보여야 하는 것은 새 거래가 아니라 **오래 안 들어온 돈**이다.
        _rows = [.. rows
            .OrderByDescending(t => t.OverdueDays ?? -1)
            .ThenBy(t => t.ExpectedPaymentDate ?? DateOnly.MaxValue)];
        _overdue = _rows.Count(t => t.OverdueDays is > 0);

        // 없어진 거래가 골라진 채로 남으면 「고른 3건」이라 적어 놓고 2건만 보낸다.
        _picked.RemoveWhere(id => _rows.All(t => t.TransactionId != id));

        // 비어 있는 것은 안내 줄이 말한다 — 「처리할 것이 없다」는 좋은 소식이라
        // 토스트로 띄우면 거래처를 열 때마다 알림이 하나씩 뜬다.
        return -1;
    }, failMessage: "미처리 거래를 읽지 못했습니다");

    private void Pick(long id, bool on)
    {
        if (on)
        {
            _picked.Add(id);
        }
        else
        {
            _picked.Remove(id);
        }
    }

    private void PickAll(bool on)
    {
        _picked.Clear();
        if (on)
        {
            foreach (var t in _rows)
            {
                _picked.Add(t.TransactionId);
            }
        }
    }

    private async Task ApplyAsync()
    {
        var ids = _rows.Where(t => _picked.Contains(t.TransactionId)).Select(t => t.TransactionId).ToList();
        if (ids.Count == 0)
        {
            Say("처리할 거래를 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (IsReceived && _paidDate is null)
        {
            Say("받은 날을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        // 무엇이 얼마나 적히는지 그 자리에서 읽혀야 한다 — 결제 기록은 쌓이는 것이라
        // 잘못 누른 것을 지우는 길이 없다(금액을 되돌리려면 반대로 또 적는 수밖에 없다).
        var what = IsReceived
            ? $"{CargoCodes.Day(DateOnly.FromDateTime(_paidDate!.Value))} 에 {CargoCodes.Won(PickedAmount)} 을(를) 받은 것으로 적습니다."
            : $"「{OutcomeName}」 로 적습니다. 받은 금액은 그대로 둡니다.";

        if (!await _confirm!.AskAsync(
                $"{CompanyName ?? "이 거래처"} 의 거래 {ids.Count}건을 한 번에 처리합니다.\n{what}",
                confirmText: "등록",
                confirmStyle: ButtonRenderStyle.Primary))
        {
            return;
        }

        var request = new BulkPaymentRequest
        {
            TransactionIds = ids,
            PaidDate = IsReceived ? DateOnly.FromDateTime(_paidDate!.Value) : null,
            Result = IsReceived ? null : _outcome,
        };

        BulkPaymentResult? result = null;
        if (!await RunAsync(async () => result = await Api.RegisterPaymentsAsync(request),
                okMessage: string.Empty, failMessage: "한 번에 처리하지 못했습니다"))
        {
            return;
        }

        var done = result?.Updated.Count ?? 0;
        var failed = result?.Failed ?? [];

        // 걸린 것이 있으면 **몇 건이 걸렸는지와 첫 이유**를 말한다. 「처리했습니다」만
        // 띄우면 목록에 남은 줄을 보고 화면이 안 고쳐진 것으로 읽는다.
        if (failed.Count > 0)
        {
            Say($"{done}건을 처리했습니다. {failed.Count}건은 처리하지 못했습니다 — {failed[0].Message}",
                NoticeTone.Warning);
        }
        else
        {
            Say($"{done}건을 처리했습니다.");
        }

        _picked.Clear();
        await AfterChangeAsync();
    }

    private async Task AfterChangeAsync()
    {
        await ReloadAsync();
        await OnChanged.InvokeAsync();
    }
}
