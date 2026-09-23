namespace ProjMngServer.Models;

// 이 파일이 `WbsBoardUser.cs` 였다 — 개발자 명부가 여기 있었다. 2026-09-23 에
// 그 속성들을 포털 계정으로 옮기고(`scom.account_profile_details` 의 `Dev.*`)
// 표도 화면도 걷어냈다 — `docs/projmng-account-merge.md`.

/// <summary>팀 공유 문서 한 쪽 — <c>projmng.wbs_docs</c>.</summary>
public sealed class WbsBoardDoc
{
    public int Id { get; set; }

    public string? Title { get; set; }

    /// <summary>본문 HTML. 화면의 서식 편집기가 만든 것이다.</summary>
    public string? Content { get; set; }

    public int SortOrder { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>사용자별 화면 설정 한 건 — <c>projmng.wbs_user_pref</c>.</summary>
public sealed class WbsBoardPref
{
    public string? Key { get; set; }

    /// <summary>담아 둔 값. JSON 글자 그대로다.</summary>
    public string? Value { get; set; }

    /// <summary>누구 몫으로 담겼나. 화면이 「누구의 설정인지」를 보여 준다.</summary>
    public string? Who { get; set; }
}
