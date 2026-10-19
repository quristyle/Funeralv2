namespace JSini.Web.CargoTrust.Api;

/// <summary>
/// 거래처 등록에서 서버에 묻기 전에 번호를 먼저 따진다. 가중치는 서버와 같아야 한다 — 어긋나면 화면만 통과시킨다.
/// </summary>
public static class BusinessNumber
{
    private static readonly int[] Weights = [1, 3, 7, 1, 3, 7, 1, 3, 5];

    /// <summary>숫자만 남긴다. 하이픈·공백을 섞어 붙여 넣는 경우가 대부분이다.</summary>
    public static string Digits(string? value) =>
        new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    /// <summary>10자리이고 검증 숫자가 맞는가.</summary>
    public static bool IsValid(string? value)
    {
        var d = Digits(value);
        if (d.Length != 10)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (d[i] - '0') * Weights[i];
        }

        // 아홉째 자리는 가중치 5 를 곱한 값의 십의 자리를 한 번 더 더한다.
        sum += (d[8] - '0') * 5 / 10;

        var check = (10 - sum % 10) % 10;
        return check == d[9] - '0';
    }

    /// <summary>「123-45-67890」 꼴. 10자리가 아니면 받은 그대로 돌려준다.</summary>
    public static string Format(string? value)
    {
        var d = Digits(value);
        return d.Length == 10 ? $"{d[..3]}-{d[3..5]}-{d[5..]}" : value ?? string.Empty;
    }
}
