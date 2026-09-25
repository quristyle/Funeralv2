namespace CargoTrustServer.Common;

/// <summary>
/// 등록자 이름을 내보내는 꼴 — 사업자등록번호(<see cref="BusinessNumber.Display"/>)와
/// 같은 자리다.
///
/// <para>
/// 설계안 29 는 **사용자 이름을 비공개**로 둔다. 그런데 미지급 거래를 「누가 적었는지
/// 모르는 채」로 늘어놓으면 그 줄은 출처 없는 주장이 된다 — 이의제기를 받는 쪽도,
/// 같은 거래처를 살피는 차주도 한 사람이 열 건을 올린 것인지 열 사람이 한 건씩
/// 올린 것인지 가릴 수 없다. 그래서 **가린 이름**을 싣는다: 사람을 특정하지는
/// 못하지만 같은 줄인지 다른 줄인지는 읽힌다.
/// </para>
///
/// <para>
/// 가리는 자리는 가운데다(「이순열」→「이*열」, 「김수」→「김*」). 관리자는 그대로
/// 본다 — 신고·이의제기를 처리하려면 누가 적었는지를 알아야 한다.
/// </para>
/// </summary>
public static class PersonName
{
    /// <summary>이름이 없는 계정(X-User-Name 없이 만들어진 줄)의 자리.</summary>
    public const string Unknown = "이름 없음";

    /// <summary>내보낼 때의 꼴. 관리자가 아니면 가운데를 가린다(설계안 29).</summary>
    public static string Display(string? name, bool isAdmin)
    {
        var text = name?.Trim();
        if (string.IsNullOrEmpty(text)) return Unknown;
        if (isAdmin) return text;

        return text.Length switch
        {
            // 한 글자는 가릴 가운데가 없다. 가려 봐야 「*」 하나라 줄을 가릴 수도 없다.
            1 => text,
            2 => $"{text[0]}*",
            _ => $"{text[0]}{new string('*', text.Length - 2)}{text[^1]}",
        };
    }
}
