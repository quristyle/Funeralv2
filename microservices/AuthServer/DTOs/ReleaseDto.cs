namespace AuthServer.DTOs;

/// <summary>
/// 배포 대상 한 건.
/// </summary>
/// <remarks>
/// 예전에는 헬프데스크 화면에 'jin114 배포' / 'goldb 배포' 두 버튼이 박혀 있었다.
/// JSini 포털은 여러 시스템의 배포를 관장하므로, 대상을 설정으로 뺐다.
/// 대상을 늘리려면 appsettings 의 Release:Targets 에 항목을 더하면 된다.
/// </remarks>
public class ReleaseTargetDto
{
    /// <summary>호출에 쓰는 식별자 (URL 에 들어간다)</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>화면에 보일 이름</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>무엇을 배포하는지에 대한 설명</summary>
    public string? Description { get; set; }

    /// <summary>대략의 소요 시간(초). 화면이 진행 안내에 쓴다.</summary>
    public int EstimatedSeconds { get; set; } = 20;
}

/// <summary>
/// 배포 대상 설정. appsettings 의 Release 섹션과 대응한다.
/// </summary>
public class ReleaseOptions
{
    /// <summary>메시지 큐 호스트. 큐 소비자가 도는 장비다.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>스크립트 실행 요청을 넣을 큐 이름</summary>
    public string QueueName { get; set; } = "run_script";

    public List<ReleaseTargetOption> Targets { get; set; } = new();
}

/// <summary>
/// 배포 대상 하나의 설정.
/// </summary>
public class ReleaseTargetOption
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>큐 소비자가 실행할 셸 스크립트의 절대 경로</summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>스크립트에 넘길 인자</summary>
    public List<string> Args { get; set; } = new();

    public int EstimatedSeconds { get; set; } = 20;
}

/// <summary>
/// 배포 실행 결과.
/// </summary>
/// <remarks>
/// 큐에 요청을 넣는 것까지가 이 API 의 일이다.
/// 스크립트가 실제로 끝났는지는 알 수 없다 — 큐 소비자가 별도 장비에서 돌린다.
/// </remarks>
public class ReleaseResultDto
{
    public bool Queued { get; set; }
    public string TargetKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
