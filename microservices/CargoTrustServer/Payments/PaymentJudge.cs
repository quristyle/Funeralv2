using CargoTrustServer.Common;
using CargoTrustServer.Data;

namespace CargoTrustServer.Payments;

/// <summary>
/// 결제 판정 — 계약 「결제 판정」 그대로.
///
/// 거래 줄의 payment_status 는 「지금」이고, 판정할 때마다 payment_record 에 한 줄이 쌓인다.
/// 운송과 결제는 별개의 사건이라(설계안 9) 받은 금액은 덮어쓰지 않고 **쌓는다**.
/// </summary>
public static class PaymentJudge
{
    /// <summary>
    /// 판정해서 거래를 바꾸고 남길 기록을 돌려준다. 입력이 틀리면 <paramref name="error"/>.
    /// </summary>
    public static PaymentRecord? Apply(CargoTransaction t, PaymentRequest req, long userId, out string? error)
    {
        error = null;
        if (!Code.TryParse<PaymentStatus>(req.Result, out var result)
            || result is not (null or PaymentStatus.UNPAID or PaymentStatus.DISPUTE))
        {
            error = "result 는 비우거나 UNPAID · DISPUTE 중 하나입니다.";
            return null;
        }
        if (Check.MaxLength(req.Memo, 10_000, "메모") is { } memoError)
        {
            error = memoError;
            return null;
        }

        var record = new PaymentRecord
        {
            TransactionId = t.TransactionId,
            UserId = userId,
            PaidDate = req.PaidDate,
            Memo = Check.Clean(req.Memo),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (result is PaymentStatus.UNPAID or PaymentStatus.DISPUTE)
        {
            // 받은 금액·날짜는 그대로 둔다 — 「못 받았다」는 판정이지 받은 것을 지우는 일이 아니다.
            t.PaymentStatus = result.Value;
            record.PaidAmount = 0m;
            record.ResultStatus = result.Value;
            record.DelayDays = req.PaidDate is { } d && t.ExpectedPaymentDate is { } e
                ? Math.Max(0, d.DayNumber - e.DayNumber)
                : null;
        }
        else
        {
            if (req.PaidAmount is not { } paid || paid <= 0)
            {
                error = "받은 금액(paidAmount)은 0보다 커야 합니다.";
                return null;
            }
            if (req.PaidDate is not { } paidDate)
            {
                error = "받은 날짜(paidDate)를 입력하세요.";
                return null;
            }
            if (t.PaidAmount + paid > Check.MaxAmount)
            {
                error = "금액이 너무 큽니다.";
                return null;
            }

            t.PaidAmount += paid;
            t.ActualPaymentDate = paidDate;
            t.PaymentStatus = Judge(t);

            record.PaidAmount = paid;
            record.ResultStatus = t.PaymentStatus;
            record.DelayDays = t.ExpectedPaymentDate is { } e ? Math.Max(0, paidDate.DayNumber - e.DayNumber) : null;
        }

        t.UpdatedAt = DateTimeOffset.UtcNow;
        return record;
    }

    /// <summary>
    /// 쌓인 금액과 마지막 지급일로 상태를 정한다.
    /// 누적 ≥ 운송료 → 예정일이 없거나 지급일 ≤ 예정일이면 PAID, 아니면 DELAYED. 모자라면 PARTIAL.
    /// </summary>
    public static PaymentStatus Judge(CargoTransaction t)
    {
        if (t.PaidAmount < t.Amount) return PaymentStatus.PARTIAL;
        return t.ExpectedPaymentDate is not { } expected
               || t.ActualPaymentDate is not { } actual
               || actual <= expected
            ? PaymentStatus.PAID
            : PaymentStatus.DELAYED;
    }

    /// <summary>
    /// 거래를 고친 뒤(운송료·예정일이 바뀌었을 때) 받은 기록이 있으면 다시 판정한다.
    /// UNPAID · DISPUTE 는 사람이 내린 판정이라 건드리지 않는다.
    /// </summary>
    public static void Recalculate(CargoTransaction t)
    {
        if (t.PaidAmount <= 0) return;
        if (t.PaymentStatus is PaymentStatus.UNPAID or PaymentStatus.DISPUTE) return;
        t.PaymentStatus = Judge(t);
    }
}
