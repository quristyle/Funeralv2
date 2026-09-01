namespace AuthServer.DTOs;

/// <summary>
/// 플레이어 릴리스 설정. <c>appsettings.json</c> 의 <c>GitHub</c> 절을 읽는다.
/// </summary>
/// <remarks>
/// <b>토큰은 <c>appsettings.Local.json</c>(git 제외)에만 둔다.</b>
/// 추적 파일에는 자리표시자만 있고, 값이 없으면 화면이 "설정이 필요하다"고 안내한다 —
/// 기동을 막지는 않는다. 이 기능을 안 쓰는 환경(개발 PC)에서도 서버는 떠야 한다.
/// </remarks>
public class GitHubOptions
{
    /// <summary>저장소 소유자 (예: <c>quristyle</c>)</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>저장소 이름 (예: <c>Funeralv2</c>)</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>
    /// 개인용 액세스 토큰. <c>repo</c> 와 <c>workflow</c> 권한이 필요하다.
    /// 태그를 만들려면 <c>contents:write</c>, 워크플로를 보려면 <c>actions:read</c> 다.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>태그를 붙일 기준 브랜치</summary>
    public string Branch { get; set; } = "main";

    /// <summary>설정이 실제로 채워져 있는가. 자리표시자는 없는 것으로 본다.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Owner) &&
        !string.IsNullOrWhiteSpace(Repo) &&
        !string.IsNullOrWhiteSpace(Token) &&
        !Token.StartsWith("__SET_IN_", StringComparison.Ordinal);
}

/// <summary>릴리스 화면이 처음 그릴 때 필요한 것들</summary>
public class PlayerReleaseStatusDto
{
    /// <summary>서버에 GitHub 설정(특히 토큰)이 갖춰져 있는가</summary>
    public bool Configured { get; set; }

    /// <summary>설정이 없을 때 화면에 띄울 안내</summary>
    public string? SetupHint { get; set; }

    /// <summary>이 사용자가 릴리스를 낼 수 있는가 (<c>can_create</c>)</summary>
    public bool CanRelease { get; set; }

    /// <summary>저장소 표시용 (<c>owner/repo</c>)</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>태그를 붙일 브랜치</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>그 브랜치의 최신 커밋. 화면이 "무엇을 릴리스하는지" 보여 준다.</summary>
    public string? HeadSha { get; set; }

    /// <summary>최신 커밋 메시지 첫 줄</summary>
    public string? HeadMessage { get; set; }

    /// <summary>이미 나가 있는 버전 태그. 중복 입력을 막고 다음 번호를 짐작하게 한다.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>가장 최근 릴리스 태그. 아직 없으면 null.</summary>
    public string? LatestRelease { get; set; }

    /// <summary>다음 버전 제안. 최신 태그의 끝자리를 하나 올린 값이다.</summary>
    public string? SuggestedVersion { get; set; }

    /// <summary>조회 중 생긴 문제(설정은 됐는데 GitHub 이 응답하지 않는 등)</summary>
    public string? Warning { get; set; }
}

/// <summary>릴리스 요청</summary>
public class PlayerReleaseRequestDto
{
    /// <summary>버전. <c>1.0.0</c> 형식으로 받는다. 앞의 <c>v</c> 는 서버가 붙인다.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 릴리스 노트. 비우면 워크플로의 <c>generate_release_notes</c> 가 만든다.
    /// (지금은 태그만 만들므로 이 값은 기록용이다 — 아래 주석 참고)
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>릴리스 요청 결과</summary>
public class PlayerReleaseResultDto
{
    /// <summary>만들어진 태그 (<c>v1.0.0</c>)</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>태그가 가리키는 커밋</summary>
    public string Sha { get; set; } = string.Empty;

    /// <summary>사람에게 보여 줄 말</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>워크플로 실행 한 건의 상태. 화면이 이것을 폴링한다.</summary>
public class PlayerReleaseRunDto
{
    /// <summary>보고 있는 태그</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// 아직 실행을 못 찾았는가.
    ///
    /// 태그를 만든 직후에는 GitHub 이 워크플로를 큐에 넣는 데 몇 초가 걸린다.
    /// 그 사이를 '실패'로 보이지 않게 구분해 둔다.
    /// </summary>
    public bool Pending { get; set; }

    /// <summary>실행 번호 (<c>#12</c>)</summary>
    public int? RunNumber { get; set; }

    /// <summary><c>queued</c> · <c>in_progress</c> · <c>completed</c></summary>
    public string? Status { get; set; }

    /// <summary><c>success</c> · <c>failure</c> · <c>cancelled</c> …</summary>
    public string? Conclusion { get; set; }

    /// <summary>GitHub 실행 화면 주소</summary>
    public string? HtmlUrl { get; set; }

    /// <summary>갈래별 상태</summary>
    public List<PlayerReleaseJobDto> Jobs { get; set; } = new();

    /// <summary>끝났고 릴리스가 발행됐으면 그 주소</summary>
    public string? ReleaseUrl { get; set; }
}

/// <summary>워크플로 갈래(job) 하나</summary>
public class PlayerReleaseJobDto
{
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Conclusion { get; set; }

    /// <summary>지금 돌고 있는 단계 이름. 끝났으면 null.</summary>
    public string? CurrentStep { get; set; }
}
