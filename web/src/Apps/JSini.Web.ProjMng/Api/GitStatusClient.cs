using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// Git 빌드 상태와 종합 모니터링 — <c>projmng/git</c>.
/// </summary>
/// <remarks>
/// <para>
/// 2026-09-23 에 사내 GitLab 에서 GitHub 로 갈아탔다. 옛 클라이언트는 git
/// 이력에 있다.
/// </para>
///
/// <para>
/// <b>저장소를 안 걸어 둔 프로젝트는 빈 채로 뜬다</b>
/// (<see cref="GitResultDto{T}.Configured"/>). 실패가 아니다.
/// </para>
///
/// <para>
/// <b>토큰은 없어도 된다.</b> 공개 저장소는 그대로 읽히고, 토큰이 없으면
/// 시간당 60회라는 한도가 붙을 뿐이다 —
/// <see cref="GitResultDto{T}.RateRemaining"/> 이 그 잔량이다.
/// </para>
///
/// <para>
/// <c>refresh</c> 는 <b>화면의 [새로고침]에만</b> 붙인다. 서버가 결과를 잠깐
/// 들고 있는데(빌드 상태 60초 · 모니터링 120초), 들어올 때마다 건너뛰면
/// 토큰 없는 한도를 몇 번 만에 다 쓴다.
/// </para>
/// </remarks>
public sealed class GitStatusClient(GatewayClient gateway)
{
    private const string Url = "projmng/git";

    public Task<GitResultDto<GitRunDto>?> CiAsync(
        int prjRid, bool refresh = false, CancellationToken ct = default)
        => gateway.GetOneAsync<GitResultDto<GitRunDto>>(
            $"{Url}/ci?prjRid={prjRid}&refresh={refresh.ToString().ToLowerInvariant()}", ct);

    public Task<GitResultDto<GitMonitorDto>?> MonitorAsync(
        int prjRid, bool refresh = false, CancellationToken ct = default)
        => gateway.GetOneAsync<GitResultDto<GitMonitorDto>>(
            $"{Url}/monitor?prjRid={prjRid}&refresh={refresh.ToString().ToLowerInvariant()}", ct);
}

/// <summary>저장소를 안 걸었을 때도 화면이 뜨게 하는 겉봉.</summary>
public sealed class GitResultDto<T>
{
    /// <summary>볼 저장소가 하나라도 있나.</summary>
    public bool Configured { get; set; }

    /// <summary>토큰이 있나. 없어도 공개 저장소는 읽힌다.</summary>
    public bool Authenticated { get; set; }

    public string? WebBaseUrl { get; set; }

    /// <summary>서버가 마지막으로 읽어 온 시각. <b>지금이 아닐 수 있다</b>(캐시).</summary>
    public string? LoadedAt { get; set; }

    /// <summary>남은 호출 한도. 「왜 갑자기 안 보이나」의 답이 대개 이것이다.</summary>
    public int? RateRemaining { get; set; }

    /// <inheritdoc cref="RateRemaining"/>
    public int? RateLimit { get; set; }

    public List<T> Rows { get; set; } = [];

    /// <summary>
    /// 토큰이 없거나 권한이 모자라 <b>못 본 항목</b>.
    /// </summary>
    /// <remarks>
    /// 말없이 비워 두면 「그 저장소는 원래 방문자가 0 인가 보다」로 읽힌다 —
    /// <b>못 본 것과 없는 것은 다르다.</b>
    /// </remarks>
    public List<GitBlockedDto> Blocked { get; set; } = [];
}

/// <summary>못 본 항목 하나.</summary>
public sealed class GitBlockedDto
{
    public string? What { get; set; }
    public string? Why { get; set; }

    /// <summary>무엇을 주면 보이나.</summary>
    public string? Needs { get; set; }
}

/// <summary>빌드 상태 한 줄 — 저장소 하나의 가장 최근 Actions 실행.</summary>
public sealed class GitRunDto
{
    /// <summary><c>owner/name</c>.</summary>
    public string? Repo { get; set; }

    public string? RunsUrl { get; set; }

    /// <summary>
    /// 한 낱말로 줄인 상태. <c>none</c> 은 <b>실행이 한 번도 없었다</b>는
    /// 뜻이고 <see cref="Error"/> 가 있는 것(못 읽었다)과 다르다.
    /// </summary>
    public string? Status { get; set; }

    public string? Error { get; set; }

    public long? RunId { get; set; }
    public string? WorkflowName { get; set; }
    public string? Event { get; set; }
    public string? Branch { get; set; }
    public string? StartedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public double? Duration { get; set; }
    public string? RunUrl { get; set; }

    public string? CommitId { get; set; }
    public string? CommitTitle { get; set; }
    public string? CommitAuthor { get; set; }
    public string? Actor { get; set; }

    /// <summary>마지막으로 성공한 실행. 지금 깨져 있어도 <b>언제까지 됐는지</b>가 보인다.</summary>
    public GitRunBriefDto? LastSuccess { get; set; }
}

/// <summary>실행 한 건의 요약.</summary>
public sealed class GitRunBriefDto
{
    public string? Status { get; set; }
    public string? WorkflowName { get; set; }
    public string? Branch { get; set; }
    public string? At { get; set; }
    public double? Duration { get; set; }
    public string? Url { get; set; }
}

/// <summary>모니터링 한 줄.</summary>
public sealed class GitMonitorDto
{
    public string? Repo { get; set; }
    public string? WebUrl { get; set; }
    public string? RunsUrl { get; set; }
    public string? Error { get; set; }

    public string? Description { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Language { get; set; }
    public string? Visibility { get; set; }
    public string? PushedAt { get; set; }
    public string? CreatedAt { get; set; }

    public long? OpenIssues { get; set; }
    public long? Stars { get; set; }
    public long? Forks { get; set; }

    /// <summary>저장소 크기(KB).</summary>
    public long? SizeKb { get; set; }

    public int Branches { get; set; }

    /// <summary>묵은 가지. <b>날짜를 물어본 가지 안에서만</b> 센 값이다.</summary>
    public int StaleBranches { get; set; }

    /// <summary>날짜를 실제로 물어본 가지 수. 위 값의 분모다.</summary>
    public int BranchesChecked { get; set; }

    public List<GitBranchDto> BranchList { get; set; } = [];

    public int? Tags { get; set; }

    public int OpenPrs { get; set; }
    public List<GitPrDto> PrList { get; set; } = [];

    public GitRunStatsDto? Runs { get; set; }

    public int Commits7d { get; set; }

    /// <summary>위 건수가 한 쪽 상한에 걸려 잘렸나. 참이면 <c>100+</c> 로 적는다.</summary>
    public bool Commits7dCapped { get; set; }

    public List<GitNameCountDto> CommitAuthors7d { get; set; } = [];
    public GitCommitDto? LastCommit { get; set; }

    public int Contributors { get; set; }
    public List<GitNameCountDto> ContributorList { get; set; } = [];

    /// <summary>방문·클론 통계(최근 14일). <b>토큰에 쓰기 권한이 있어야 찬다.</b></summary>
    public GitTrafficDto? Traffic { get; set; }

    /// <summary>릴리스. 토큰 없이도 보인다.</summary>
    public List<GitReleaseDto> Releases { get; set; } = [];

    /// <summary>이 저장소에서 온 GHCR 이미지. <b>토큰이 있어야 찬다.</b></summary>
    public List<GitPackageDto> Packages { get; set; } = [];

    /// <summary>가장 최근에 깨진 실행의 원인 자리.</summary>
    public GitFailureDto? LastFailure { get; set; }
}

/// <summary>방문·클론 통계(최근 14일).</summary>
public sealed class GitTrafficDto
{
    public int Views { get; set; }

    /// <summary>같은 사람을 한 번으로 센 값. 이쪽이 실제 사람 수에 가깝다.</summary>
    public int UniqueViews { get; set; }

    public int Clones { get; set; }

    /// <inheritdoc cref="UniqueViews"/>
    public int UniqueClones { get; set; }

    public List<GitNameCountDto> TopPaths { get; set; } = [];
}

/// <summary>릴리스 하나.</summary>
public sealed class GitReleaseDto
{
    public string? TagName { get; set; }
    public string? Name { get; set; }
    public string? PublishedAt { get; set; }
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public string? Author { get; set; }
    public string? Url { get; set; }
}

/// <summary>GHCR 컨테이너 이미지 하나.</summary>
public sealed class GitPackageDto
{
    public string? Name { get; set; }
    public long Versions { get; set; }
    public string? LatestTags { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Url { get; set; }
}

/// <summary>깨진 실행 하나의 원인 자리.</summary>
public sealed class GitFailureDto
{
    public long? RunId { get; set; }
    public string? WorkflowName { get; set; }
    public string? Branch { get; set; }
    public string? At { get; set; }
    public string? Url { get; set; }

    /// <summary>깨진 잡과 단계 — <c>build / dotnet test</c>.</summary>
    public List<string> Steps { get; set; } = [];
}

/// <summary>가지 하나.</summary>
public sealed class GitBranchDto
{
    public string? Name { get; set; }
    public bool Default { get; set; }
    public bool Protected { get; set; }

    /// <summary>마지막 커밋 시각. <b>안 물어본 가지는 비어 있다.</b></summary>
    public string? At { get; set; }

    public bool Stale { get; set; }
}

/// <summary>열려 있는 풀 리퀘스트 하나.</summary>
public sealed class GitPrDto
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

/// <summary>최근 실행 몇 개를 훑은 품질 지표.</summary>
public sealed class GitRunStatsDto
{
    /// <summary>훑은 개수. <b>전부가 아니다</b> — 서버가 정한 표본이다.</summary>
    public int Sampled { get; set; }

    /// <summary>저장소에 쌓인 전체 실행 수.</summary>
    public int Total { get; set; }

    public int Success { get; set; }
    public int Failure { get; set; }
    public int Other { get; set; }

    /// <summary>성공률(%). 끝난 것만 분모다.</summary>
    public double? Rate { get; set; }

    public double? AvgDuration { get; set; }

    /// <summary>최근 순서대로의 상태. 작은 띠로 그린다.</summary>
    public List<string?> Recent { get; set; } = [];

    public GitRunBriefDto? Last { get; set; }
    public GitRunBriefDto? LastSuccess { get; set; }
    public GitRunBriefDto? LastFailure { get; set; }
}

/// <summary>커밋 하나.</summary>
public sealed class GitCommitDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? At { get; set; }
    public string? Url { get; set; }
}

/// <summary>이름과 건수.</summary>
public sealed class GitNameCountDto
{
    public string? Name { get; set; }
    public long Count { get; set; }
}

/// <summary>
/// 실행 상태를 사람이 읽는 말로. <b>화면마다 적지 않는다</b> — 두 화면이
/// 같은 값을 쓰는데 한쪽만 고치면 같은 상태가 다른 이름으로 보인다.
/// </summary>
public static class GitRunText
{
    public static string Of(string? status) => status switch
    {
        "success" => "성공",
        "failure" => "실패",
        "cancelled" => "취소",
        "skipped" => "건너뜀",
        "timed_out" => "시간초과",
        "action_required" => "승인대기",
        "queued" or "waiting" or "pending" => "대기",
        "in_progress" => "진행",

        // 실행이 **한 번도 없었다**. 못 읽은 것과 다르다.
        "none" => "실행 없음",

        null => "—",
        _ => status,
    };

    public static string Badge(string? status) => status switch
    {
        "success" => "jsini-badge--on",
        "failure" or "timed_out" => "jsini-badge--off",
        "in_progress" or "queued" or "waiting" or "pending" or "action_required" => "jsini-badge--warn",
        _ => "",
    };
}
