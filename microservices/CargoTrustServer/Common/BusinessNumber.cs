namespace CargoTrustServer.Common;

/// <summary>
/// 사업자등록번호 — 이 서비스의 거래처 식별자다(설계안 33-3).
///
/// 저장은 숫자 10자리만 한다. 하이픈을 섞어 넣으면 같은 회사가 두 줄이 되고,
/// 그 순간 통계가 둘로 갈라진다. 그래서 받는 곳마다 여기의 <see cref="TryNormalize"/> 를 거친다.
/// </summary>
public static class BusinessNumber
{
    // 국세청 검증 규칙의 가중치 — 앞 9자리에 곱한다.
    private static readonly int[] Weights = [1, 3, 7, 1, 3, 7, 1, 3, 5];

    /// <summary>
    /// 하이픈·공백을 걷어 내고 10자리·검증 숫자를 본다.
    /// 실패하면 <paramref name="error"/> 에 사람이 읽을 이유를 싣는다.
    /// </summary>
    public static bool TryNormalize(string? raw, out string digits, out string error)
    {
        digits = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "사업자등록번호를 입력하세요.";
            return false;
        }

        var cleaned = new string(raw.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());
        if (cleaned.Length != 10 || !cleaned.All(char.IsAsciiDigit))
        {
            error = "사업자등록번호는 숫자 10자리입니다.";
            return false;
        }

        if (!IsValidCheckDigit(cleaned))
        {
            error = "사업자등록번호가 올바르지 않습니다(검증 숫자 불일치).";
            return false;
        }

        digits = cleaned;
        return true;
    }

    /// <summary>
    /// 검증 숫자 — 앞 9자리 × 가중치의 합에, 9번째 자리 × 5 의 십의 자리를 더한다.
    /// 10 에서 그 합의 일의 자리를 뺀 값(10 이면 0)이 마지막 자리와 같아야 한다.
    /// </summary>
    public static bool IsValidCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (digits[i] - '0') * Weights[i];
        sum += (digits[8] - '0') * 5 / 10;
        var check = (10 - sum % 10) % 10;
        return check == digits[9] - '0';
    }

    /// <summary>
    /// 내보낼 때의 꼴. 관리자가 아니면 뒤 5자리를 가린다(설계안 29).
    /// 검색은 전체 번호로 하므로 가려도 찾을 수 있다.
    /// </summary>
    public static string Display(string? digits, bool isAdmin)
    {
        if (string.IsNullOrEmpty(digits) || digits.Length != 10) return digits ?? string.Empty;
        return isAdmin
            ? $"{digits[..3]}-{digits[3..5]}-{digits[5..]}"
            : $"{digits[..3]}-{digits[3..5]}-*****";
    }

    /// <summary>검색어가 사업자번호 모양(숫자 10자리)이면 그 숫자를, 아니면 null.</summary>
    public static string? AsSearchKey(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return null;
        var cleaned = new string(q.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());
        return cleaned.Length == 10 && cleaned.All(char.IsAsciiDigit) ? cleaned : null;
    }
}
