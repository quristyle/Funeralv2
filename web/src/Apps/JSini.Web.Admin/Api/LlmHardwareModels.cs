using System.Text.Json.Serialization;

namespace JSini.Web.Admin.Api;

/// <summary>
/// LLM 장비(호스트명 <c>llm</c>)의 하드웨어 상태.
/// </summary>
/// <remarks>
/// <para>
/// 장비는 운영서버와 같은 내부망에 있어 브라우저가 직접 묻지 못한다.
/// 포털 → 게이트웨이 → AIAgentServer → 장비 nginx → 수집기 순으로 간다.
/// </para>
/// <para>
/// <b>장비가 꺼져 있어도 200 이 온다.</b> 그때는 <see cref="Reachable"/> 이 거짓이고
/// <see cref="Metrics"/> 가 비어 있으며 <see cref="Message"/> 에 까닭이 담긴다 —
/// 화면이 빈 자리 대신 "왜" 를 보여 줄 수 있어야 하기 때문이다.
/// </para>
/// </remarks>
public sealed class LlmHardwareDto
{
    public bool Reachable { get; set; }

    /// <summary>닿지 못했을 때의 까닭. 닿았으면 null.</summary>
    public string? Message { get; set; }

    /// <summary>실제로 물어본 주소. 개발 PC 와 운영이 서로 다르다.</summary>
    public string? Endpoint { get; set; }

    public LlmMetricsDto? Metrics { get; set; }
}

public sealed class LlmMetricsDto
{
    public LlmSystemDto? System { get; set; }
    public LlmCpuDto? Cpu { get; set; }
    public LlmMemoryDto? Memory { get; set; }
    public LlmGpuSetDto? Gpu { get; set; }
    public List<LlmDiskDto> Disks { get; set; } = [];
    public List<LlmNicDto> Network { get; set; } = [];
    public List<LlmServiceDto> Services { get; set; } = [];
    public List<LlmServerDto> Llm { get; set; } = [];
}

public sealed class LlmSystemDto
{
    public string? Hostname { get; set; }
    public string? Os { get; set; }
    public string? Kernel { get; set; }
    public long UptimeSeconds { get; set; }
    public long BootTime { get; set; }
    public List<double> LoadAvg { get; set; } = [];

    /// <summary>장비가 자료를 뜬 시각(유닉스 초). 화면이 "언제 것인지" 를 말할 때 쓴다.</summary>
    public long CollectedAt { get; set; }
}

public sealed class LlmCpuDto
{
    public string? Model { get; set; }
    public int LogicalCores { get; set; }
    public string? Governor { get; set; }
    public string? EnergyPreference { get; set; }
    public double TotalUsagePercent { get; set; }
    public List<double> PerCorePercent { get; set; } = [];
    public List<int> CurrentMhz { get; set; } = [];
    public int? AvgMhz { get; set; }
    public double? TemperatureC { get; set; }
}

public sealed class LlmMemoryDto
{
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsedPercent { get; set; }
    public long CachedBytes { get; set; }
    public long BuffersBytes { get; set; }
    public long SwapTotalBytes { get; set; }
    public long SwapUsedBytes { get; set; }
}

public sealed class LlmGpuSetDto
{
    public bool Available { get; set; }

    /// <summary>GPU 를 못 읽을 때의 까닭. 드라이버 판이 어긋나면 여기에 담긴다.</summary>
    public string? Reason { get; set; }

    public string? CudaVersion { get; set; }
    public List<LlmGpuDto> Devices { get; set; } = [];
}

public sealed class LlmGpuDto
{
    public int Index { get; set; }
    public string? Name { get; set; }
    public string? Uuid { get; set; }
    public string? DriverVersion { get; set; }
    public string? VbiosVersion { get; set; }
    public string? Serial { get; set; }

    public int? TemperatureGpu { get; set; }
    public int? TemperatureMemory { get; set; }
    public int? FanSpeed { get; set; }
    public int? UtilizationGpu { get; set; }
    public int? UtilizationMemory { get; set; }

    public long? MemoryTotal { get; set; }
    public long? MemoryUsed { get; set; }
    public long? MemoryFree { get; set; }
    public long? MemoryReserved { get; set; }
    public double MemoryUsedPercent { get; set; }

    public double? PowerDraw { get; set; }
    public double? PowerLimit { get; set; }
    public double? PowerMaxLimit { get; set; }

    /// <summary>실제로 걸려 있는 전력 한도. 기본값보다 낮으면 누군가 줄여 둔 것이다.</summary>
    public double? EnforcedPowerLimit { get; set; }

    public int? ClocksSm { get; set; }
    public int? ClocksMem { get; set; }
    public int? ClocksGr { get; set; }
    public int? ClocksMaxSm { get; set; }
    public int? ClocksMaxMem { get; set; }

    /// <summary>
    /// PCIe 폭·세대. <b>유휴일 때는 Gen1 로 떨어진다</b> — 절전이지 고장이 아니다.
    /// 다만 폭(<see cref="PcieLinkWidthCurrent"/>)이 최대보다 작으면 메인보드 레인
    /// 배분이 그런 것이라 바뀌지 않는다.
    /// </summary>
    public int? PcieLinkGenCurrent { get; set; }
    public int? PcieLinkGenMax { get; set; }
    public int? PcieLinkWidthCurrent { get; set; }
    public int? PcieLinkWidthMax { get; set; }

    public string? PersistenceMode { get; set; }
    public string? ComputeMode { get; set; }
    public string? EccModeCurrent { get; set; }
    public string? Pstate { get; set; }

    /// <summary>0 이 아니면 무언가가 클럭을 누르고 있다는 뜻이다(발열·전력 한도 등).</summary>
    public string? ClocksEventReasonsActive { get; set; }

    public int? EncoderStatsAverageFps { get; set; }

    public List<LlmGpuProcDto> Processes { get; set; } = [];
}

public sealed class LlmGpuProcDto
{
    public int Pid { get; set; }
    public string? Name { get; set; }
    public long MemoryMiB { get; set; }
}

public sealed class LlmDiskDto
{
    public string? Device { get; set; }
    public string? Mount { get; set; }
    public string? FsType { get; set; }
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public double UsedPercent { get; set; }
}

public sealed class LlmNicDto
{
    public string? Name { get; set; }
    public long RxBytes { get; set; }
    public long RxPackets { get; set; }
    public long TxBytes { get; set; }
    public long TxPackets { get; set; }
    public long RxErrors { get; set; }
    public long TxErrors { get; set; }
    public string? OperState { get; set; }
    public int? SpeedMbps { get; set; }
}

public sealed class LlmServiceDto
{
    public string? Unit { get; set; }
    public string? ActiveState { get; set; }
    public string? SubState { get; set; }
    public string? Enabled { get; set; }

    /// <summary><b>이 값이 늘고 있으면 무언가 반복해 죽고 있다.</b> 상태는 active 로 보인다.</summary>
    public int Restarts { get; set; }

    public long? MemoryBytes { get; set; }
    public int? MainPid { get; set; }
    public long? ActiveSeconds { get; set; }
}

public sealed class LlmServerDto
{
    public string? Label { get; set; }
    public string? BaseUrl { get; set; }
    public bool Reachable { get; set; }
    public List<LlmModelDto> Models { get; set; } = [];
    public LlmPropsDto? Props { get; set; }
}

public sealed class LlmModelDto
{
    public string? Id { get; set; }
    public long? ContextLength { get; set; }
    public long? TrainedContext { get; set; }
    public long? Params { get; set; }
    public long? SizeBytes { get; set; }
    public string? Quantization { get; set; }
    public long? VocabSize { get; set; }
}

public sealed class LlmPropsDto
{
    public int? Slots { get; set; }
    public string? ModelPath { get; set; }
    public bool ChatTemplate { get; set; }
}
