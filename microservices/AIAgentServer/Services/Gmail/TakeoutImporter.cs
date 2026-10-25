using System.Diagnostics;

using MimeKit;

namespace AIAgentServer.Services.Gmail;

/// <summary>Google Takeout 이 내려 주는 mbox 파일을 읽어 <c>scom.gmail_messages</c> 에 붓는다.
/// 과거 전체를 한 번에 올리는 길이다 — 증분은 Gmail API·IMAP 이 맡는다.
///
/// <para><b>다시 돌려도 안전하다.</b> 넣기는 <c>ON CONFLICT DO NOTHING</c> 이라 이미 있는 메일은 조용히
/// 지나간다. 중간에 끊겨도 처음부터 다시 돌리면 되고, Takeout 을 새로 받아 겹치는 구간이 생겨도 같다.</para></summary>
public sealed class TakeoutImporter(
    GmailWriter writer,
    GmailImportStatus status,
    ILogger<TakeoutImporter> logger)
{
    /// <summary>한 번에 넣는 줄 수. 본문을 통째로 들고 있으므로 너무 키우면 메모리가 는다.</summary>
    private const int BatchSize = 100;

    /// <summary>깨진 메일이 이만큼 잇따르면 파일이 mbox 가 아니라고 보고 멈춘다. 한두 통 깨진 것과
    /// 애초에 형식이 다른 것을 가르는 자리다.</summary>
    private const int MaxConsecutiveFailures = 50;

    /// <summary>적재를 시작한 뒤 끝까지 돈다. 시작 표시(<see cref="GmailImportStatus.TryBegin"/>)는
    /// 부르는 쪽이 이미 해 두었다고 본다 — 둘이 겹쳐 도는 것을 거기서 막는다.</summary>
    public async Task ImportAsync(string accountId, string mboxPath, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var stream = File.OpenRead(mboxPath);
            var parser = new MimeParser(stream, MimeFormat.Mbox);

            var batch = new List<GmailMessageRow>(BatchSize);
            var consecutiveFailures = 0;

            while (!parser.IsEndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                MimeMessage message;
                try
                {
                    message = await parser.ParseMessageAsync(ct);
                }
                catch (FormatException ex)
                {
                    status.CountFailed();
                    if (++consecutiveFailures >= MaxConsecutiveFailures)
                    {
                        throw new InvalidOperationException(
                            $"메일을 {MaxConsecutiveFailures}통 잇따라 읽지 못했습니다. mbox 파일이 맞는지 확인해 주세요.", ex);
                    }

                    continue;
                }

                consecutiveFailures = 0;

                GmailMessageRow row;
                try
                {
                    row = MailNormalizer.FromMime(message, accountId, "takeout");
                }
                catch (Exception ex)
                {
                    // 한 통이 이상해도 나머지를 포기하지 않는다.
                    logger.LogWarning(ex, "메일 한 통을 변환하지 못했습니다 (건너뜁니다)");
                    status.CountFailed();
                    continue;
                }
                finally
                {
                    message.Dispose();
                }

                batch.Add(row);
                status.CountParsed();

                if (batch.Count >= BatchSize)
                {
                    status.CountInserted(await writer.InsertAsync(batch, ct));
                    batch.Clear();
                    LogProgress(sw);
                }
            }

            if (batch.Count > 0)
            {
                status.CountInserted(await writer.InsertAsync(batch, ct));
                batch.Clear();
            }

            status.Finish(null);
            logger.LogInformation(
                "Takeout 적재 끝: 계정 {Account} · 읽음 {Parsed} · 넣음 {Inserted} · 겹침 {Duplicate} · 실패 {Failed} · {Elapsed}초",
                accountId, status.Parsed, status.Inserted, status.Parsed - status.Inserted, status.Failed,
                sw.Elapsed.TotalSeconds.ToString("F1"));
        }
        catch (OperationCanceledException)
        {
            status.Finish("중단했습니다.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Takeout 적재가 실패했습니다: 계정 {Account} · 파일 {Path}", accountId, mboxPath);
            status.Finish(ex.Message);
        }
    }

    private void LogProgress(Stopwatch sw)
    {
        // 수십만 통을 읽는 동안 아무 말이 없으면 멎은 것과 구별이 안 된다.
        if (status.Parsed % 2000 != 0) return;

        logger.LogInformation(
            "Takeout 적재 중: 읽음 {Parsed} · 넣음 {Inserted} · {Elapsed}초",
            status.Parsed, status.Inserted, sw.Elapsed.TotalSeconds.ToString("F0"));
    }
}
