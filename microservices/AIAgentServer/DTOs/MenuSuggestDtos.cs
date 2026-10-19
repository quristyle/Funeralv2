namespace AIAgentServer.DTOs;

/// <summary>메뉴 자동 채우기 요청. 화면이 고를 수 있는 화면·아이콘 목록을 함께 보낸다.</summary>
public sealed class MenuSuggestRequest
{
    /// <summary>사용자가 적은 설명(「계정별 알림 발송 이력」). 비면 메뉴 이름을 쓴다.</summary>
    public string? Brief { get; set; }

    /// <summary>상위 메뉴의 줄기(「시스템 › 메뉴 관리」). 이름을 고르는 데 쓴다.</summary>
    public string? Parent { get; set; }

    /// <summary>고를 수 있는 화면 — 열쇠와 주소.</summary>
    public List<MenuSuggestScreen>? Screens { get; set; }

    /// <summary>고를 수 있는 아이콘 이름(iconify).</summary>
    public List<string>? Icons { get; set; }

    /// <summary>이미 쓰는 메뉴 코드. 새 코드는 이것과 겹치면 안 된다.</summary>
    public List<string>? ExistingNames { get; set; }

    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public sealed record MenuSuggestScreen(string Key, string Path);

/// <summary>
/// 추천 결과. 목록에 없는 화면·아이콘은 서버가 걷어 낸 뒤 보낸다 — 화면은
/// 그 열쇠를 가진 것만 가리킬 수 있어야 하므로 여기서 한 번 더 막는다.
/// <c>Name</c> 은 메뉴 코드(영문 PascalCase, 기존 코드와 겹치지 않음).
/// </summary>
public sealed record MenuSuggestion(
    string? Name,
    string? TitleKey,
    string? TitleKo,
    string? TitleEn,
    string? Icon,
    string? ScreenKey);
