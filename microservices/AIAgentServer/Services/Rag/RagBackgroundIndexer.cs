namespace AIAgentServer.Services.Rag;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using AIAgentServer.Data;

/// <summary>RAG 문서 등록/삭제 시 백그라운드에서 자동으로 색인을 생성/갱신하는 서비스. 카테고리별 중복/동시 실행을 방지하고 연속적인 요청을 병합(coalesce)한다.</summary>
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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _categoryLocks = new(StringComparer.Ordinal);

    public RagBackgroundIndexer(IServiceScopeFactory scopeFactory, ILogger<RagBackgroundIndexer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>지정된 카테고리의 백그라운드 색인을 큐에 추가한다. 이미 큐에 있거나 실행 중인 경우 중복 실행을 방지하고 필요 시 완료 후 재실행 플래그를 설정한다.</summary>
    public void QueueReindex(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return;

        lock (_sync)
        {
            if (_running.Contains(categoryId))
            {
                _needsRerun.Add(categoryId);
                _logger.LogInformation("RAG 백그라운드 색인 실행 중 — 재실행 예약: {Category}", categoryId);
                return;
            }

            if (_queued.Add(categoryId))
            {
                _channel.Writer.TryWrite(categoryId);
                _logger.LogInformation("RAG 백그라운드 색인 큐 추가: {Category}", categoryId);
            }
        }
    }

    /// <summary>수동 색인 호출 시 백그라운드 작업과 충돌하지 않도록 동일한 카테고리 락을 획득하여 색인을 직접 수행한다.</summary>
    public async Task<RagIndexFile> ReindexDirectAsync(string categoryId, RagLibrary lib, CancellationToken ct = default)
    {
        var sem = _categoryLocks.GetOrAdd(categoryId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            lock (_sync)
            {
                _running.Add(categoryId);
                _needsRerun.Remove(categoryId);
            }

            var index = await lib.ReindexAsync(categoryId, ct);
            return index;
        }
        finally
        {
            lock (_sync)
            {
                _running.Remove(categoryId);
            }
            sem.Release();
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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // 여러 파일이 연달아 등록되는 경우(루프 업로드 등)를 위해 1초 디바운스 대기
            try
            {
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
            }

            var sem = _categoryLocks.GetOrAdd(categoryId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(stoppingToken);
            try
            {
                await ExecuteReindexAsync(categoryId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG 백그라운드 색인 처리 중 오류: {Category}", categoryId);
            }
            finally
            {
                sem.Release();
            }

            bool rerun;
            lock (_sync)
            {
                _running.Remove(categoryId);
                rerun = _needsRerun.Remove(categoryId);
                if (rerun)
                {
                    _queued.Add(categoryId);
                    _channel.Writer.TryWrite(categoryId);
                    _logger.LogInformation("RAG 색인 중 새 요청이 감지되어 후속 색인을 예약합니다: {Category}", categoryId);
                }
            }
        }
    }

    private async Task ExecuteReindexAsync(string categoryId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var cat = await db.RagCategories.FirstOrDefaultAsync(x => x.Id == categoryId && !x.IsDeleted, ct);
        if (cat is null)
        {
            _logger.LogInformation("RAG 백그라운드 색인 건너뜀 (카테고리 없음 또는 삭제됨): {Category}", categoryId);
            return;
        }

        var lib = scope.ServiceProvider.GetRequiredService<RagLibrary>();
        _logger.LogInformation("RAG 백그라운드 색인 시작: {Category}", categoryId);
        var index = await lib.ReindexAsync(categoryId, ct);
        _logger.LogInformation("RAG 백그라운드 색인 완료: {Category} (토막 {Chunks}개)", categoryId, index.Chunks.Count);
    }
}
