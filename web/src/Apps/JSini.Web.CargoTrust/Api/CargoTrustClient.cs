using System.Globalization;
using JSini.Web.Http;

namespace JSini.Web.CargoTrust.Api;

/// <summary>
/// CargoTrustServer 호출 — 사용자(차주·운송사) 엔드포인트만. 게이트웨이의
/// <c>/api/cargotrust</c> 아래로 나간다.
///
/// <para>
/// 경로·필드는 docs/cargotrust/05-api-design.md 「사용자 엔드포인트」 그대로다.
/// <c>/admin/*</c> 은 여기 없다 — 관리자 모듈이 따로 부른다. 여기 두면 차주
/// 화면 어딘가에서 불렀을 때 403 이 나는 것 말고는 막아 주는 것이 없다.
/// </para>
///
/// <para>
/// [목록·한 건을 메서드로 가른다]
/// </para>
///
/// <para>
/// 서버는 모든 응답을 <c>{data:{result:[…]}}</c> 봉투에 싣는다. 목록인지 한 건인지는
/// 봉투만으로 알 수 없어서 <see cref="GatewayClient"/> 의 이름으로 고른다
/// (<c>GetListAsync</c> · <c>GetOneAsync</c>). <c>by-number</c> 는 「없음」이
/// 빈 결과로 오므로 한 건 읽기가 그대로 <c>null</c> 을 준다.
/// </para>
/// </summary>
public sealed class CargoTrustClient(GatewayClient gateway)
{
    /// <summary>게이트웨이가 이 서비스로 라우팅하는 접두사.</summary>
    private const string Prefix = "cargotrust";

    // ── 나 · 홈 ──────────────────────────────────────────────

    public Task<MeInfo?> GetMeAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<MeInfo>($"{Prefix}/me", ct);

    public Task<HomeInfo?> GetHomeAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<HomeInfo>($"{Prefix}/home", ct);

    // ── 거래처 ───────────────────────────────────────────────

    /// <summary>
    /// 거래처 검색(≤50). <paramref name="field"/>: <c>all</c> · <c>name</c> · <c>bizno</c> ·
    /// <c>ceo</c> · <c>phone</c> · <c>address</c>.
    /// </summary>
    public Task<IReadOnlyList<CompanySummary>> SearchCompaniesAsync(
        string q, string? field = null, CancellationToken ct = default)
        => gateway.GetListAsync<CompanySummary>(
            $"{Prefix}/companies/search?q={Uri.EscapeDataString(q)}&field={Uri.EscapeDataString(field ?? "all")}", ct);

    /// <summary>등록 전 중복 확인. 없으면 <c>null</c>.</summary>
    public Task<CompanyInfo?> GetCompanyByNumberAsync(string businessNumber, CancellationToken ct = default)
        => gateway.GetOneAsync<CompanyInfo>(
            $"{Prefix}/companies/by-number/{Uri.EscapeDataString(BusinessNumber.Digits(businessNumber))}", ct);

    /// <summary>
    /// 거래처 상세. <paramref name="period"/> 는 30·90·180·365, <c>null</c> 이면 전체.
    /// 부를 때마다 서버가 「최근 본 거래처」를 갱신한다.
    /// </summary>
    public Task<CompanyDetail?> GetCompanyAsync(long companyId, int? period, CancellationToken ct = default)
        => gateway.GetOneAsync<CompanyDetail>(
            $"{Prefix}/companies/{companyId}?period={PeriodArg(period)}", ct);

    public Task<IReadOnlyList<PublicReview>> GetCompanyReviewsAsync(long companyId, CancellationToken ct = default)
        => gateway.GetListAsync<PublicReview>($"{Prefix}/companies/{companyId}/reviews", ct);

    /// <summary>거래처 등록. 이미 있는 번호면 409(「이미 등록된 사업자번호입니다」).</summary>
    public Task<CompanyInfo?> CreateCompanyAsync(CompanyCreateRequest request, CancellationToken ct = default)
        => gateway.PostAsync<CompanyInfo>($"{Prefix}/companies", request, ct);

    // ── 내 거래 ──────────────────────────────────────────────

    /// <summary>
    /// 내 거래(내 것만). 조건은 모두 고를 수 있다.
    ///
    /// <para>
    /// <paramref name="open"/> 은 「아직 처리가 안 된 것」이다 — 상태 넷
    /// (<c>SCHEDULED·PARTIAL·UNPAID·DISPUTE</c>)이라 <paramref name="status"/> 하나로는
    /// 못 고른다. 서버가 거르므로 목록 상한에 옛 미처리가 잘리지 않는다.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<MyTransaction>> GetMyTransactionsAsync(
        string? status = null, DateOnly? from = null, DateOnly? to = null, long? companyId = null,
        bool open = false, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        if (from is not null)
        {
            query.Add($"from={Date(from.Value)}");
        }

        if (to is not null)
        {
            query.Add($"to={Date(to.Value)}");
        }

        if (companyId is not null)
        {
            query.Add($"companyId={companyId}");
        }

        if (open)
        {
            query.Add("open=true");
        }

        var path = query.Count == 0
            ? $"{Prefix}/transactions"
            : $"{Prefix}/transactions?{string.Join('&', query)}";

        return gateway.GetListAsync<MyTransaction>(path, ct);
    }

    public Task<TransactionDetail?> GetTransactionAsync(long transactionId, CancellationToken ct = default)
        => gateway.GetOneAsync<TransactionDetail>($"{Prefix}/transactions/{transactionId}", ct);

    public Task<MyTransaction?> CreateTransactionAsync(TransactionSaveRequest request, CancellationToken ct = default)
        => gateway.PostAsync<MyTransaction>($"{Prefix}/transactions", request, ct);

    public Task<MyTransaction?> UpdateTransactionAsync(
        long transactionId, TransactionSaveRequest request, CancellationToken ct = default)
        => gateway.PutAsync<MyTransaction>($"{Prefix}/transactions/{transactionId}", request, ct);

    /// <summary>논리 삭제. 통계에서 빠진다.</summary>
    public Task DeleteTransactionAsync(long transactionId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/transactions/{transactionId}", ct);

    /// <summary>결제 결과 등록. 상태(정상·지연·일부)는 서버가 정해 바뀐 거래를 돌려준다.</summary>
    public Task<MyTransaction?> RegisterPaymentAsync(
        long transactionId, PaymentRequest request, CancellationToken ct = default)
        => gateway.PostAsync<MyTransaction>($"{Prefix}/transactions/{transactionId}/payment", request, ct);

    /// <summary>
    /// 고른 거래를 한 번에 처리한다. 건마다 <b>남은 금액 전액</b>을 넣은 것과 같고,
    /// 된 것과 안 된 것이 함께 온다 — 한 건이 걸려도 나머지는 처리된다.
    /// </summary>
    public Task<BulkPaymentResult?> RegisterPaymentsAsync(
        BulkPaymentRequest request, CancellationToken ct = default)
        => gateway.PostAsync<BulkPaymentResult>($"{Prefix}/transactions/payments", request, ct);

    /// <summary>후기 저장. 거래당 하나라 이미 있으면 고친다.</summary>
    public Task<ReviewInfo?> SaveReviewAsync(long transactionId, string content, CancellationToken ct = default)
        => gateway.PostAsync<ReviewInfo>(
            $"{Prefix}/transactions/{transactionId}/review", new ReviewSaveRequest { Content = content }, ct);

    // ── 미수금 ───────────────────────────────────────────────

    public Task<ReceivablesInfo?> GetReceivablesAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<ReceivablesInfo>($"{Prefix}/receivables", ct);

    // ── 미지급 거래 ──────────────────────────────────────────

    /// <summary>
    /// 미지급 거래 목록. <b>내 것만이 아니다</b> — 누가 적었든 <c>UNPAID</c> 로 남은
    /// 거래를 모아 준다(줄마다 등록자가 가린 이름으로 실린다).
    ///
    /// <para>
    /// 미수금(<see cref="GetReceivablesAsync"/>)과 섞지 않는다. 그쪽은 내가 못 받은
    /// 돈이고 상태 넷을 담지만, 여기는 <c>UNPAID</c> 하나다.
    /// </para>
    /// </summary>
    public Task<UnpaidList?> GetUnpaidTransactionsAsync(
        long? companyId = null, string? q = null, DateOnly? from = null, DateOnly? to = null,
        bool mine = false, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (companyId is not null)
        {
            query.Add($"companyId={companyId}");
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query.Add($"q={Uri.EscapeDataString(q.Trim())}");
        }

        if (from is not null)
        {
            query.Add($"from={Date(from.Value)}");
        }

        if (to is not null)
        {
            query.Add($"to={Date(to.Value)}");
        }

        if (mine)
        {
            query.Add("mine=true");
        }

        var path = query.Count == 0
            ? $"{Prefix}/unpaid-transactions"
            : $"{Prefix}/unpaid-transactions?{string.Join('&', query)}";

        return gateway.GetOneAsync<UnpaidList>(path, ct);
    }

    // ── 신고 ─────────────────────────────────────────────────

    /// <summary>신고. 같은 대상에 처리 안 된 내 신고가 있으면 409.</summary>
    public Task<ReportInfo?> CreateReportAsync(ReportRequest request, CancellationToken ct = default)
        => gateway.PostAsync<ReportInfo>($"{Prefix}/reports", request, ct);

    public Task<IReadOnlyList<ReportInfo>> GetMyReportsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<ReportInfo>($"{Prefix}/reports/mine", ct);

    // ── 이의제기 (운송사) ────────────────────────────────────

    /// <summary>자기 회사에 관한 거래. 회사에 연결된 운송사 계정만 부를 수 있다(아니면 403).</summary>
    public Task<IReadOnlyList<PublicTransaction>> GetCompanyTransactionsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<PublicTransaction>($"{Prefix}/company-transactions", ct);

    /// <summary>이의제기. 같은 거래에 처리 안 된 이의제기가 있으면 409.</summary>
    public Task<DisputeInfo?> CreateDisputeAsync(DisputeRequest request, CancellationToken ct = default)
        => gateway.PostAsync<DisputeInfo>($"{Prefix}/disputes", request, ct);

    public Task<IReadOnlyList<DisputeInfo>> GetMyDisputesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<DisputeInfo>($"{Prefix}/disputes/mine", ct);

    // ── 도우미 ───────────────────────────────────────────────

    private static string Date(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string PeriodArg(int? period) =>
        period is null ? "all" : period.Value.ToString(CultureInfo.InvariantCulture);
}
