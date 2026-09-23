namespace ProjMngServer.Models;

// GitLab 에서 읽어 오는 것들. **우리 DB 에 담지 않는다** — 메모리에 잠깐
// 들고 있다가 버린다(TTL). 원본을 그대로 쓰는 자리라, 여기 있는 이름은
// 대체로 GitLab API 의 이름 그대로다.

/// <summary>토큰이 없거나 호출이 막혔을 때도 화면이 뜨게 하는 겉봉.</summary>
/// <typeparam name="T">줄 하나의 모양.</typeparam>
public sealed class GitlabResult<T>
{
    /// <summary>토큰이 있나. <b>없으면 줄은 오지만 값이 전부 비어 있다.</b></summary>
    public bool Configured { get; set; }

    public string? BaseUrl { get; set; }
    public string? Group { get; set; }

    /// <summary>마지막으로 읽어 온 시각. 캐시라 지금이 아닐 수 있다.</summary>
    public string? LoadedAt { get; set; }

    public List<T> Rows { get; set; } = [];
}

/// <summary>빌드 상태 한 줄 — 프로젝트 하나의 가장 최근 Job.</summary>
public sealed class GitlabJobRow
{
    public string? Module { get; set; }

    /// <summary><c>be</c> 또는 <c>fe</c>.</summary>
    public string? Kind { get; set; }

    public string? Path { get; set; }
    public string? JobsUrl { get; set; }

    /// <summary>
    /// <c>success</c>·<c>failed</c>·<c>running</c>… <c>none</c> 이면 Job 이
    /// 한 번도 없었다는 뜻이고, <see cref="Error"/> 가 있으면 못 읽은 것이다.
    /// <b>둘은 다르다.</b>
    /// </summary>
    public string? Status { get; set; }

    /// <summary>못 읽은 까닭. 이 값이 있으면 나머지는 비어 있다.</summary>
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

    /// <summary>가장 최근 <b>성공한</b> Job. 지금 깨져 있어도 언제까지 됐는지 보인다.</summary>
    public GitlabJobBrief? LastSuccess { get; set; }
}

/// <summary>Job 한 건의 요약.</summary>
public sealed class GitlabJobBrief
{
    public string? Status { get; set; }
    public string? Name { get; set; }
    public string? Ref { get; set; }

    /// <summary>끝난 시각. 없으면 시작 시각, 그것도 없으면 만든 시각.</summary>
    public string? At { get; set; }

    public double? Duration { get; set; }
    public string? Url { get; set; }
}

/// <summary>모니터링 한 줄 — 프로젝트 하나의 종합.</summary>
public sealed class GitlabMonitorRow
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

    /// <summary>오래 손대지 않은 가지. 며칠부터인지는 설정이 정한다.</summary>
    public int StaleBranches { get; set; }

    public List<GitlabBranch> BranchList { get; set; } = [];

    public int OpenMrs { get; set; }
    public List<GitlabMr> MrList { get; set; } = [];

    public GitlabJobStats? Jobs { get; set; }

    public int Commits7d { get; set; }
    public List<GitlabNameCount> CommitAuthors7d { get; set; } = [];
    public GitlabCommit? LastCommit { get; set; }

    public int Contributors { get; set; }
    public List<GitlabNameCount> ContributorList { get; set; } = [];

    public GitlabRegistry? Registry { get; set; }
}

/// <summary>가지 하나.</summary>
public sealed class GitlabBranch
{
    public string? Name { get; set; }
    public bool Default { get; set; }
    public bool Merged { get; set; }

    /// <summary>마지막 커밋 시각.</summary>
    public string? At { get; set; }

    public bool Stale { get; set; }
}

/// <summary>열려 있는 병합 요청 하나.</summary>
public sealed class GitlabMr
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

/// <summary>최근 Job 몇 개를 훑어 낸 품질 지표.</summary>
public sealed class GitlabJobStats
{
    /// <summary>훑은 개수. <b>전부가 아니다</b> — 설정이 정한 표본이다.</summary>
    public int Sampled { get; set; }

    public int Success { get; set; }
    public int Failed { get; set; }
    public int Other { get; set; }

    /// <summary>성공률(%). 끝난 것(성공 + 실패)만 분모로 센다.</summary>
    public double? Rate { get; set; }

    public double? AvgDuration { get; set; }

    /// <summary>최근 순서대로의 상태. 화면이 작은 띠로 그린다.</summary>
    public List<string?> Recent { get; set; } = [];

    public GitlabJobBrief? Last { get; set; }
    public GitlabJobBrief? LastSuccess { get; set; }
    public GitlabJobBrief? LastFailed { get; set; }
}

/// <summary>커밋 하나.</summary>
public sealed class GitlabCommit
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? At { get; set; }
    public string? Url { get; set; }
}

/// <summary>이름과 건수 한 쌍.</summary>
public sealed class GitlabNameCount
{
    public string? Name { get; set; }
    public long Count { get; set; }
}

/// <summary>컨테이너 레지스트리.</summary>
public sealed class GitlabRegistry
{
    public int Repos { get; set; }
    public long Tags { get; set; }
    public long Size { get; set; }
    public List<GitlabRegistryRepo> List { get; set; } = [];
}

/// <inheritdoc cref="GitlabRegistry"/>
public sealed class GitlabRegistryRepo
{
    public string? Name { get; set; }
    public string? Location { get; set; }
    public long Tags { get; set; }
    public long Size { get; set; }
    public string? CreatedAt { get; set; }
}
