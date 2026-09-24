using JSini.Web.Http;

namespace JSini.Web.CargoTrust.Admin.Api;

/// <summary>
/// CargoTrustServer 의 관리자 엔드포인트. 게이트웨이의 <c>/api/cargotrust/admin/*</c> 로 나간다.
/// </summary>
/// <remarks>
/// <para>
/// 계약은 docs/cargotrust/05-api-design.md 의 「관리자 엔드포인트」절이다. 거기 적힌
/// 것만 부른다 — 화면이 편하자고 없는 엔드포인트를 지어내면 백엔드와 어긋난다.
/// </para>
///
/// <para>
/// [관리자인지 여기서 따지지 않는다]
/// </para>
///
/// <para>
/// 서버가 <c>X-User-Roles</c> 로 가리고 아니면 403 을 준다. 그 403 은
/// <see cref="ApiException"/> 이 되어 <c>DataPage</c> 가 토스트로 이유를 띄운다.
/// 화면이 한 번 더 따지면 서버의 관리자 판정(역할 설정 · <c>user_type = ADMIN</c>)과
/// 반드시 어긋난다 — 어긋나는 쪽은 늘 「들어가야 하는데 못 들어가는」 쪽이라 원인을
/// 찾기가 나쁘다.
/// </para>
///
/// <para>
/// 목록은 서버가 상한(500)에서 자르고 페이징하지 않는다. 표가 쪽을 나눈다.
/// </para>
/// </remarks>
public sealed class CargoAdminClient(GatewayClient gateway)
{
    /// <summary>게이트웨이가 CargoTrustServer 의 <c>/admin</c> 으로 넘기는 접두사.</summary>
    private const string Prefix = "cargotrust/admin";

    // ── 대시보드 · 통계 ──────────────────────────────────────

    public Task<AdminDashboard?> GetDashboardAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<AdminDashboard>($"{Prefix}/dashboard", ct);

    /// <summary>최근 <paramref name="months"/> 달의 월별 거래 · 지연 많은 거래처 · 의심 등록 사용자.</summary>
    public Task<AdminStatistics?> GetStatisticsAsync(int months, CancellationToken ct = default)
        => gateway.GetOneAsync<AdminStatistics>($"{Prefix}/statistics" + Query(("months", months)), ct);

    // ── 거래처 ───────────────────────────────────────────────

    public Task<IReadOnlyList<AdminCompany>> GetCompaniesAsync(
        string? q = null, string? status = null, CancellationToken ct = default)
        => gateway.GetListAsync<AdminCompany>(
            $"{Prefix}/companies" + Query(("q", q), ("status", status)), ct);

    public Task<AdminCompany?> CreateCompanyAsync(AdminCompanySave body, CancellationToken ct = default)
        => gateway.PostAsync<AdminCompany>($"{Prefix}/companies", body, ct);

    public Task<AdminCompany?> UpdateCompanyAsync(long id, AdminCompanySave body, CancellationToken ct = default)
        => gateway.PutAsync<AdminCompany>($"{Prefix}/companies/{id}", body, ct);

    // ── 거래 · 결제 ──────────────────────────────────────────

    public Task<IReadOnlyList<AdminTransaction>> GetTransactionsAsync(
        string? q, string? paymentStatus, string? reviewStatus, DateTime? from, DateTime? to,
        CancellationToken ct = default)
        => gateway.GetListAsync<AdminTransaction>(
            $"{Prefix}/transactions" + Query(
                ("q", q),
                ("paymentStatus", paymentStatus),
                ("reviewStatus", reviewStatus),
                ("from", CargoAdminFormat.QueryDay(from)),
                ("to", CargoAdminFormat.QueryDay(to))), ct);

    public Task<AdminTransaction?> UpdateTransactionAsync(
        long id, AdminTransactionUpdate body, CancellationToken ct = default)
        => gateway.PutAsync<AdminTransaction>($"{Prefix}/transactions/{id}", body, ct);

    public Task<IReadOnlyList<AdminPayment>> GetPaymentsAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
        => gateway.GetListAsync<AdminPayment>(
            $"{Prefix}/payments" + Query(
                ("from", CargoAdminFormat.QueryDay(from)),
                ("to", CargoAdminFormat.QueryDay(to))), ct);

    // ── 후기 · 신고 · 이의제기 ───────────────────────────────

    public Task<IReadOnlyList<AdminReview>> GetReviewsAsync(string? status, CancellationToken ct = default)
        => gateway.GetListAsync<AdminReview>($"{Prefix}/reviews" + Query(("status", status)), ct);

    /// <summary>후기를 숨기거나 되살린다 — <c>VISIBLE</c> · <c>HIDDEN</c>.</summary>
    public Task<AdminReview?> SetReviewStatusAsync(long id, string status, CancellationToken ct = default)
        => gateway.PutAsync<AdminReview>($"{Prefix}/reviews/{id}", new { status }, ct);

    public Task<IReadOnlyList<AdminReport>> GetReportsAsync(string? status, CancellationToken ct = default)
        => gateway.GetListAsync<AdminReport>($"{Prefix}/reports" + Query(("status", status)), ct);

    public Task<AdminReport?> ResolveReportAsync(long id, AdminResolve body, CancellationToken ct = default)
        => gateway.PutAsync<AdminReport>($"{Prefix}/reports/{id}", body, ct);

    public Task<IReadOnlyList<AdminDispute>> GetDisputesAsync(string? status, CancellationToken ct = default)
        => gateway.GetListAsync<AdminDispute>($"{Prefix}/disputes" + Query(("status", status)), ct);

    public Task<AdminDispute?> ResolveDisputeAsync(long id, AdminResolve body, CancellationToken ct = default)
        => gateway.PutAsync<AdminDispute>($"{Prefix}/disputes/{id}", body, ct);

    // ── 사용자 ───────────────────────────────────────────────

    public Task<IReadOnlyList<AdminUser>> GetUsersAsync(
        string? q = null, string? userType = null, CancellationToken ct = default)
        => gateway.GetListAsync<AdminUser>(
            $"{Prefix}/users" + Query(("q", q), ("userType", userType)), ct);

    public Task<AdminUser?> UpdateUserAsync(long id, AdminUserUpdate body, CancellationToken ct = default)
        => gateway.PutAsync<AdminUser>($"{Prefix}/users/{id}", body, ct);

    // ── 감사 기록 ────────────────────────────────────────────

    public Task<IReadOnlyList<AdminAuditEntry>> GetAuditAsync(
        string? targetType, DateTime? from, DateTime? to, CancellationToken ct = default)
        => gateway.GetListAsync<AdminAuditEntry>(
            $"{Prefix}/audit" + Query(
                ("targetType", targetType),
                ("from", CargoAdminFormat.QueryDay(from)),
                ("to", CargoAdminFormat.QueryDay(to))), ct);

    /// <summary>
    /// 쿼리스트링. 값이 없거나 빈 글자면 뺀다 — 빈 <c>status=</c> 를 실으면 서버가
    /// 「상태가 빈 글자인 것」을 찾아 빈 목록을 줄 수 있다.
    /// </summary>
    private static string Query(params (string Key, object? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null && !(p.Value is string s && string.IsNullOrWhiteSpace(s)))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!.Trim())}")
            .ToList();

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
