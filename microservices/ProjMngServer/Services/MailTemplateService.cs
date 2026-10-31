using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>메일 HTML 템플릿(<c>projmng.mail_template</c>).</summary>
public sealed class MailTemplateService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string Columns = """
        tpl_id    AS TplId,
        tpl_kind  AS TplKind,
        tpl_nm    AS TplNm,
        tpl_desc  AS TplDesc,
        subject   AS Subject,
        body_html AS BodyHtml,
        use_yn    AS UseYn,
        sort_no   AS SortNo,
        cre_id    AS CreId,
        cre_dt    AS CreDt,
        mod_id    AS ModId,
        mod_dt    AS ModDt
        """;

    /// <summary>템플릿 목록. <paramref name="usableOnly"/> 면 쓰는 것만.</summary>
    public async Task<List<MailTemplate>> ListAsync(string? kind, bool usableOnly, long? tplId = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<MailTemplate>($"""
            SELECT {Columns}
              FROM projmng.mail_template
             WHERE (@tplId::bigint IS NULL OR tpl_id = @tplId)
               AND (@kind = '' OR tpl_kind = @kind)
               AND (NOT @usableOnly OR use_yn)
             ORDER BY sort_no, tpl_nm, tpl_id
            """, new { tplId, kind = kind ?? string.Empty, usableOnly });

        return [.. rows];
    }

    public async Task<MailTemplate?> CreateAsync(MailTemplate item, string userId)
    {
        using var db = Open();

        var id = await db.ExecuteScalarAsync<long>("""
            INSERT INTO projmng.mail_template
                 ( tpl_kind, tpl_nm, tpl_desc, subject, body_html, use_yn, sort_no, cre_id, cre_dt )
            VALUES ( @TplKind, @TplNm, @TplDesc, @Subject, @BodyHtml, @UseYn, @SortNo, @userId, now() )
            RETURNING tpl_id
            """, new
        {
            TplKind = string.IsNullOrWhiteSpace(item.TplKind) ? MailTemplateKinds.Schedule : item.TplKind.Trim(),
            TplNm = item.TplNm!.Trim(),
            item.TplDesc, item.Subject, item.BodyHtml, item.UseYn, item.SortNo, userId,
        });

        return (await ListAsync(null, false, id)).FirstOrDefault();
    }

    public async Task<MailTemplate?> UpdateAsync(long tplId, MailTemplate item, string userId)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.mail_template
               SET tpl_kind  = @TplKind,
                   tpl_nm    = @TplNm,
                   tpl_desc  = @TplDesc,
                   subject   = @Subject,
                   body_html = @BodyHtml,
                   use_yn    = @UseYn,
                   sort_no   = @SortNo,
                   mod_id    = @userId,
                   mod_dt    = now()
             WHERE tpl_id = @tplId
            """, new
        {
            tplId,
            TplKind = string.IsNullOrWhiteSpace(item.TplKind) ? MailTemplateKinds.Schedule : item.TplKind.Trim(),
            TplNm = item.TplNm!.Trim(),
            item.TplDesc, item.Subject, item.BodyHtml, item.UseYn, item.SortNo, userId,
        });

        return affected == 0 ? null : (await ListAsync(null, false, tplId)).FirstOrDefault();
    }

    public async Task<bool> DeleteAsync(long tplId)
    {
        using var db = Open();
        return await db.ExecuteAsync("DELETE FROM projmng.mail_template WHERE tpl_id = @tplId", new { tplId }) > 0;
    }
}
