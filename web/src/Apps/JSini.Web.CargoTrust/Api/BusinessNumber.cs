namespace JSini.Web.CargoTrust.Api;

/// <summary>
/// 사업자등록번호를 화면에서 먼저 따져 본다.
///
/// <para>
/// [서버가 이미 따지는데 왜 또 하나]
/// </para>
///
/// <para>
/// 서버도 같은 규칙으로 400 을 낸다(05-api-design.md 「사업자등록번호」).
/// 여기서 먼저 보는 것은 **거래처 등록 팝업의 흐름** 때문이다 — 번호를 넣으면
/// 이미 있는 회사인지부터 물어보는데(<c>by-number</c>), 자릿수가 틀린 번호로
/// 그것을 물으면 「없다」가 돌아와 사람이 새로 등록하려 들고, 그제야 저장에서
/// 번호가 틀렸다고 막힌다. 틀린 번호는 묻기 전에 막는다.
/// </para>
///
/// <para>
/// 가중치는 국세청 규칙(<c>1,3,7,1,3,7,1,3,5</c>)이다. 서버와 다르게 고치지 않는다 —
/// 어긋나면 화면은 통과시키고 서버가 막는, 원인을 알 수 없는 실패가 된다.
/// </para>
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
