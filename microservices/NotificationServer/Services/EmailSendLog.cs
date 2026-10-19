using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.Entities;

namespace NotificationServer.Services;

/// <summary>메일 직발송을 푸시와 같은 표(scom.push_send_logs)에 남긴다. 표를 따로 만들면 보낸 기록이 다시 갈라진다.
/// 받는 사람 수만큼 줄이 되고(푸시와 같은 규칙), 주소는 로그인 아이디로 되짚어 적는다.</summary>
public static class EmailSendLog
{
    /// <summary>포털 계정 주인. 주인 키는 로그인 아이디다.</summary>
    public const string OwnerTypePortal = "jsini";

    /// <summary>계정을 못 찾은 주소. 주인 키가 <b>메일 주소 그 자체</b>다.</summary>
    public const string OwnerTypeEmail = "email";

    /// <summary>본문 미리보기 길이. 서식 메일 본문은 수십 KB 라 앞머리 몇 줄만 남긴다.</summary>
    private const int BodyPreviewLength = 500;

    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex SpacePattern = new(@"\s+", RegexOptions.Compiled);

    /// <summary>보낸(또는 실패한) 메일을 줄로 남긴다. 기록을 못 남겨도 발송은 실패가 아니다(메일은 이미 나갔다).
    /// 돌려주는 BatchId 는 포털 보관함 한 줄과 이어지는 끈이다.</summary>
    public static async Task<string?> WriteAsync(
        AppDbContext db,
        ILogger logger,
        IReadOnlyList<string> recipients,
        string? subject,
        string? body,
        bool html,
        string? sentBy,
        bool success,
        string? failureReason,
        CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            return null;
        }

        var owners = await ResolveOwnersAsync(db, recipients, ct);
        var batchId = Guid.NewGuid().ToString();
        var preview = Preview(body, html);
        var now = DateTime.UtcNow;

        var added = new List<PushSendLog>(recipients.Count);

        foreach (var address in recipients)
        {
            var known = owners.TryGetValue(address, out var loginId);

            var row = new PushSendLog
            {
                SentAt = now,
                Channel = PushSendLog.ChannelEmail,
                OwnerType = known ? OwnerTypePortal : OwnerTypeEmail,
                OwnerKey = known ? loginId! : address,

                // 메일에서 이 칸은 받는 주소다 — 계정을 못 찾은 줄도 주소는 남긴다.
                Endpoint = address,

                Title = subject,
                Body = preview,
                IsSuccess = success,
                FailureReason = success ? null : failureReason,
                SentBy = sentBy,
                BatchId = batchId,
            };

            db.PushSendLogs.Add(row);
            added.Add(row);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "메일 발송 기록을 남기지 못했습니다.");

            // 못 적은 줄은 추적기에서 뗀다(남기면 다음 SaveChanges 에 딸려 가서 메일과 푸시가 나간 뒤 발송 전체가 터진다).
            foreach (var row in added) db.Entry(row).State = EntityState.Detached;
        }

        return batchId;
    }

    /// <summary>메일 주소 → 로그인 아이디. 대소문자를 가리지 않는다. 한 주소를 두 계정이 쓰면 대표 주소 먼저, 그다음 아이디 순으로 고른다(실행마다 바뀌지 않게).</summary>
    private static async Task<Dictionary<string, string>> ResolveOwnersAsync(
        AppDbContext db, IReadOnlyList<string> addresses, CancellationToken ct)
    {
        var wanted = addresses
            .Select(a => a.Trim().ToLowerInvariant())
            .Where(a => a.Length > 0)
            .Distinct()
            .ToList();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (wanted.Count == 0)
        {
            return map;
        }

        var rows = await (
            from a in db.Accounts.AsNoTracking()
            where !a.IsDeleted
            join d in db.AccountProfileDetails.AsNoTracking() on a.Id equals d.AccountId
            where d.DetailType == "Email" && !d.IsDeleted && d.Content != ""
                  && wanted.Contains(d.Content.ToLower())
            select new { a.UserId, d.Content, d.IsPrimary })
            .ToListAsync(ct);

        foreach (var row in rows.OrderByDescending(r => r.IsPrimary).ThenBy(r => r.UserId, StringComparer.Ordinal))
        {
            map.TryAdd(row.Content.Trim(), row.UserId);
        }

        return map;
    }

    /// <summary>표에 남길 본문 미리보기. 서식 메일은 태그를 걷어 낸다. 보낸 메일 보관함도 같은 규칙을 쓴다.</summary>
    internal static string? Preview(string? body, bool html)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var text = html ? TagPattern.Replace(body, " ") : body;
        text = SpacePattern.Replace(text, " ").Trim();

        return text.Length <= BodyPreviewLength ? text : text[..BodyPreviewLength] + "…";
    }
}
