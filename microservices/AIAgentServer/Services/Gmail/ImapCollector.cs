using AIAgentServer.Data;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;

using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services.Gmail;

/// <summary>개인 Gmail 을 IMAP 으로 받아 온다.
///
/// <para><b>왜 API 가 아닌가.</b> <c>gmail.readonly</c> 는 제한된 스코프라, 개인 계정은 회사의 내부
/// 앱으로 동의할 수 없고 외부 앱은 구글 보안 심사(CASA)에 걸린다. IMAP 은 2단계 인증을 켜고 앱
/// 비밀번호를 받으면 심사도 만료도 없다.</para>
///
/// <para><b>「전체보관함」만 읽는다.</b> Gmail 의 IMAP 은 라벨을 폴더처럼 보여 주므로, 폴더를 모두
/// 돌면 라벨이 둘 붙은 메일이 두 번 걸린다. <c>\All</c> 한 곳만 보면 모든 메일이 꼭 한 번씩 있다.</para>
///
/// <para><b>UIDVALIDITY 가 바뀌면 UID 는 쓸 수 없다.</b> 서버가 메일함을 다시 만들었다는 뜻이라,
/// 지난 번 UID 는 전혀 다른 메일을 가리킨다. 그때는 표식을 버리고 처음부터 받는다 — 겹침은
/// Message-ID 가 지우므로 줄이 불어나지 않는다.</para></summary>
public sealed class ImapCollector(
    AiUsageDbContext db,
    GmailWriter writer,
    IConfiguration config,
    ILogger<ImapCollector> logger)
{
    private const int BatchSize = 100;

    /// <summary>첫 수집에서 가져올 최대 통수. 과거 전체는 Takeout 이 맡는다.</summary>
    private const int FirstRunCap = 2000;

    public sealed record Result(int Fetched, int Inserted, long? LastUid, string? Note);

    public async Task<Result> SyncAsync(GmailAccountRow account, int max, CancellationToken ct)
    {
        var password = config[$"Gmail:Credentials:{account.Id}:AppPassword"];
        if (string.IsNullOrWhiteSpace(password))
            return new Result(0, 0, null, $"Gmail:Credentials:{account.Id}:AppPassword 가 없습니다.");

        var host = config["Gmail:Imap:Host"] is { Length: > 0 } h ? h : "imap.gmail.com";
        var port = config.GetValue("Gmail:Imap:Port", 993);

        using var client = new ImapClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(account.Email, password, ct);

        try
        {
            var folder = await AllMailAsync(client, ct);
            await folder.OpenAsync(FolderAccess.ReadOnly, ct);

            // 메일함이 다시 만들어졌으면 지난 UID 는 딴 메일을 가리킨다 — 표식을 버린다.
            var reset = account.UidValidity is { } known && known != folder.UidValidity;
            if (reset)
            {
                logger.LogWarning(
                    "IMAP UIDVALIDITY 가 바뀌었습니다({Old} → {New}). 처음부터 받습니다: {Account}",
                    account.UidValidity, folder.UidValidity, account.Id);
            }

            var since = reset ? null : account.LastUid;
            var uids = since is { } last
                ? await folder.SearchAsync(SearchQuery.Uids(new UniqueIdRange(new UniqueId((uint)last + 1), UniqueId.MaxValue)), ct)
                : await folder.SearchAsync(SearchQuery.All, ct);

            // 첫 수집은 최근 것부터 잘라 가져온다.
            var take = since is null ? Math.Min(max, FirstRunCap) : max;
            var picked = uids.OrderByDescending(u => u.Id).Take(take).OrderBy(u => u.Id).ToList();

            var fetched = 0;
            var inserted = 0;
            var batch = new List<GmailMessageRow>(BatchSize);
            var newest = since ?? 0;

            foreach (var uid in picked)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var message = await folder.GetMessageAsync(uid, ct);
                    batch.Add(MailNormalizer.FromMime(message, account.Id, "imap", uid.Id.ToString()));
                    fetched++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "IMAP 메일 {Uid} 를 받지 못했습니다 (건너뜁니다)", uid);
                }

                if (uid.Id > newest) newest = uid.Id;

                if (batch.Count >= BatchSize)
                {
                    inserted += await writer.InsertAsync(batch, ct);
                    batch.Clear();
                }
            }

            if (batch.Count > 0) inserted += await writer.InsertAsync(batch, ct);

            await StampAsync(account.Id, newest, folder.UidValidity, ct);

            logger.LogInformation(
                "IMAP 수집: 계정 {Account} · 받음 {Fetched} · 넣음 {Inserted} · UID {Uid}",
                account.Id, fetched, inserted, newest);

            return new Result(fetched, inserted, newest, null);
        }
        finally
        {
            await client.DisconnectAsync(true, CancellationToken.None);
        }
    }

    /// <summary>「전체보관함」을 찾는다. 이름은 계정 언어에 따라 다르므로(<c>[Gmail]/전체보관함</c> ·
    /// <c>[Gmail]/All Mail</c>) 이름이 아니라 <c>\All</c> 표시로 찾는다.</summary>
    private static async Task<IMailFolder> AllMailAsync(ImapClient client, CancellationToken ct)
    {
        var all = client.GetFolder(SpecialFolder.All);
        if (all is not null) return all;

        // 특별 폴더를 안 알려 주는 서버에서는 받은 편지함으로 물러선다.
        await Task.CompletedTask;
        return client.Inbox;
    }

    private Task StampAsync(string accountId, long lastUid, uint uidValidity, CancellationToken ct) =>
        db.GmailAccounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.LastUid, lastUid)
                .SetProperty(a => a.UidValidity, (long)uidValidity)
                .SetProperty(a => a.LastSyncAt, DateTime.UtcNow)
                .SetProperty(a => a.LastError, (string?)null), ct);
}
