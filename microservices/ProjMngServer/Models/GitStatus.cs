namespace ProjMngServer.Models;

// Git 저장소에서 읽어 오는 것들. **우리 DB 에 담지 않는다** — 잠깐 들고
// 있다가 버린다(TTL).
//
// [GitLab 에서 GitHub 로 갈아탔다 (2026-09-23)]
//
// 사내망에서는 GitLab 이었고 저장소를 `pmm001`…`pmm018` × `be`/`fe` 규칙으로
// **만들어 냈다.** GitHub 로 오면서 그 규칙이 뜻을 잃어(저장소가 하나다)
// **설정이 목록을 준다.** 옛 구현은 git 이력에 있다.

/// <summary>설정이 없을 때도 화면이 뜨게 하는 겉봉.</summary>
/// <typeparam name="T">줄 하나의 모양.</typeparam>
public sealed class GitResult<T>
{
    /// <summary>볼 저장소가 하나라도 있나. 거짓이면 줄이 없다.</summary>
    public bool Configured { get; set; }

    /// <summary>
    /// 토큰이 있나. <b>없어도 공개 저장소는 읽힌다</b> — 시간당 60회가
    /// 5,000회로 바뀔 뿐이다.
    /// </summary>
    public bool Authenticated { get; set; }

    public string? WebBaseUrl { get; set; }

    /// <summary>마지막으로 읽어 온 시각. 캐시라 지금이 아닐 수 있다.</summary>
    public string? LoadedAt { get; set; }

    /// <summary>
    /// 남은 호출 한도. 토큰 없이 쓰면 금세 바닥나므로 화면이 보여 준다 —
    /// 「왜 갑자기 안 보이나」의 답이 대개 이것이다.
    /// </summary>
    public int? RateRemaining { get; set; }

    /// <inheritdoc cref="RateRemaining"/>
    public int? RateLimit { get; set; }

    public List<T> Rows { get; set; } = [];

    /// <summary>
    /// 토큰이 없거나 권한이 모자라 <b>못 본 항목</b>. 화면이 그대로 보여 준다.
    /// </summary>
    /// <remarks>
    /// 말없이 비워 두면 「그 저장소는 원래 방문자가 0 인가 보다」로 읽힌다.
    /// <b>못 본 것과 없는 것은 다르다.</b>
    /// </remarks>
    public List<GitBlocked> Blocked { get; set; } = [];
}

/// <summary>못 본 항목 하나.</summary>
public sealed class GitBlocked
{
    /// <summary>사람이 읽는 이름 — 「방문·클론 통계」.</summary>
    public string? What { get; set; }

    /// <summary>왜 못 봤나.</summary>
    public string? Why { get; set; }

    /// <summary>무엇을 주면 보이나 — 「Administration: Read」.</summary>
    public string? Needs { get; set; }
}

/// <summary>빌드 상태 한 줄 — 저장소 하나의 가장 최근 Actions 실행.</summary>
public sealed class GitRunRow
{
    /// <summary><c>owner/name</c>.</summary>
    public string? Repo { get; set; }

    public string? RunsUrl { get; set; }

    /// <summary>
    /// 한 낱말로 줄인 상태 — <c>success</c>·<c>failure</c>·<c>cancelled</c>·
    /// <c>skipped</c>·<c>queued</c>·<c>in_progress</c>.
    /// </summary>
    /// <remarks>
    /// GitHub 는 이것을 <b>둘로 나눠 준다</b> — 끝났는지(<c>status</c>)와
    /// 어떻게 끝났는지(<c>conclusion</c>). 화면이 그 둘을 다시 조합하게 두면
    /// 화면마다 규칙이 갈리므로 여기서 하나로 만든다.
    /// </remarks>
    public string? Status { get; set; }

    /// <summary>
    /// 실행이 <b>한 번도 없었다</b>는 뜻의 <c>none</c> 이 들어올 수 있다.
    /// <see cref="Error"/>(못 읽었다)와 다르다.
    /// </summary>
    public string? Error { get; set; }

    public long? RunId { get; set; }

    /// <summary>워크플로 이름(<c>ci</c>·<c>deploy</c>).</summary>
    public string? WorkflowName { get; set; }

    /// <summary>무엇이 일으켰나 — <c>push</c>·<c>pull_request</c>·<c>schedule</c>.</summary>
    public string? Event { get; set; }

    public string? Branch { get; set; }
    public string? StartedAt { get; set; }
    public string? UpdatedAt { get; set; }

    /// <summary>걸린 시간(초). 시작과 끝의 차다 — GitHub 가 따로 주지 않는다.</summary>
    public double? Duration { get; set; }

    public string? RunUrl { get; set; }

    public string? CommitId { get; set; }
    public string? CommitTitle { get; set; }
    public string? CommitAuthor { get; set; }

    /// <summary>실행을 일으킨 계정.</summary>
    public string? Actor { get; set; }

    /// <summary>마지막으로 성공한 실행. 지금 깨져 있어도 <b>언제까지 됐는지</b>가 보인다.</summary>
    public GitRunBrief? LastSuccess { get; set; }
}

/// <summary>실행 한 건의 요약.</summary>
public sealed class GitRunBrief
{
    public string? Status { get; set; }
    public string? WorkflowName { get; set; }
    public string? Branch { get; set; }
    public string? At { get; set; }
    public double? Duration { get; set; }
    public string? Url { get; set; }
}

/// <summary>모니터링 한 줄 — 저장소 하나의 종합.</summary>
public sealed class GitMonitorRow
{
    public string? Repo { get; set; }
    public string? WebUrl { get; set; }
    public string? RunsUrl { get; set; }
    public string? Error { get; set; }

    public string? Description { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Language { get; set; }
    public string? Visibility { get; set; }

    /// <summary>마지막으로 무엇이든 밀어 넣은 때.</summary>
    public string? PushedAt { get; set; }

    public string? CreatedAt { get; set; }

    public long? OpenIssues { get; set; }
    public long? Stars { get; set; }
    public long? Forks { get; set; }

    /// <summary>저장소 크기(KB). GitHub 가 KB 로 준다.</summary>
    public long? SizeKb { get; set; }

    public int Branches { get; set; }

    /// <summary>
    /// 오래 손대지 않은 가지. <b>보이는 가지만 세어 본 값</b>이다 —
    /// GitHub 는 가지 목록에 날짜를 주지 않아 하나씩 더 물어야 한다.
    /// </summary>
    public int StaleBranches { get; set; }

    /// <summary>날짜를 실제로 물어본 가지 수. 위 <see cref="StaleBranches"/> 의 분모다.</summary>
    public int BranchesChecked { get; set; }

    public List<GitBranch> BranchList { get; set; } = [];

    public int? Tags { get; set; }

    public int OpenPrs { get; set; }
    public List<GitPr> PrList { get; set; } = [];

    public GitRunStats? Runs { get; set; }

    public int Commits7d { get; set; }

    /// <summary>
    /// 위 건수가 <b>한 쪽 상한에 걸려 잘렸나</b>. 참이면 실제로는 더 많다.
    /// </summary>
    /// <remarks>
    /// GitHub 는 한 번에 100건까지 준다. 잘린 값을 그냥 보여 주면 「7일에 딱
    /// 100건」이라는 있을 법한 숫자로 읽혀 <b>아무도 의심하지 않는다.</b>
    /// 화면은 이 값이 참일 때 <c>100+</c> 로 적는다.
    /// </remarks>
    public bool Commits7dCapped { get; set; }

    public List<GitNameCount> CommitAuthors7d { get; set; } = [];
    public GitCommit? LastCommit { get; set; }

    public int Contributors { get; set; }
    public List<GitNameCount> ContributorList { get; set; } = [];

    /// <summary>방문·클론 통계(최근 14일). <b>토큰에 쓰기 권한이 있어야 보인다.</b></summary>
    public GitTraffic? Traffic { get; set; }

    /// <summary>릴리스. 토큰 없이도 보인다.</summary>
    public List<GitRelease> Releases { get; set; } = [];

    /// <summary>이 저장소에서 온 GHCR 이미지. <b>토큰이 있어야 보인다.</b></summary>
    public List<GitPackage> Packages { get; set; } = [];

    /// <summary>
    /// 가장 최근에 깨진 실행의 <b>어느 단계에서</b> 깨졌나.
    /// GitHub 까지 가지 않고 원인 자리를 본다.
    /// </summary>
    public GitFailure? LastFailure { get; set; }
}

/// <summary>방문·클론 통계(최근 14일).</summary>
public sealed class GitTraffic
{
    public int Views { get; set; }

    /// <summary>같은 사람을 한 번으로 센 값. 이쪽이 실제 사람 수에 가깝다.</summary>
    public int UniqueViews { get; set; }

    public int Clones { get; set; }

    /// <inheritdoc cref="UniqueViews"/>
    public int UniqueClones { get; set; }

    /// <summary>많이 본 경로.</summary>
    public List<GitNameCount> TopPaths { get; set; } = [];
}

/// <summary>릴리스 하나.</summary>
public sealed class GitRelease
{
    public string? TagName { get; set; }
    public string? Name { get; set; }
    public string? PublishedAt { get; set; }
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public string? Author { get; set; }
    public string? Url { get; set; }
}

/// <summary>깨진 실행 하나의 원인 자리.</summary>
public sealed class GitFailure
{
    public long? RunId { get; set; }
    public string? WorkflowName { get; set; }
    public string? Branch { get; set; }
    public string? At { get; set; }
    public string? Url { get; set; }

    /// <summary>깨진 잡과 단계 — <c>build / dotnet test</c>.</summary>
    public List<string> Steps { get; set; } = [];
}

/// <summary>GHCR 컨테이너 이미지 하나.</summary>
/// <remarks>
/// 저장소가 아니라 <b>소유자</b>에 매달린다. 배포가 올리는 이미지 열둘이
/// 여기 보인다 — 운영에 떠 있는 태그와 대조하는 자리다.
/// </remarks>
public sealed class GitPackage
{
    public string? Name { get; set; }
    public long Versions { get; set; }

    /// <summary>가장 최근 판의 태그들.</summary>
    public string? LatestTags { get; set; }

    public string? UpdatedAt { get; set; }
    public string? Url { get; set; }
}

/// <summary>가지 하나.</summary>
public sealed class GitBranch
{
    public string? Name { get; set; }
    public bool Default { get; set; }
    public bool Protected { get; set; }

    /// <summary>마지막 커밋 시각. <b>안 물어본 가지는 비어 있다.</b></summary>
    public string? At { get; set; }

    public bool Stale { get; set; }
}

/// <summary>열려 있는 풀 리퀘스트 하나.</summary>
public sealed class GitPr
{
    public long? Number { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Source { get; set; }
    public string? Target { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public bool Draft { get; set; }
    public string? WebUrl { get; set; }
}

/// <summary>최근 실행 몇 개를 훑어 낸 품질 지표.</summary>
public sealed class GitRunStats
{
    /// <summary>훑은 개수. <b>전부가 아니다</b> — 설정이 정한 표본이다.</summary>
    public int Sampled { get; set; }

    /// <summary>저장소에 쌓인 전체 실행 수. GitHub 가 세어 준다.</summary>
    public int Total { get; set; }

    public int Success { get; set; }
    public int Failure { get; set; }
    public int Other { get; set; }

    /// <summary>성공률(%). <b>끝난 것만 분모다</b> — 돌고 있는 것을 넣으면 값이 출렁인다.</summary>
    public double? Rate { get; set; }

    public double? AvgDuration { get; set; }

    /// <summary>최근 순서대로의 상태. 화면이 작은 띠로 그린다.</summary>
    public List<string?> Recent { get; set; } = [];

    public GitRunBrief? Last { get; set; }
    public GitRunBrief? LastSuccess { get; set; }
    public GitRunBrief? LastFailure { get; set; }
}

/// <summary>커밋 하나.</summary>
public sealed class GitCommit
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? At { get; set; }
    public string? Url { get; set; }
}

/// <summary>이름과 건수 한 쌍.</summary>
public sealed class GitNameCount
{
    public string? Name { get; set; }
    public long Count { get; set; }
}
