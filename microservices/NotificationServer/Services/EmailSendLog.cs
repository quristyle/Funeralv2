using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.Entities;

namespace NotificationServer.Services;

/// <summary>
/// 메일 직발송을 <b>푸시와 같은 표</b>(<c>scom.push_send_logs</c>)에 남긴다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 표를 새로 만들지 않았나]
/// </para>
///
/// <para>
/// <see cref="PushSendLog.Channel"/> 은 처음부터 이것을 위해 있던 칸이다
/// (그 머리말: <i>"같은 화면에서 이메일·문자·카카오도 보내기 때문"</i>).
/// 표를 새로 만들면 <b>「보낸 기록」이 다시 갈라진다</b> — 「이 사람에게
/// 무엇이 나갔나」를 물을 때마다 두 표를 합쳐 봐야 한다.
/// </para>
///
/// <para>
/// [메일은 한 통이 아니라 <b>받는 사람 수만큼</b> 줄이 된다]
/// </para>
///
/// <para>
/// 푸시가 기기마다 한 줄인 것과 같은 규칙이다. 주소 셋에 보낸 한 통을 한 줄로
/// 두면 <b>그중 누구에게 갔는지</b>를 그 줄에서 읽을 수 없고, 계정별 현황에서
/// 그 사람 것만 골라낼 수도 없다. 한 번의 발송은 <see cref="PushSendLog.BatchId"/>
/// 로 묶인다.
/// </para>
///
/// <para>
/// [주소를 계정으로 되돌려 적는다]
/// </para>
///
/// <para>
/// 받는 사람은 세 길(<c>to</c> · <c>toUser</c> · <c>toRole</c>)로 들어오는데
/// 셋 모두 마지막에는 <b>주소 목록</b>이 된다. 그 주소가 어느 계정의 것인지
/// 여기서 한 번 되짚어 <c>owner_key</c> 에 로그인 아이디를 적는다 — 안 그러면
/// 계정 앱 현황 화면이 <c>toUser</c> 로 보낸 것만 알아보고, 같은 사람에게
/// 주소로 보낸 메일은 남의 것처럼 보인다.
/// </para>
///
/// <para>
/// 못 찾은 주소는 <see cref="OwnerTypeEmail"/> 로 남긴다. 버리지 않는 이유는
/// <b>바깥으로 나간 메일도 발신 기록</b>이기 때문이다 — 문의 회신처럼 계정이
/// 아예 없는 상대가 대부분이다.
/// </para>
/// </remarks>
public static class EmailSendLog
{
    /// <summary>포털 계정 주인. 주인 키는 로그인 아이디다.</summary>
    public const string OwnerTypePortal = "jsini";

    /// <summary>계정을 못 찾은 주소. 주인 키가 <b>메일 주소 그 자체</b>다.</summary>
    public const string OwnerTypeEmail = "email";

    /// <summary>
    /// 본문 미리보기 길이. 표에 원문을 통째로 넣지 않는 까닭은 서식 메일 본문이
    /// 수십 KB 라서다 — 되짚는 데 필요한 것은 앞머리 몇 줄뿐이다.
    /// </summary>
    private const int BodyPreviewLength = 500;

    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex SpacePattern = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// 보낸(또는 보내려다 실패한) 메일을 줄로 남긴다.
    /// </summary>
    /// <remarks>
    /// <b>기록을 못 남겨도 발송은 실패가 아니다.</b> 메일은 이미 나갔으므로
    /// 여기서 던지면 부르는 쪽이 성공을 실패로 되돌려 말하게 된다 —
    /// <c>PushSender.LogAsync</c> 와 같은 자세다.
    /// </remarks>
    public static async Task WriteAsync(
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
            return;
        }

        var owners = await ResolveOwnersAsync(db, recipients, ct);
        var batchId = Guid.NewGuid().ToString();
        var preview = Preview(body, html);
        var now = DateTime.UtcNow;

        foreach (var address in recipients)
        {
            var known = owners.TryGetValue(address, out var loginId);

            db.PushSendLogs.Add(new PushSendLog
            {
                SentAt = now,
                Channel = PushSendLog.ChannelEmail,
                OwnerType = known ? OwnerTypePortal : OwnerTypeEmail,
                OwnerKey = known ? loginId! : address,

                // 푸시에서 이 칸은 「보낸 기기」다. 메일에서 그에 해당하는
                // 것이 받는 주소라 같은 자리에 적는다 — 계정을 못 찾은 줄도
                // 주소가 어디였는지는 남아야 한다.
                Endpoint = address,

                Title = subject,
                Body = preview,
                IsSuccess = success,
                FailureReason = success ? null : failureReason,
                SentBy = sentBy,
                BatchId = batchId,
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "메일 발송 기록을 남기지 못했습니다.");
        }
    }

    /// <summary>
    /// 메일 주소 → 로그인 아이디. 대소문자를 가리지 않는다(주소는 그렇게 쓴다).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>한 주소를 두 계정이 적어 둔 경우가 실제로 있다</b>(운영 DB 에서
    /// <c>administrator</c> 와 <c>quristyle</c> 이 같은 주소를 쓴다). 어느
    /// 쪽이 맞는지 여기서 알 방법은 없으므로 고르는 규칙을 <b>못박아 둔다</b> —
    /// 대표 주소 먼저, 그다음 아이디 순.
    /// </para>
    /// <para>
    /// 순서를 안 정하면 DB 가 주는 대로 받아 <b>같은 주소가 실행할 때마다 다른
    /// 계정에 붙는다.</b> 그러면 그 사람의 앱 현황에서 메일 기록이 있다 없다
    /// 하고, 그 흔들림은 「기록이 사라진다」로 신고가 들어온다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 표에 남길 본문 미리보기. 서식 메일은 <b>태그를 걷어 낸다</b> —
    /// 안 걷으면 목록의 그 칸이 <c>&lt;table style=…</c> 로 시작해 아무것도 읽히지 않는다.
    /// </summary>
    private static string? Preview(string? body, bool html)
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
