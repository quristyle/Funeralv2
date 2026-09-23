using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// GitLab 빌드 상태와 종합 모니터링 — <c>projmng/gitlab</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>설정이 없어도 실패하지 않는다.</b> <see cref="GitlabResultDto{T}.Configured"/>
/// 가 거짓으로 오고 줄마다 까닭이 적혀 있다 — GitLab 을 안 쓰는 프로젝트가
/// 정상이라 화면이 안내만 보이면 된다.
/// </para>
///
/// <para>
/// 서버가 결과를 잠깐 들고 있다(빌드 상태 60초 · 모니터링 120초).
/// <c>refresh</c> 를 참으로 주면 그것을 건너뛴다 — <b>화면의 [새로고침]에만
/// 붙인다.</b> 들어올 때마다 건너뛰면 저장소 서른여섯에 왕복이 288 번이다.
/// </para>
/// </remarks>
public sealed class GitlabStatusClient(GatewayClient gateway)
{
    private const string Url = "projmng/gitlab";

    public Task<GitlabResultDto<GitlabJobDto>?> CiAsync(
        int prjRid, bool refresh = false, CancellationToken ct = default)
        => gateway.GetOneAsync<GitlabResultDto<GitlabJobDto>>(
            $"{Url}/ci?prjRid={prjRid}&refresh={refresh.ToString().ToLowerInvariant()}", ct);

    public Task<GitlabResultDto<GitlabMonitorDto>?> MonitorAsync(
        int prjRid, bool refresh = false, CancellationToken ct = default)
        => gateway.GetOneAsync<GitlabResultDto<GitlabMonitorDto>>(
            $"{Url}/monitor?prjRid={prjRid}&refresh={refresh.ToString().ToLowerInvariant()}", ct);
}

/// <summary>설정이 없을 때도 화면이 뜨게 하는 겉봉.</summary>
public sealed class GitlabResultDto<T>
{
    /// <summary>주소와 토큰이 다 있나. 거짓이면 줄의 값이 전부 비어 있다.</summary>
    public bool Configured { get; set; }

    public string? BaseUrl { get; set; }
    public string? Group { get; set; }

    /// <summary>서버가 마지막으로 읽어 온 시각. <b>지금이 아닐 수 있다</b>(캐시).</summary>
    public string? LoadedAt { get; set; }

    public List<T> Rows { get; set; } = [];
}

/// <summary>빌드 상태 한 줄.</summary>
public sealed class GitlabJobDto
{
    public string? Module { get; set; }

    /// <summary><c>be</c> 또는 <c>fe</c>.</summary>
    public string? Kind { get; set; }

    public string? Path { get; set; }
    public string? JobsUrl { get; set; }

    /// <summary>
    /// <c>none</c> 은 <b>Job 이 한 번도 없었다</b>는 뜻이다.
    /// <see cref="Error"/> 가 있는 것(못 읽었다)과 다르다.
    /// </summary>
    public string? Status { get; set; }

    public string? Error { get; set; }

    public long? JobId { get; set; }
    public string? JobName { get; set; }
    public string? Stage { get; set; }
    public string? Ref { get; set; }
    public string? CreatedAt { get; set; }
    public string? StartedAt { get; set; }
    public string? FinishedAt { get; set; }
    public double? Duration { get; set; }
    public string? JobUrl { get; set; }

    public string? CommitId { get; set; }
    public string? CommitTitle { get; set; }
    public string? CommitAuthor { get; set; }

    public long? PipelineId { get; set; }
    public string? PipelineStatus { get; set; }
    public string? UserName { get; set; }

    /// <summary>마지막으로 성공한 Job. 지금 깨져 있어도 <b>언제까지 됐는지</b>가 보인다.</summary>
    public GitlabJobBriefDto? LastSuccess { get; set; }
}

/// <summary>Job 한 건의 요약.</summary>
public sealed class GitlabJobBriefDto
{
    public string? Status { get; set; }
    public string? Name { get; set; }
    public string? Ref { get; set; }
    public string? At { get; set; }
    public double? Duration { get; set; }
    public string? Url { get; set; }
}

/// <summary>모니터링 한 줄.</summary>
public sealed class GitlabMonitorDto
{
    public string? Module { get; set; }
    public string? Kind { get; set; }
    public string? Path { get; set; }
    public string? WebUrl { get; set; }
    public string? JobsUrl { get; set; }
    public string? Error { get; set; }

    public long? ProjectId { get; set; }
    public string? DefaultBranch { get; set; }
    public string? LastActivityAt { get; set; }
    public string? CreatedAt { get; set; }
    public long? OpenIssues { get; set; }

    public long? CommitCount { get; set; }
    public long? RepoSize { get; set; }
    public long? ArtifactsSize { get; set; }
    public long? StorageSize { get; set; }
    public long? RegistrySize { get; set; }

    public int? Tags { get; set; }

    public int Branches { get; set; }

    /// <summary>오래 손대지 않은 가지. 며칠부터인지는 서버 설정이 정한다.</summary>
    public int StaleBranches { get; set; }

    public List<GitlabBranchDto> BranchList { get; set; } = [];

    public int OpenMrs { get; set; }
    public List<GitlabMrDto> MrList { get; set; } = [];

    public GitlabJobStatsDto? Jobs { get; set; }

    public int Commits7d { get; set; }
    public List<GitlabNameCountDto> CommitAuthors7d { get; set; } = [];
    public GitlabCommitDto? LastCommit { get; set; }

    public int Contributors { get; set; }
    public List<GitlabNameCountDto> ContributorList { get; set; } = [];

    public GitlabRegistryDto? Registry { get; set; }
}

/// <summary>가지 하나.</summary>
public sealed class GitlabBranchDto
{
    public string? Name { get; set; }
    public bool Default { get; set; }
    public bool Merged { get; set; }
    public string? At { get; set; }
    public bool Stale { get; set; }
}

/// <summary>열린 병합 요청 하나.</summary>
public sealed class GitlabMrDto
{
    public long? Iid { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Source { get; set; }
    public string? Target { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public bool Draft { get; set; }
    public string? WebUrl { get; set; }
}

/// <summary>최근 Job 몇 개를 훑은 품질 지표.</summary>
public sealed class GitlabJobStatsDto
{
    /// <summary>훑은 개수. <b>전부가 아니다</b> — 서버가 정한 표본이다.</summary>
    public int Sampled { get; set; }

    public int Success { get; set; }
    public int Failed { get; set; }
    public int Other { get; set; }

    /// <summary>성공률(%). 끝난 것만 분모다.</summary>
    public double? Rate { get; set; }

    public double? AvgDuration { get; set; }

    /// <summary>최근 순서대로의 상태. 작은 띠로 그린다.</summary>
    public List<string?> Recent { get; set; } = [];

    public GitlabJobBriefDto? Last { get; set; }
    public GitlabJobBriefDto? LastSuccess { get; set; }
    public GitlabJobBriefDto? LastFailed { get; set; }
}

/// <summary>커밋 하나.</summary>
public sealed class GitlabCommitDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? At { get; set; }
    public string? Url { get; set; }
}

/// <summary>이름과 건수.</summary>
public sealed class GitlabNameCountDto
{
    public string? Name { get; set; }
    public long Count { get; set; }
}

/// <summary>컨테이너 레지스트리.</summary>
public sealed class GitlabRegistryDto
{
    public int Repos { get; set; }
    public long Tags { get; set; }
    public long Size { get; set; }
    public List<GitlabRegistryRepoDto> List { get; set; } = [];
}

/// <inheritdoc cref="GitlabRegistryDto"/>
public sealed class GitlabRegistryRepoDto
{
    public string? Name { get; set; }
    public string? Location { get; set; }
    public long Tags { get; set; }
    public long Size { get; set; }
    public string? CreatedAt { get; set; }
}
