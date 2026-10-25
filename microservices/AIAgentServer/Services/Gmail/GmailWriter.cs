using System.Text;

using AIAgentServer.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

namespace AIAgentServer.Services.Gmail;

/// <summary>메일 줄을 표에 넣는다. 길 셋(Takeout·API·IMAP)이 모두 이것을 쓴다 — 겹침을 버리는 규칙이
/// 길마다 달라지면 안 되기 때문이다.
///
/// <para>넣기는 <c>ON CONFLICT DO NOTHING</c> 이라 <b>몇 번을 돌려도 안전하다.</b> 돌려주는 수는
/// <b>실제로 들어간 줄</b>이므로, 넘긴 수와의 차이가 곧 겹쳐서 버린 수다.</para></summary>
public sealed class GmailWriter(AiUsageDbContext db)
{
    private const string InsertHead =
        """
        INSERT INTO scom.gmail_messages
            (account_id, source, provider_msg_id, thread_id, rfc822_message_id,
             subject, from_addr, from_name, to_addrs, cc_addrs, sent_at,
             labels, snippet, body_text, body_html, has_attachments, attachments, created_at)
        VALUES
        """;

    private const int ColumnCount = 18;

    /// <summary>칸마다의 보내는 형식. 비어 있는 칸(null)은 형식을 알려 주지 않으면 Npgsql 이 무엇으로
    /// 보낼지 못 정한다 — 그래서 값이 아니라 <b>자리</b>에 형식을 못박는다.
    /// jsonb 칸은 글로 보내고 SQL 에서 <c>::jsonb</c> 로 바꾼다.</summary>
    private static readonly NpgsqlDbType[] ColumnTypes =
    [
        NpgsqlDbType.Text,        // account_id
        NpgsqlDbType.Text,        // source
        NpgsqlDbType.Text,        // provider_msg_id
        NpgsqlDbType.Text,        // thread_id
        NpgsqlDbType.Text,        // rfc822_message_id
        NpgsqlDbType.Text,        // subject
        NpgsqlDbType.Text,        // from_addr
        NpgsqlDbType.Text,        // from_name
        NpgsqlDbType.Text,        // to_addrs  → jsonb
        NpgsqlDbType.Text,        // cc_addrs  → jsonb
        NpgsqlDbType.TimestampTz, // sent_at
        NpgsqlDbType.Text,        // labels    → jsonb
        NpgsqlDbType.Text,        // snippet
        NpgsqlDbType.Text,        // body_text
        NpgsqlDbType.Text,        // body_html
        NpgsqlDbType.Boolean,     // has_attachments
        NpgsqlDbType.Text,        // attachments → jsonb
        NpgsqlDbType.TimestampTz, // created_at
    ];

    /// <summary>글로 보내고 SQL 에서 jsonb 로 바꿀 칸의 자리.</summary>
    private static readonly HashSet<int> JsonColumns = [8, 9, 11, 16];

    /// <summary>한 묶음을 한 문장으로 넣는다. 줄마다 왕복하면 수십만 통에서 시간이 통째로 거기 간다.</summary>
    /// <returns>실제로 들어간 줄 수. 넘긴 수와의 차이가 겹쳐서 버린 수다.</returns>
    public async Task<int> InsertAsync(IReadOnlyList<GmailMessageRow> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return 0;

        var sql = new StringBuilder(InsertHead);
        var parameters = new List<object>(batch.Count * ColumnCount);

        for (var i = 0; i < batch.Count; i++)
        {
            var r = batch[i];
            object?[] values =
            [
                r.AccountId, r.Source, r.ProviderMsgId, r.ThreadId, r.Rfc822MessageId,
                r.Subject, r.FromAddr, r.FromName, r.ToAddrs, r.CcAddrs, r.SentAt,
                r.Labels, r.Snippet, r.BodyText, r.BodyHtml, r.HasAttachments, r.Attachments, r.CreatedAt,
            ];

            sql.Append(i == 0 ? " (" : ", (");
            for (var c = 0; c < ColumnCount; c++)
            {
                var name = $"p{parameters.Count}";
                if (c > 0) sql.Append(", ");
                sql.Append('@').Append(name);
                if (JsonColumns.Contains(c)) sql.Append("::jsonb");

                parameters.Add(new NpgsqlParameter(name, ColumnTypes[c])
                {
                    Value = values[c] ?? DBNull.Value,
                });
            }

            sql.Append(')');
        }

        // 겹침은 조용히 버린다 — Takeout 과 증분이 겹치는 구간은 늘 생긴다.
        sql.Append(" ON CONFLICT DO NOTHING");

        return await db.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, ct);
    }
}
