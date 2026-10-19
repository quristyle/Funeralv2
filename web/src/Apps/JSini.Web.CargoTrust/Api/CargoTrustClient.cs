using System.Globalization;
using JSini.Web.Http;

namespace JSini.Web.CargoTrust.Api;

/// <summary>
/// CargoTrustServer 의 사용자 엔드포인트 호출(게이트웨이 /api/cargotrust). 관리자 경로(/admin/*)는 관리자 모듈이 따로 부른다.
/// 목록과 한 건은 응답 봉투만으로 구분되지 않아 GetListAsync · GetOneAsync 로 가른다. by-number 의 「없음」은 빈 결과라 null 이 된다.
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
    /// 내 거래(내 것만). open 은 상태 넷(SCHEDULED·PARTIAL·UNPAID·DISPUTE) 전체라 status 로는 못 고른다. 서버가 거르므로 목록 상한에 잘리지 않는다.
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
    /// 고른 거래를 한 번에 처리한다. 건마다 남은 금액 전액을 넣은 것과 같고, 된 것과 안 된 것이 함께 온다.
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
    /// 미지급 거래 목록. 내 것만이 아니라 누가 적었든 UNPAID 거래를 모은다(등록자 이름은 가려져 실린다).
    /// 미수금(GetReceivablesAsync)과 섞지 않는다 — 여기는 UNPAID 하나다.
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

    // ── 톨게이트 심야할인 ────────────────────────────────────

    /// <summary>지금 쓰는 할인 규칙. 목표 할인율 고르개와 「기준」 표시가 이것으로 만들어진다.</summary>
    public Task<TollRulesInfo?> GetTollRulesAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<TollRulesInfo>($"{Prefix}/toll/rules", ct);

    /// <summary>영업소 검색. 민자 구간은 기본으로 빠진다 — 요금·할인 체계가 따로다.</summary>
    public Task<IReadOnlyList<TollPlazaInfo>> SearchPlazasAsync(
        string? q, bool includePrivate = false, CancellationToken ct = default)
        => gateway.GetListAsync<TollPlazaInfo>(
            $"{Prefix}/toll/plazas?q={Uri.EscapeDataString(q ?? string.Empty)}&includePrivate={(includePrivate ? "true" : "false")}", ct);

    /// <summary>① 진입·진출 시각 → 심야할인율.</summary>
    public Task<TollCalcResultInfo?> CalcTollDiscountAsync(TollCalcRequest request, CancellationToken ct = default)
        => gateway.PostAsync<TollCalcResultInfo>($"{Prefix}/toll/discount/calc", request, ct);

    /// <summary>② 한쪽 시각 + 목표 할인율 → 나머지 시각 추천.</summary>
    public Task<TollSuggestResultInfo?> SuggestTollTimeAsync(TollSuggestRequest request, CancellationToken ct = default)
        => gateway.PostAsync<TollSuggestResultInfo>($"{Prefix}/toll/discount/suggest", request, ct);

    /// <summary>구간 통행료. 못 받아도 빈 값이 온다 — 통행료가 없다고 할인율 계산까지 막지 않는다.</summary>
    public Task<TollFareInfo?> GetTollFareAsync(
        string fromCode, string toCode, string vehicleClass, decimal discount, CancellationToken ct = default)
        => gateway.GetOneAsync<TollFareInfo>(
            $"{Prefix}/toll/fare?from={Uri.EscapeDataString(fromCode)}&to={Uri.EscapeDataString(toCode)}"
            + $"&vehicleClass={Uri.EscapeDataString(vehicleClass)}"
            + $"&discount={discount.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task<IReadOnlyList<TollCalcHistoryInfo>> GetTollHistoryAsync(int limit = 30, CancellationToken ct = default)
        => gateway.GetListAsync<TollCalcHistoryInfo>($"{Prefix}/toll/history?limit={limit}", ct);

    // ── 내 차량 ──────────────────────────────────────────────

    /// <summary>번호판 읽기. 저장하지 않는다 — 읽은 결과를 먼저 보여 주고 사람이 그 위에서 고치게 한다.</summary>
    public Task<PlateReadInfo?> ReadPlateAsync(string plateNo, CancellationToken ct = default)
        => gateway.GetOneAsync<PlateReadInfo>(
            $"{Prefix}/vehicles/plate?no={Uri.EscapeDataString(plateNo)}", ct);

    public Task<IReadOnlyList<VehicleInfo>> GetMyVehiclesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<VehicleInfo>($"{Prefix}/vehicles", ct);

    /// <summary>차량 등록. 이미 등록한 번호판이면 409.</summary>
    public Task<VehicleInfo?> CreateVehicleAsync(VehicleSaveRequest request, CancellationToken ct = default)
        => gateway.PostAsync<VehicleInfo>($"{Prefix}/vehicles", request, ct);

    public Task<VehicleInfo?> UpdateVehicleAsync(long vehicleId, VehicleSaveRequest request, CancellationToken ct = default)
        => gateway.PutAsync<VehicleInfo>($"{Prefix}/vehicles/{vehicleId}", request, ct);

    public Task DeleteVehicleAsync(long vehicleId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/vehicles/{vehicleId}", ct);

    // ── 도우미 ───────────────────────────────────────────────

    private static string Date(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string PeriodArg(int? period) =>
        period is null ? "all" : period.Value.ToString(CultureInfo.InvariantCulture);
}
