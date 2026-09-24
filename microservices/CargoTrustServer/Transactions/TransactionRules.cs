using CargoTrustServer.Common;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Transactions;

/// <summary>
/// 거래 등록 검사 (설계안 13.2 · 계약 「거래 등록 검사」).
///
/// 통계는 사람이 적어 넣은 거래에서 나온다. 한 사람이 같은 거래를 여러 번 넣거나 한 회사에
/// 몰아 넣으면 그 회사의 숫자가 통째로 기운다 — 그래서 중복은 막고, 몰아 넣기는 거절 대신
/// 표시(FLAGGED)해서 관리자가 보게 한다. 거절하면 정말 그만큼 거래한 사람이 기록을 못 남긴다.
/// </summary>
public static class TransactionRules
{
    /// <summary>같은 사람이 같은 회사에 하루에 이만큼을 넘기면 표시한다.</summary>
    public const int DailyFlagThreshold = 10;

    public const string DuplicateMessage = "같은 거래가 이미 등록돼 있습니다";

    /// <summary>입력 모양 검사(2 · 3번). 회사는 불러서 돌려준다.</summary>
    public static async Task<(Company? Company, string? Error)> ValidateAsync(
        CargoTrustDbContext db, TransactionSaveRequest req, CancellationToken ct)
    {
        if (req.CompanyId is not { } companyId)
            return (null, "거래처(companyId)를 고르세요.");

        var company = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId, ct);
        if (company is null || company.Status == CompanyStatus.HIDDEN)
            return (null, "거래처를 찾을 수 없습니다.");

        if (req.TransportDate is not { } transportDate)
            return (company, "운송일(transportDate)을 입력하세요.");
        if (req.Amount is not { } amount || amount <= 0)
            return (company, "운송료(amount)는 0보다 커야 합니다.");
        if (amount > Check.MaxAmount)
            return (company, "운송료가 너무 큽니다.");
        if (req.ExpectedPaymentDate is { } expected && expected < transportDate)
            return (company, "지급 예정일은 운송일과 같거나 뒤여야 합니다.");

        var lengthError = Check.MaxLength(Check.Clean(req.Origin), 300, "출발지")
                          ?? Check.MaxLength(Check.Clean(req.Destination), 300, "도착지")
                          ?? Check.MaxLength(Check.Clean(req.TransportType), 50, "운송 종류")
                          ?? Check.MaxLength(Check.Clean(req.DispatchChannel), 100, "배차 경로")
                          ?? Check.MaxLength(req.Memo, 10_000, "메모");
        return (company, lengthError);
    }

    /// <summary>
    /// 중복(4번) — 같은 사용자 · 회사 · 운송일 · 금액 · 출발지 · 도착지, 삭제 안 된 것.
    /// 출발지·도착지는 앞뒤 공백을 걷은 값끼리, 빈 값은 빈 값끼리 같다고 본다.
    /// </summary>
    public static Task<bool> IsDuplicateAsync(
        CargoTrustDbContext db, long userId, TransactionSaveRequest req, long? exceptId, CancellationToken ct)
    {
        var origin = Check.Clean(req.Origin);
        var destination = Check.Clean(req.Destination);
        return db.Transactions.AnyAsync(t =>
            !t.IsDeleted
            && t.UserId == userId
            && t.CompanyId == req.CompanyId
            && t.TransportDate == req.TransportDate
            && t.Amount == req.Amount
            && t.Origin == origin
            && t.Destination == destination
            && (exceptId == null || t.TransactionId != exceptId), ct);
    }

    /// <summary>
    /// 비정상 패턴(5번). 걸리면 사유를, 아니면 null.
    /// <paramref name="countToday"/> 가 true 면 「오늘 이 회사에 넣은 건수」도 본다(새로 넣을 때만).
    /// </summary>
    public static async Task<string?> FlagReasonAsync(
        CargoTrustDbContext db, long userId, long companyId, DateOnly transportDate, bool countToday, CancellationToken ct)
    {
        var reasons = new List<string>();
        var today = KstDate.Today;

        if (transportDate > today)
            reasons.Add("운송일이 오늘보다 뒤");

        if (countToday)
        {
            var since = KstDate.StartUtc(today);
            var todayCount = await db.Transactions.CountAsync(t =>
                t.UserId == userId && t.CompanyId == companyId && !t.IsDeleted && t.CreatedAt >= since, ct);
            // 이번 것까지 세어 기준을 넘는가
            if (todayCount + 1 > DailyFlagThreshold)
                reasons.Add($"같은 거래처에 하루 {DailyFlagThreshold}건 초과 등록");
        }

        return reasons.Count == 0 ? null : string.Join(" · ", reasons);
    }

    /// <summary>요청 값을 거래에 옮긴다(회사 · 사용자 · 결제 상태 제외).</summary>
    public static void Apply(CargoTransaction t, TransactionSaveRequest req)
    {
        t.CompanyId = req.CompanyId!.Value;
        t.TransportDate = req.TransportDate!.Value;
        t.Origin = Check.Clean(req.Origin);
        t.Destination = Check.Clean(req.Destination);
        t.TransportType = Check.Clean(req.TransportType);
        t.DispatchChannel = Check.Clean(req.DispatchChannel);
        t.Amount = req.Amount!.Value;
        t.ExpectedPaymentDate = req.ExpectedPaymentDate;
        t.Memo = Check.Clean(req.Memo);
    }
}
