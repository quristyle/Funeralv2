using JSini.Shared.Infrastructure.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NotificationServer.Data;
using NotificationServer.Entities;
using NotificationServer.Options;

namespace NotificationServer.Services;

/// <summary>
/// 쪽지 <b>전환 메일</b> — 앱 푸시가 기기에 닿지 않은 쪽지를 메일로 다시 보낸다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 필요한가 — <b>보낸 것과 닿은 것은 다르다</b>]
/// </para>
///
/// <para>
/// 웹푸시는 우리가 푸시 서비스(FCM 등)에 넘기는 데까지만 우리 일이다. 그 뒤는
/// 기기가 켜져 있어야 하고, 안 켜져 있으면 푸시 서비스가 <b>수명(TTL, 기본 6시간)
/// 까지 들고 있다가 조용히 버린다</b>(docs/push-delivery.md). 그래서
/// <c>Note.PushSent</c> 가 참이어도 받는 사람이 영영 모를 수 있는데, 보낸 쪽의
/// 화면에는 「앱 알림 1대」라고 적혀 있다. <b>그 조용한 어긋남</b>을 메운다.
/// </para>
///
/// <para>
/// [무엇을 보고 고르나]
/// </para>
///
/// <list type="bullet">
///   <item><description><b>기기에 안 닿았다</b>(<c>delivered_at IS NULL</c>) —
///   서비스워커가 받으면 되알려 준다(<c>push-sw.js</c> → <c>POST /notes/delivered</c>).
///   닿았는데 안 읽은 것은 <b>건드리지 않는다</b>: 알림은 멀쩡히 받고 나중에
///   보려고 둔 사람에게 메일을 한 통 더 보내는 것은 도움이 아니라 소음이다.</description></item>
///   <item><description><b>안 읽었다</b>(<c>read_at IS NULL</c>) — 어딘가에서
///   이미 열어 봤으면 전해진 것이다.</description></item>
///   <item><description><b>보낼 때 메일이 안 나갔다</b>(<c>email_sent = false</c>) —
///   「쪽지 메일받기」를 켜 둔 사람에게는 그때 이미 갔다.</description></item>
///   <item><description><b>아직 전환 메일을 안 보냈다</b>(<c>fallback_email_at IS NULL</c>) —
///   이 칸이 없으면 5분마다 같은 메일이 나간다.</description></item>
/// </list>
///
/// <para>
/// [<b>본인이 메일을 껐으면 안 보낸다</b>]
/// </para>
///
/// <para>
/// 「쪽지 메일받기」(<c>note_email_enabled</c>)는 <b>보지 않는다</b> — 그것을 켠
/// 사람에게는 보낼 때 이미 나갔으므로, 여기까지 내려온 사람은 전부 안 켠 사람이다.
/// 대신 <b>전체 메일 스위치</b>(<c>email_enabled</c>, 행이 없으면 켜짐)는 지킨다.
/// 그것을 끈 사람은 「메일로는 연락하지 말라」고 말한 것이라, 전환 발송이라고
/// 해서 넘어설 수 있는 뜻이 아니다.
/// </para>
///
/// <para>
/// [<c>ReportMailDispatcher</c> 와 무엇이 닮고 무엇이 다른가]
/// </para>
///
/// <para>
/// 주기로 깨어나 때가 된 줄을 집는 모양은 같다. 다른 것은 <b>다시 보내지 않기
/// 위한 자국을 어디에 찍느냐</b>다 — 그쪽은 배치 줄의 <c>last_sent_at</c> 이고
/// 이쪽은 쪽지 줄의 <c>fallback_email_at</c> 이다. 둘 다 메모리가 아니라 표에
/// 찍는다: 메모리에 두면 서비스를 재기동할 때마다 또 간다.
/// </para>
/// </remarks>
public sealed class NoteFallbackMailer(
    IServiceScopeFactory scopes,
    IOptions<NoteFallbackMailOptions> options,
    IConfiguration configuration,
    ILogger<NoteFallbackMailer> logger) : BackgroundService
{
    private readonly NoteFallbackMailOptions _opt = options.Value;

    /// <summary>
    /// 첫 바퀴를 조금 늦춘다. <b>기동 직후는 DB 도 SMTP 도 아직 안 풀린다</b> —
    /// 그때 들어가면 첫 바퀴가 통째로 실패한다.
    /// </summary>
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

        // **기다리는 자리마다 취소를 받아 둔다.** `ExecuteAsync` 밖으로 새어
        // 나가는 `OperationCanceledException` 은 끄는 길에 호스트가 오류로
        // 적는다 — 멀쩡히 내린 서비스가 「죽었다」로 보인다. 다른 배치
        // (`AuthServer.ReportMailDispatcher`)와 같은 꼴로 적는다.
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
                // **한 바퀴가 깨져도 배치는 산다.** 여기서 새면 다음 차례가
                // 영영 안 오는데, 그러면 아무도 모르는 채로 전환 메일만 멎는다.
                logger.LogError(ex, "쪽지 전환 메일 한 바퀴가 깨졌습니다.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// 한 바퀴 — 때가 된 쪽지를 집어 메일로 돌린다.
    /// </summary>
    private async Task SweepAsync(CancellationToken ct)
    {
        var now = AppTime.UtcNow;
        var cutoff = now.AddMinutes(-Math.Max(_opt.AfterMinutes, 1));
        var floor = now.AddHours(-Math.Max(_opt.LookbackHours, 1));

        using var scope = scopes.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // **조건 넷이 전부 null 비교다.** 부분 색인(`IX_notes_fallback`)이 그
        // 모양에 맞춰져 있어서, 표가 커져도 읽는 것은 아직 처리 안 된 몇 줄뿐이다
        // (AppDbContext 의 OnModelCreating).
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

        // 전체 메일 스위치를 끈 사람. **행이 없으면 켜짐**이라 「끈 사람」을 묻는다
        // (NotificationPreferenceService 머리말).
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

                // **다시 집지 않게 자국을 찍는다.** 본인이 끈 것은 고쳐질 일이
                // 아니므로 기다려 봐야 72시간 동안 5분마다 같은 줄을 읽을 뿐이다.
                note.FallbackEmailAt = now;
                Trouble(note, "앱 알림이 안 닿았으나 받는 사람이 메일을 꺼 두어 전환 발송하지 않았습니다");
                continue;
            }

            if (!addressOf.TryGetValue(note.ReceiverKey, out var to))
            {
                skippedNoAddress++;

                // 주소가 없는 것은 **언젠가 등록되면 풀릴 일**이라 자국을 찍지
                // 않는다 — 되돌아볼 창(LookbackHours)이 닫히면 저절로 빠진다.
                // 대신 보낸 사람이 보낸함에서 알아볼 수 있게 까닭만 적어 둔다.
                Trouble(note, "앱 알림이 안 닿았는데 메일 주소가 없어 전환 발송을 못 했습니다");
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
                sent++;
            }
            catch (Exception ex)
            {
                // **자국을 안 찍는다.** 한 번 실패한 것은 다음 차례에 다시 해 본다 —
                // SMTP 가 잠깐 막힌 것과 「보낼 수 없는 사람」은 다르다.
                logger.LogError(ex, "쪽지 전환 메일을 보내지 못했습니다. note={Note} to={To}",
                    note.Id, note.ReceiverKey);
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "쪽지 전환 메일 {Sent}통. 대상={Due} 주소없음={NoAddr} 메일꺼둠={OptOut}",
            sent, due.Count, skippedNoAddress, skippedOptOut);
    }

    /// <summary>
    /// 메일 본문 — <b>원래 쪽지 위에 「왜 메일로 왔는지」를 한 줄 얹는다.</b>
    /// </summary>
    /// <remarks>
    /// 그 줄이 없으면 받는 사람은 <b>같은 글을 두 번 받았다</b>고 여긴다(앱 알림이
    /// 늦게라도 뜰 수 있다). 쪽지함 주소를 함께 적는 것은 답장하는 길이 거기뿐이기
    /// 때문이다 — 이 메일에 회신해 봐야 발송 계정으로 갈 뿐이다.
    /// </remarks>
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

    /// <summary>
    /// 쪽지함 주소. 설정(<c>Portal:BaseUrl</c>)이 비면 링크 줄을 통째로 뺀다 —
    /// <b>localhost 주소가 적힌 메일</b>은 아무도 못 연다.
    /// </summary>
    private string? InboxUrl()
    {
        var baseUrl = (configuration["Portal:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');

        return baseUrl.Length == 0 ? null : baseUrl + InboxPath;
    }

    /// <summary>
    /// 두드림 기록(<c>notify_note</c>)에 한 마디 더한다. <b>같은 말을 두 번 적지
    /// 않는다</b> — 주소가 없는 줄은 되돌아볼 창이 닫힐 때까지 매 바퀴 걸린다.
    /// </summary>
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
