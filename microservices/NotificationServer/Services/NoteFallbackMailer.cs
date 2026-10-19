using JSini.Shared.Infrastructure.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NotificationServer.Data;
using NotificationServer.Entities;
using NotificationServer.Options;

namespace NotificationServer.Services;

/// <summary>쪽지 전환 메일. 앱 푸시가 기기에 닿지 않은 쪽지를 메일로 다시 보낸다(보낸 것과 닿은 것은 다르다).
/// 한 통마다 자국을 바로 찍는다(바퀴 끝에 모아 저장하면 배포로 취소된 뒤 이미 나간 메일이 또 나간다).</summary>
public sealed class NoteFallbackMailer(
    IServiceScopeFactory scopes,
    IOptions<NoteFallbackMailOptions> options,
    IConfiguration configuration,
    ILogger<NoteFallbackMailer> logger) : BackgroundService
{
    private readonly NoteFallbackMailOptions _opt = options.Value;

    /// <summary>첫 바퀴를 조금 늦춘다 — 기동 직후는 DB 와 SMTP 가 아직 안 풀려 있다.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    /// <summary>쪽지를 눌렀을 때 열 화면. 푸시·메일이 같은 곳을 가리킨다.</summary>
    private const string InboxPath = "/admin/note/box";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled)
        {
            logger.LogInformation("쪽지 전환 메일이 꺼져 있습니다 (NoteFallbackMail:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(_opt.IntervalMinutes, 1, 60));

        logger.LogInformation(
            "쪽지 전환 메일을 켭니다. {After}분 안 닿으면 메일로, {Interval}분마다 훑습니다.",
            _opt.AfterMinutes, interval.TotalMinutes);

        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        // 기다리는 자리마다 취소를 받는다(새어 나간 OperationCanceledException 은 서비스를 오류로 보이게 한다).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 바퀴가 깨져도 배치는 산다(새면 다음 차례가 안 와서 전환 메일이 조용히 멎는다).
                logger.LogError(ex, "쪽지 전환 메일 한 바퀴가 깨졌습니다.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>한 바퀴 — 때가 된 쪽지를 집어 메일로 돌린다.</summary>
    private async Task SweepAsync(CancellationToken ct)
    {
        var now = AppTime.UtcNow;
        var cutoff = now.AddMinutes(-Math.Max(_opt.AfterMinutes, 1));
        var floor = now.AddHours(-Math.Max(_opt.LookbackHours, 1));

        using var scope = scopes.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 조건 넷이 전부 null 비교다(부분 색인 IX_notes_fallback 이 그 모양에 맞춰져 있다).
        var due = await db.Notes
            .Where(n => !n.IsDeleted
                        && !n.ReceiverDeleted
                        && n.ReadAt == null
                        && n.DeliveredAt == null
                        && n.FallbackEmailAt == null
                        && !n.EmailSent
                        && n.SentAt <= cutoff
                        && n.SentAt >= floor)
            .OrderBy(n => n.SentAt)
            .Take(Math.Clamp(_opt.MaxPerRun, 1, 500))
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return;
        }

        var resolver = scope.ServiceProvider.GetRequiredService<INoteRecipientResolver>();
        var prefs = scope.ServiceProvider.GetRequiredService<INotificationPreferenceService>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var keys = due.Select(n => n.ReceiverKey).Distinct(StringComparer.Ordinal).ToList();

        var people = await resolver.LoadByLoginIdsAsync(keys, ct);

        var addressOf = people
            .Where(p => !string.IsNullOrWhiteSpace(p.Email))
            .ToDictionary(p => p.LoginId, p => p.Email!, StringComparer.Ordinal);

        // 전체 메일 스위치를 끈 사람을 묻는다(행이 없으면 켜짐 — NotificationPreferenceService).
        var mailOff = await prefs.GetEmailDisabledLoginIdsAsync(keys, ct);

        var sent = 0;
        var skippedNoAddress = 0;
        var skippedOptOut = 0;

        foreach (var note in due)
        {
            if (ct.IsCancellationRequested) break;

            if (mailOff.Contains(note.ReceiverKey))
            {
                skippedOptOut++;

                // 본인이 끈 것은 **이 쪽지에 대해서는** 바뀔 일이 아니다.
                note.FallbackEmailAt = now;
                Trouble(note, "앱 알림이 안 닿았으나 받는 사람이 메일을 꺼 두어 전환 발송하지 않았습니다");
                await MarkAsync(db, note);
                continue;
            }

            if (!addressOf.TryGetValue(note.ReceiverKey, out var to))
            {
                skippedNoAddress++;

                // 자국을 찍는다(안 찍으면 이 줄이 창을 차지해 뒤의 쪽지가 한 통도 못 나간다).
                note.FallbackEmailAt = now;
                Trouble(note, "앱 알림이 안 닿았는데 메일 주소가 없어 전환 발송을 못 했습니다");
                await MarkAsync(db, note);
                continue;
            }

            var title = string.IsNullOrWhiteSpace(note.Title) ? "쪽지" : note.Title!;
            var body = Compose(note);

            try
            {
                await email.SendAsync(
                    to,
                    $"[쪽지] {title}",
                    NoticeEmailTemplate.Render(title, body, note.SenderName),
                    html: true,
                    attachments: null,
                    textBody: NoticeEmailTemplate.PlainAlternative(title, body, note.SenderName));

                note.FallbackEmailAt = now;
                Trouble(note, "앱 알림이 안 닿아 메일로 다시 보냈습니다");

                await MarkAsync(db, note);
                sent++;
            }
            catch (Exception ex)
            {
                // 자국을 안 찍는다(실패한 줄은 다음 차례에 다시 한다).
                db.Entry(note).State = EntityState.Unchanged;

                logger.LogError(ex, "쪽지 전환 메일을 보내지 못했습니다. note={Note} to={To}",
                    note.Id, note.ReceiverKey);
            }
        }

        logger.LogInformation(
            "쪽지 전환 메일 {Sent}통. 대상={Due} 주소없음={NoAddr} 메일꺼둠={OptOut}",
            sent, due.Count, skippedNoAddress, skippedOptOut);
    }

    /// <summary>줄 하나의 자국을 그 자리에서 표에 적는다. 취소 토큰을 주지 않는다 — 메일이 나갔다는 사실을 잃으면 같은 메일이 또 나간다.</summary>
    private static Task MarkAsync(AppDbContext db, Note note)
    {
        note.UpdatedAt = AppTime.UtcNow;
        return db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>메일 본문. 원래 쪽지 위에 「왜 메일로 왔는지」 한 줄을 얹는다(없으면 같은 글을 두 번 받았다고 여긴다).</summary>
    private string Compose(Note note)
    {
        var who = string.IsNullOrWhiteSpace(note.SenderName) ? note.SenderKey : note.SenderName!;
        var when = AppTime.ToKorea(note.SentAt).ToString("yyyy-MM-dd HH:mm");

        var lines = new List<string>
        {
            $"{who} 님이 {when} 에 보낸 쪽지입니다.",
            "앱 알림이 기기에 닿지 않아 메일로 다시 보냅니다.",
            string.Empty,
            (note.Body ?? string.Empty).Trim(),
        };

        if (InboxUrl() is { Length: > 0 } url)
        {
            lines.Add(string.Empty);
            lines.Add($"쪽지함에서 보기: {url}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>쪽지함 주소. Portal:BaseUrl 이 비면 링크 줄을 뺀다 — localhost 주소가 적힌 메일은 아무도 못 연다.</summary>
    private string? InboxUrl()
    {
        var baseUrl = (configuration["Portal:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');

        return baseUrl.Length == 0 ? null : baseUrl + InboxPath;
    }

    /// <summary>두드림 기록(notify_note)에 한 마디 더한다. 주소 없는 줄을 같은 말로 매 바퀴 다시 적지 않는다.</summary>
    private static void Trouble(Note note, string what)
    {
        if (note.NotifyNote is { Length: > 0 } had)
        {
            if (had.Contains(what, StringComparison.Ordinal)) return;

            note.NotifyNote = had + " · " + what;
            return;
        }

        note.NotifyNote = what;
    }
}
