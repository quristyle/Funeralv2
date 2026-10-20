namespace AIAgentServer.Services.Rag;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using AIAgentServer.Data;

/// <summary>RAG 색인을 백그라운드에서 만든다. 올리기·지우기·「색인 다시 만들기」가 모두 여기로 들어오고, 카테고리별로 겹치지 않게 하나씩 돈다.
/// 요청 안에서 돌리지 않는 까닭 — 임베딩이 CPU 로 돌아 문서가 많으면 몇 분씩 걸리고, 포털의 HttpClient 가 180초에 끊는다.</summary>
public sealed class RagBackgroundIndexer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RagBackgroundIndexer> _logger;

    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly object _sync = new();
    private readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    private readonly HashSet<string> _running = new(StringComparer.Ordinal);
    private readonly HashSet<string> _needsRerun = new(StringComparer.Ordinal);

    /// <summary>카테고리마다 마지막 작업의 모습. 메모리에만 있다 — 서비스가 다시 뜨면 비고, 그때 화면은 카테고리의 「색인한 때」를 본다.</summary>
    private readonly ConcurrentDictionary<string, RagIndexStatus> _status = new(StringComparer.Ordinal);

    public RagBackgroundIndexer(IServiceScopeFactory scopeFactory, ILogger<RagBackgroundIndexer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>지금 이 카테고리의 색인 작업 상태. 한 번도 돈 적이 없으면 <c>idle</c>.</summary>
    public RagIndexStatus GetStatus(string categoryId)
    {
        lock (_sync)
        {
            var s = _status.GetValueOrDefault(categoryId) ?? RagIndexStatus.Idle(categoryId);
            return s with { RerunQueued = _needsRerun.Contains(categoryId) };
        }
    }

    /// <summary>색인을 큐에 넣는다. 이미 기다리는 중이면 그대로 두고, 도는 중이면 끝난 뒤 한 번 더 돌게 적어 둔다.</summary>
    public RagIndexStatus QueueReindex(string categoryId, string trigger = "auto", string? requestedBy = null)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return RagIndexStatus.Idle(categoryId ?? string.Empty);

        lock (_sync)
        {
            if (_running.Contains(categoryId))
            {
                _needsRerun.Add(categoryId);
                _logger.LogInformation("RAG 색인 실행 중 — 재실행 예약: {Category}", categoryId);
            }
            else if (_queued.Add(categoryId))
            {
                _status[categoryId] = new RagIndexStatus
                {
                    Category = categoryId,
                    State = RagIndexStatus.Queued,
                    Trigger = trigger,
                    RequestedBy = requestedBy,
                    QueuedAt = DateTime.UtcNow,
                };
                _channel.Writer.TryWrite(categoryId);
                _logger.LogInformation("RAG 색인 큐 추가: {Category} ({Trigger})", categoryId, trigger);
            }

            return GetStatus(categoryId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RAG 백그라운드 색인 서비스가 시작되었습니다.");

        while (!stoppingToken.IsCancellationRequested)
        {
            string categoryId;
            try
            {
                categoryId = await _channel.Reader.ReadAsync(stoppingToken);

                // 여러 파일이 연달아 올라오면 한 번에 묶는다.
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            lock (_sync)
            {
                _queued.Remove(categoryId);
                _running.Add(categoryId);
                _needsRerun.Remove(categoryId);
                Update(categoryId, s => s with
                {
                    State = RagIndexStatus.Running,
                    Phase = null,
                    Done = 0,
                    Total = 0,
                    StartedAt = DateTime.UtcNow,
                    FinishedAt = null,
                    Error = null,
                });
            }

            try
            {
                var chunks = await ExecuteReindexAsync(categoryId, stoppingToken);
                Update(categoryId, s => s with
                {
                    State = RagIndexStatus.Completed,
                    Phase = null,
                    FinishedAt = DateTime.UtcNow,
                    Chunks = chunks,
                });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG 백그라운드 색인 처리 중 오류: {Category}", categoryId);
                Update(categoryId, s => s with
                {
                    State = RagIndexStatus.Failed,
                    FinishedAt = DateTime.UtcNow,
                    Error = ex.Message,
                });
            }

            lock (_sync)
            {
                _running.Remove(categoryId);
                if (_needsRerun.Remove(categoryId))
                {
                    var prev = _status.GetValueOrDefault(categoryId);
                    _queued.Add(categoryId);
                    _status[categoryId] = new RagIndexStatus
                    {
                        Category = categoryId,
                        State = RagIndexStatus.Queued,
                        Trigger = prev?.Trigger ?? "auto",
                        RequestedBy = prev?.RequestedBy,
                        QueuedAt = DateTime.UtcNow,
                    };
                    _channel.Writer.TryWrite(categoryId);
                    _logger.LogInformation("RAG 색인 중 새 요청이 있어 후속 색인을 예약합니다: {Category}", categoryId);
                }
            }
        }
    }

    private async Task<int> ExecuteReindexAsync(string categoryId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        if (!await db.RagCategories.AnyAsync(x => x.Id == categoryId && !x.IsDeleted, ct))
        {
            throw new InvalidOperationException("카테고리가 없거나 지워졌습니다.");
        }

        var lib = scope.ServiceProvider.GetRequiredService<RagLibrary>();
        _logger.LogInformation("RAG 백그라운드 색인 시작: {Category}", categoryId);
        var index = await lib.ReindexAsync(categoryId,
            (phase, done, total) => Update(categoryId, s => s with { Phase = phase, Done = done, Total = total }),
            ct);
        _logger.LogInformation("RAG 백그라운드 색인 완료: {Category} (토막 {Chunks}개)", categoryId, index.Chunks.Count);
        return index.Chunks.Count;
    }

    private void Update(string categoryId, Func<RagIndexStatus, RagIndexStatus> change)
        => _status.AddOrUpdate(categoryId,
            id => change(RagIndexStatus.Idle(id)),
            (_, s) => change(s));
}

/// <summary>카테고리 하나의 색인 작업 상태. 화면이 몇 초마다 읽어 진행을 그린다.</summary>
public sealed record RagIndexStatus
{
    public const string IdleState = "idle";
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "done";
    public const string Failed = "failed";

    public string Category { get; init; } = string.Empty;

    /// <summary><c>idle</c> · <c>queued</c> · <c>running</c> · <c>done</c> · <c>failed</c></summary>
    public string State { get; init; } = IdleState;

    /// <summary>도는 중일 때의 단계. <c>read</c>(문서 읽기) · <c>embed</c>(임베딩) · <c>save</c>(저장)</summary>
    public string? Phase { get; init; }

    public int Done { get; init; }
    public int Total { get; init; }

    /// <summary><c>manual</c>(「색인 다시 만들기」) · <c>auto</c>(올리기·지우기)</summary>
    public string? Trigger { get; init; }

    public string? RequestedBy { get; init; }
    public DateTime? QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }

    /// <summary>끝난 작업이 만든 토막 수.</summary>
    public int? Chunks { get; init; }

    public string? Error { get; init; }

    /// <summary>도는 동안 새 요청이 와서, 끝나면 한 번 더 돈다.</summary>
    public bool RerunQueued { get; init; }

    public static RagIndexStatus Idle(string categoryId) => new() { Category = categoryId };
}
