namespace CargoTrustServer.Common;

/// <summary>
/// 등록자 이름을 가려서 내보낸다(「이순열」→「이*열」). 출처를 가르는 데 쓰므로 사람은 특정하지 못해도 같은 줄인지 다른 줄인지는 읽힌다. 관리자는 그대로 본다.
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
