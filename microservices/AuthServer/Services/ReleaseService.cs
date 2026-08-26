using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;

namespace AuthServer.Services;

/// <summary>
/// 배포 실행 서비스 구현체
/// </summary>
/// <remarks>
/// 이 서비스가 배포를 하지는 않는다. "이 스크립트를 돌려 달라" 를 큐에 넣고,
/// 배포 장비의 래퍼가 진행 상황을 콜백으로 되돌려 보고한다.
///
/// <para>
/// <b>예전에는 큐에 넣고 잊었다.</b> 진행 상황을 알 길이 없으니 화면이 단계를
/// 스스로 만들어 내 <c>setTimeout</c> 으로 초록색 [SUCCESS] 를 찍었다.
/// 그래서 배포가 실패해도, 소비자가 아예 안 떠 있어도 화면은 전부 초록이었다.
/// </para>
///
/// <para>
/// 없던 것은 <b>run id</b> 였다. 요청 한 건을 <see cref="ReleaseRun"/> 행으로 만들면
/// 래퍼가 그 id 로 보고할 수 있고 이력도 남는다.
/// </para>
///
/// <para>
/// <b>기존 배포를 깨지 않는다.</b> 큐 이름과 메시지의 <c>script</c>·<c>args</c> 를
/// 그대로 두었다. 배포 장비의 지금 소비자는 늘어난 필드를 무시하고 예전처럼 동작한다.
/// 래퍼를 붙인 뒤 대상별로 <c>ReportsProgress</c> 를 켜면 실제 진행 상황이 들어온다.
/// </para>
/// </remarks>
public class ReleaseService : IReleaseService
{
    /// <summary>권한을 읽어 올 메뉴 경로. 배포 실행은 이 메뉴의 can_cust1 이다.</summary>
    private const string MenuPath = "/portal/release";

    private readonly ReleaseOptions _options;
    private readonly ILogger<ReleaseService> _logger;
    private readonly AppDbContext _db;
    private readonly IMenuService _menus;
    private readonly IHttpClientFactory _http;

    public ReleaseService(
        IOptions<ReleaseOptions> options,
        ILogger<ReleaseService> logger,
        AppDbContext db,
        IMenuService menus,
        IHttpClientFactory http)
    {
        _options = options.Value;
        _logger = logger;
        _db = db;
        _menus = menus;
        _http = http;
    }

    // ── 목록 ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ReleaseTargetListDto> GetTargetsAsync(string userId)
    {
        // 화면을 열 때마다 멈춘 run 을 정리한다. 아래 4절 참고.
        await SweepStaleRunsAsync();

        var targets = _options.Targets
            .Where(t => !string.IsNullOrWhiteSpace(t.Key))
            .ToList();

        var items = new List<ReleaseTargetDto>();

        foreach (var t in targets)
        {
            // 대상은 보통 두세 개다. 대상마다 한 번 읽는 편이 한 번에 읽고
            // 메모리에서 묶는 것보다 읽기 쉽다.
            var last = await _db.ReleaseRuns
                .Where(r => !r.IsDeleted && r.TargetKey == t.Key)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            var active = last is not null && !ReleaseRunStatus.IsFinal(last.Status)
                ? last.Id
                : null;

            items.Add(new ReleaseTargetDto
            {
                Key = t.Key,
                Name = string.IsNullOrWhiteSpace(t.Name) ? t.Key : t.Name,
                Description = t.Description,
                ReportsProgress = t.ReportsProgress,
                TimeoutSeconds = t.TimeoutSeconds,
                EstimatedSeconds = t.EstimatedSeconds,
                ActiveRunId = active,
                LastRun = last is null ? null : ToDto(last)
            });
        }

        // 보고를 켰는데 콜백 주소가 없으면 설정이 반쪽이다. 화면에서 보여야 한다.
        string? warning = null;
        if (string.IsNullOrWhiteSpace(_options.CallbackBaseUrl) &&
            targets.Any(t => t.ReportsProgress))
        {
            warning =
                "진행 보고를 켠 대상이 있으나 Release:CallbackBaseUrl 이 비어 있습니다. " +
                "그 대상은 실행되지 않습니다.";
        }

        return new ReleaseTargetListDto
        {
            Items = items,
            CanRelease = await CanReleaseAsync(userId),
            ConfigWarning = warning
        };
    }

    // ── 실행 요청 ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<(ReleaseTriggerOutcome, ReleaseResultDto)> TriggerAsync(
        string key, string userId)
    {
        // 화면의 v-perm 은 버튼을 숨기는 장치일 뿐이다. 요청을 직접 보내면 통과하므로
        // 여기서 다시 본다. 배포는 되돌리기 어려운 동작이라 특히 그렇다.
        if (!await CanReleaseAsync(userId))
        {
            return (ReleaseTriggerOutcome.Forbidden, new ReleaseResultDto
            {
                TargetKey = key,
                Message = "배포를 실행할 권한이 없습니다."
            });
        }

        var target = _options.Targets.FirstOrDefault(t =>
            string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return (ReleaseTriggerOutcome.Invalid, new ReleaseResultDto
            {
                TargetKey = key,
                Message = $"'{key}' 배포 대상이 설정에 없습니다."
            });
        }

        if (string.IsNullOrWhiteSpace(target.ScriptPath))
        {
            return (ReleaseTriggerOutcome.Invalid, new ReleaseResultDto
            {
                TargetKey = target.Key,
                Message = $"'{target.Name}' 의 실행 스크립트 경로가 비어 있습니다."
            });
        }

        // 보고를 받기로 했는데 받을 주소가 없으면 실행하지 않는다.
        // 조용히 못 받는 것보다 왜 안 되는지 말하는 편이 낫다.
        var reports = target.ReportsProgress;
        if (reports && string.IsNullOrWhiteSpace(_options.CallbackBaseUrl))
        {
            return (ReleaseTriggerOutcome.Invalid, new ReleaseResultDto
            {
                TargetKey = target.Key,
                Message =
                    "진행 보고를 켠 대상인데 Release:CallbackBaseUrl 이 비어 있습니다. " +
                    "배포 장비가 결과를 돌려보낼 주소를 설정하세요."
            });
        }

        await SweepStaleRunsAsync();

        // 같은 대상이 이미 돌고 있으면 막는다.
        // 여기서 확인하는 것은 안내를 위한 것이고, 경합은 아래 unique 인덱스가 막는다.
        var running = await _db.ReleaseRuns
            .Where(r => !r.IsDeleted && r.TargetKey == target.Key &&
                        (r.Status == ReleaseRunStatus.Queued ||
                         r.Status == ReleaseRunStatus.Running))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (running is not null)
        {
            return (ReleaseTriggerOutcome.Conflict, new ReleaseResultDto
            {
                TargetKey = target.Key,
                RunId = running.Id,
                Message = $"'{target.Name}' 이(가) 이미 진행 중입니다."
            });
        }

        var run = new ReleaseRun
        {
            TargetKey = target.Key,
            TargetName = string.IsNullOrWhiteSpace(target.Name) ? target.Key : target.Name,
            ScriptPath = target.ScriptPath,
            Args = JsonSerializer.Serialize(target.Args),
            Status = ReleaseRunStatus.Queued,
            ReportsProgress = reports,
            RequestedBy = userId,
            TimeoutSeconds = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : 600,
            // 보고를 받지 않는 대상은 토큰이 필요 없다.
            CallbackToken = reports ? NewToken() : null
        };

        _db.ReleaseRuns.Add(run);
        AddEvent(run, "info", null,
            $"[요청] {run.TargetName} — {userId} 이(가) 실행을 요청했습니다.");

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 두 사람이 같은 순간에 눌렀다. 인덱스가 막아 준 것이다.
            return (ReleaseTriggerOutcome.Conflict, new ReleaseResultDto
            {
                TargetKey = target.Key,
                Message = $"'{target.Name}' 이(가) 이미 진행 중입니다."
            });
        }

        // ── 큐에 넣는다 ─────────────────────────────────────
        try
        {
            Publish(target, run);
        }
        catch (Exception ex)
        {
            // 예전에는 이 실패가 화면의 붉은 줄로만 남고 사라졌다. 이제 이력에 남는다.
            _logger.LogError(ex, "배포 요청 전송 실패. target={Target} run={Run}",
                target.Key, run.Id);

            run.Status = ReleaseRunStatus.Failed;
            run.FinishedAt = DateTime.UtcNow;
            run.Message = $"메시지 큐에 연결하지 못했습니다: {ex.Message}";
            run.CallbackToken = null;
            AddEvent(run, "error", null, $"[오류] {run.Message}");
            await _db.SaveChangesAsync();

            return (ReleaseTriggerOutcome.Failed, new ReleaseResultDto
            {
                TargetKey = target.Key,
                RunId = run.Id,
                Message = run.Message
            });
        }

        _logger.LogInformation(
            "배포 요청을 큐에 넣었습니다. target={Target} script={Script} user={User} run={Run} reports={Reports}",
            target.Key, target.ScriptPath, userId, run.Id, reports);

        if (reports)
        {
            AddEvent(run, "info", null,
                "[대기] 큐에 넣었습니다. 배포 장비가 집어가면 진행 상황이 이어집니다.");
        }
        else
        {
            // 보고가 오지 않는 대상이다. 성공했다고 말하지 않는다.
            run.Status = ReleaseRunStatus.Dispatched;
            run.FinishedAt = DateTime.UtcNow;
            run.Message =
                "요청을 큐에 넣었습니다. 이 대상은 진행 보고를 하지 않으므로 " +
                "실제 결과는 배포 장비에서 확인해야 합니다.";
            AddEvent(run, "warn", null, $"[알림] {run.Message}");
        }

        await _db.SaveChangesAsync();

        return (ReleaseTriggerOutcome.Ok, new ReleaseResultDto
        {
            Queued = true,
            TargetKey = target.Key,
            RunId = run.Id,
            Message = reports
                ? $"'{run.TargetName}' 배포 요청을 보냈습니다."
                : run.Message!
        });
    }

    /// <summary>
    /// 큐에 메시지를 넣는다.
    /// </summary>
    /// <remarks>
    /// 연결은 요청할 때만 연다. 브로커가 내려가 있어도 서비스 기동에는 영향이 없다.
    ///
    /// <para>
    /// <b>메시지 모양은 예전과 호환된다.</b> <c>script</c>·<c>args</c> 를 그대로 두었으므로
    /// 배포 장비의 지금 소비자는 늘어난 필드를 무시하고 예전처럼 동작한다.
    /// 새 래퍼는 <c>runId</c>·<c>callbackUrl</c>·<c>token</c> 을 보고 진행 상황을 보고한다.
    /// </para>
    ///
    /// <para>
    /// <c>targetKey</c> 를 함께 보내는 이유: 새 소비자는 자기 쪽 key→script 표를 보고
    /// 실행하는 편이 안전하다. 메시지가 시키는 아무 경로나 실행하지 않게 된다.
    /// 그때가 되면 <c>script</c> 를 빼는 것도 검토할 수 있다(28번 문서 참고).
    /// </para>
    /// </remarks>
    private void Publish(ReleaseTargetOption target, ReleaseRun run)
    {
        var factory = new ConnectionFactory
        {
            HostName = string.IsNullOrWhiteSpace(_options.HostName)
                ? "localhost"
                : _options.HostName,
            DispatchConsumersAsync = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // durable 은 기본 꺼짐이다. run_script 는 이미 non-durable 로 존재하고,
        // durable 로 다시 선언하면 브로커가 PRECONDITION_FAILED 를 낸다.
        channel.QueueDeclare(
            queue: _options.QueueName,
            durable: _options.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        // ── script·args 를 무엇으로 보낼지 ──────────────────
        //
        // 래퍼 경로가 설정돼 있으면 script 자리에 래퍼를 넣고 args 앞에 run 정보를 끼운다.
        // 그러면 지금 소비자는 예전과 똑같이 "script 를 args 와 함께 실행" 하는데
        // 그 script 가 래퍼가 되어, 소비자를 한 줄도 고치지 않고 보고를 받게 된다.
        var script = target.ScriptPath;
        var args = target.Args.ToList();

        var wrap = run.ReportsProgress && !string.IsNullOrWhiteSpace(target.WrapperPath);
        if (wrap)
        {
            script = target.WrapperPath!;
            args = new List<string>
                {
                    run.Id,
                    run.CallbackToken ?? string.Empty,
                    CallbackUrl(run.Id),
                    target.ScriptPath
                }
                .Concat(target.Args)
                .ToList();
        }

        var payload = new Dictionary<string, object?>
        {
            // 예전 소비자가 읽는 두 값
            ["script"] = script,
            ["args"] = args.ToArray(),
            // 새 소비자가 읽는 값
            ["targetKey"] = target.Key,
            ["runId"] = run.Id,
            // 래퍼를 거치면 소비자는 아무것도 몰라도 된다. 새 소비자가 헷갈리지 않게 알려 준다.
            ["wrapped"] = wrap
        };

        if (run.ReportsProgress)
        {
            payload["callbackUrl"] = CallbackUrl(run.Id);
            payload["token"] = run.CallbackToken;
            // 래퍼를 거치는 경우 실제 배포 스크립트가 무엇인지도 남겨 둔다.
            payload["targetScript"] = target.ScriptPath;
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        IBasicProperties? props = null;
        if (_options.Durable)
        {
            // durable 큐일 때만 의미가 있다. non-durable 큐에 persistent 를 붙여도
            // 브로커가 재시작되면 큐 자체가 사라진다.
            props = channel.CreateBasicProperties();
            props.Persistent = true;
        }

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            basicProperties: props,
            body: body);
    }

    // ── 조회 ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ReleaseRunDto?> GetRunAsync(string runId, int sinceSeq)
    {
        await SweepStaleRunsAsync();

        var run = await _db.ReleaseRuns
            .FirstOrDefaultAsync(r => r.Id == runId && !r.IsDeleted);

        if (run is null) return null;

        var events = await _db.ReleaseRunEvents
            .Where(e => e.RunId == runId && !e.IsDeleted && e.Seq > sinceSeq)
            .OrderBy(e => e.Seq)
            .Select(e => new ReleaseRunEventDto
            {
                Seq = e.Seq,
                Level = e.Level,
                Step = e.Step,
                Message = e.Message,
                At = e.CreatedAt
            })
            .ToListAsync();

        var dto = ToDto(run);
        dto.Events = events;
        return dto;
    }

    /// <inheritdoc />
    public async Task<List<ReleaseRunDto>> GetRunsAsync(int take)
    {
        await SweepStaleRunsAsync();

        var runs = await _db.ReleaseRuns
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync();

        return runs.Select(ToDto).ToList();
    }

    // ── 보고 받기 ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<(ReleaseReportOutcome, ReportReleaseEventsResultDto)> ReportEventsAsync(
        string runId, string? token, ReportReleaseEventsDto report)
    {
        var run = await _db.ReleaseRuns
            .FirstOrDefaultAsync(r => r.Id == runId && !r.IsDeleted);

        if (run is null)
        {
            return (ReleaseReportOutcome.NotFound, new ReportReleaseEventsResultDto
            {
                Ok = false, Stop = true, Message = "실행을 찾을 수 없습니다."
            });
        }

        // 실행 인증. 토큰이 지워졌으면 이미 끝난 run 이다.
        if (string.IsNullOrWhiteSpace(run.CallbackToken) ||
            string.IsNullOrWhiteSpace(token) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(run.CallbackToken),
                Encoding.UTF8.GetBytes(token)))
        {
            _logger.LogWarning("배포 보고 토큰이 맞지 않습니다. run={Run}", runId);
            return (ReleaseReportOutcome.Rejected, new ReportReleaseEventsResultDto
            {
                Ok = false, Stop = true, Message = "토큰이 맞지 않거나 이미 끝난 실행입니다."
            });
        }

        var now = DateTime.UtcNow;

        // 늦게 온 보고로 timeout 을 되살린다. 오래 도는 배포를 시간만으로 죽였을 수
        // 있으므로, 실제로 살아 있다고 알려 오면 그 말을 믿는다.
        if (run.Status is ReleaseRunStatus.Queued or ReleaseRunStatus.Timeout)
        {
            if (run.Status == ReleaseRunStatus.Timeout)
            {
                AddEvent(run, "info", null,
                    "[복귀] 제한 시간을 넘겨 중단으로 처리했으나 배포 장비가 다시 보고했습니다.");
            }

            run.Status = ReleaseRunStatus.Running;
            run.StartedAt ??= now;
            run.FinishedAt = null;
        }

        var accepted = 0;
        var capped = false;

        foreach (var line in report.Events ?? new List<ReportReleaseEventLineDto>())
        {
            if (run.LastSeq >= _options.MaxEventsPerRun)
            {
                capped = true;
                break;
            }

            var level = Normalize(line.Level);
            var text = Truncate(line.Message ?? string.Empty);

            // 단계 표시는 스크립트가 '##STEP <이름>' 을 찍었을 때만 온다.
            if (level == "step" && !string.IsNullOrWhiteSpace(line.Step))
            {
                run.CurrentStep = Truncate(line.Step, 200);
            }

            AddEvent(run, level, line.Step, text);
            accepted++;
        }

        if (capped && run.LastSeq == _options.MaxEventsPerRun)
        {
            // 조용히 자르지 않는다. 한 줄 남겨 두면 로그가 왜 끊겼는지 알 수 있다.
            AddEvent(run, "warn", null,
                $"[알림] 로그가 {_options.MaxEventsPerRun}줄을 넘어 이후 줄은 저장하지 않습니다.");
        }

        if (report.Final)
        {
            var code = report.ExitCode ?? -1;
            run.ExitCode = code;
            run.FinishedAt = now;
            run.Status = code == 0 ? ReleaseRunStatus.Succeeded : ReleaseRunStatus.Failed;
            run.Message = code == 0
                ? "배포 스크립트가 정상으로 끝났습니다."
                : $"배포 스크립트가 실패했습니다 (종료 코드 {code}).";
            AddEvent(run, code == 0 ? "result" : "error", null,
                $"[{(code == 0 ? "완료" : "실패")}] {run.Message}");

            // 끝났으므로 토큰을 지운다. 남겨 두면 끝난 run 에 계속 덧붙일 수 있다.
            run.CallbackToken = null;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 같은 run 에 두 보고가 동시에 들어와 순번이 부딪혔다.
            // 래퍼는 순차로 보내므로 흔치 않다. 실패로 알려 주면 래퍼가 다시 보낸다.
            _logger.LogWarning(ex, "배포 보고 순번 충돌. run={Run}", runId);
            return (ReleaseReportOutcome.Retry, new ReportReleaseEventsResultDto
            {
                Ok = false, Message = "순번이 충돌했습니다. 잠시 뒤 다시 보내 주세요."
            });
        }

        // 배포가 끝났으면 대상이 실제로 새 버전을 들고 있는지 확인한다.
        // 종료 코드 0 과 "반영됐다" 는 다른 이야기다.
        if (report.Final) await VerifyVersionAsync(run);

        return (ReleaseReportOutcome.Ok, new ReportReleaseEventsResultDto
        {
            Ok = true,
            Accepted = accepted,
            Stop = capped || ReleaseRunStatus.IsFinal(run.Status)
        });
    }

    // ── 배포 후 버전 확인 ───────────────────────────────────

    /// <summary>
    /// 대상이 스스로 알려 주는 버전을 읽어 남긴다.
    /// </summary>
    /// <remarks>
    /// 예전 화면은 <b>포털 자신의</b> <c>/version.json</c> 을 브라우저에서 읽었다.
    /// jin114 를 배포해도 그 숫자는 바뀌지 않으니 아무 의미가 없었다.
    /// 대상마다 <c>VersionUrl</c> 을 두고 <b>서버가</b> 읽는다.
    ///
    /// <para>
    /// 읽지 못해도 배포 결과는 바꾸지 않는다. 확인에 실패한 것과
    /// 배포가 실패한 것은 다른 일이다. 대신 로그에 남겨 둔다.
    /// </para>
    /// </remarks>
    private async Task VerifyVersionAsync(ReleaseRun run)
    {
        var target = _options.Targets.FirstOrDefault(t =>
            string.Equals(t.Key, run.TargetKey, StringComparison.OrdinalIgnoreCase));

        if (target is null || string.IsNullOrWhiteSpace(target.VersionUrl)) return;

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var body = await client.GetStringAsync(target.VersionUrl);
            var version = ParseVersion(body);

            run.DeployedVersion = version;
            AddEvent(run, "result", null, $"[버전] 대상이 알려 준 버전: {version}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배포 후 버전 확인 실패. target={Target} url={Url}",
                run.TargetKey, target.VersionUrl);
            AddEvent(run, "warn", null,
                $"[버전] 확인하지 못했습니다 ({target.VersionUrl}): {ex.Message}");
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>JSON 이면 version 키를, 아니면 본문 앞부분을 읽는다.</summary>
    private static string ParseVersion(string body)
    {
        var text = body.Trim();

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("version", out var v))
            {
                return v.ToString();
            }
        }
        catch (JsonException)
        {
            // JSON 이 아니면 그냥 본문으로 본다.
        }

        return text.Length > 100 ? text[..100] : text;
    }

    // ── 멈춘 실행 정리 ──────────────────────────────────────

    /// <summary>
    /// 제한 시간을 넘긴 실행을 timeout 으로 바꾼다.
    /// </summary>
    /// <remarks>
    /// 별도의 백그라운드 서비스를 두지 않고 <b>읽을 때마다</b> 훑는다.
    /// 이유: 멈춘 run 이 문제가 되는 순간은 (1) 화면이 진행 상황을 볼 때와
    /// (2) 다음 배포를 시도할 때뿐이고, 두 경로가 모두 여기를 지난다.
    /// 상주 서비스를 늘리면 수명·중복 실행을 함께 돌봐야 한다.
    ///
    /// <para>
    /// <b>토큰은 지우지 않는다.</b> 오래 도는 배포를 시간만으로 죽였을 수 있으므로,
    /// 늦게라도 보고가 오면 running 으로 되살린다.
    /// </para>
    /// </remarks>
    private async Task SweepStaleRunsAsync()
    {
        var open = await _db.ReleaseRuns
            .Where(r => !r.IsDeleted &&
                        (r.Status == ReleaseRunStatus.Queued ||
                         r.Status == ReleaseRunStatus.Running))
            .ToListAsync();

        if (open.Count == 0) return;

        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var run in open)
        {
            if (run.Status == ReleaseRunStatus.Queued)
            {
                // 아무도 집어가지 않았다. 스크립트가 오래 도는 것과는 다른 문제라
                // 더 짧은 시간으로 본다.
                var grace = _options.PickupTimeoutSeconds > 0
                    ? _options.PickupTimeoutSeconds
                    : 60;

                if ((now - run.CreatedAt).TotalSeconds <= grace) continue;

                run.Status = ReleaseRunStatus.Timeout;
                run.FinishedAt = now;
                run.Message =
                    $"{grace}초 안에 배포 장비가 요청을 집어가지 않았습니다. " +
                    "큐 소비자가 떠 있는지 확인하세요.";
            }
            else
            {
                var since = run.StartedAt ?? run.CreatedAt;
                if ((now - since).TotalSeconds <= run.TimeoutSeconds) continue;

                run.Status = ReleaseRunStatus.Timeout;
                run.FinishedAt = now;
                run.Message =
                    $"제한 시간({run.TimeoutSeconds}초) 안에 끝났다는 보고가 오지 않았습니다.";
            }

            AddEvent(run, "error", null, $"[중단] {run.Message}");
            changed = true;
        }

        if (changed) await _db.SaveChangesAsync();
    }

    // ── 거들기 ──────────────────────────────────────────────

    /// <summary>배포 실행 권한. <c>/portal/release</c> 의 can_cust1 이다.</summary>
    private async Task<bool> CanReleaseAsync(string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        return perm.CanCust1;
    }

    /// <summary>로그 한 줄을 붙이고 순번을 올린다.</summary>
    private void AddEvent(ReleaseRun run, string level, string? step, string message)
    {
        run.LastSeq += 1;
        _db.ReleaseRunEvents.Add(new ReleaseRunEvent
        {
            RunId = run.Id,
            Seq = run.LastSeq,
            Level = level,
            Step = string.IsNullOrWhiteSpace(step) ? null : Truncate(step, 200),
            Message = Truncate(message)
        });
    }

    private string CallbackUrl(string runId) =>
        $"{_options.CallbackBaseUrl!.TrimEnd('/')}/release/runs/{runId}/events";

    /// <summary>배포 장비만 아는 1회용 토큰.</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static readonly string[] AllowedLevels =
        { "info", "stdout", "step", "warn", "error", "result" };

    /// <summary>모르는 level 은 stdout 으로 본다. 배포 장비가 보내는 값이라 믿지 않는다.</summary>
    private static string Normalize(string? level)
    {
        var v = (level ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedLevels.Contains(v) ? v : "stdout";
    }

    private string Truncate(string text) => Truncate(text, _options.MaxEventLength);

    private static string Truncate(string text, int max)
    {
        if (max <= 0 || text.Length <= max) return text;
        return string.Concat(text.AsSpan(0, max), " …(잘림)");
    }

    /// <summary>unique 인덱스에 부딪혔나 (Postgres 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" };

    private static ReleaseRunDto ToDto(ReleaseRun r) => new()
    {
        Id = r.Id,
        TargetKey = r.TargetKey,
        TargetName = r.TargetName,
        Status = r.Status,
        ReportsProgress = r.ReportsProgress,
        RequestedBy = r.RequestedBy,
        RequestedAt = r.CreatedAt,
        StartedAt = r.StartedAt,
        FinishedAt = r.FinishedAt,
        ExitCode = r.ExitCode,
        CurrentStep = r.CurrentStep,
        Message = r.Message,
        DeployedVersion = r.DeployedVersion,
        ScriptPath = r.ScriptPath,
        LastSeq = r.LastSeq,
        IsFinal = ReleaseRunStatus.IsFinal(r.Status)
    };
}
