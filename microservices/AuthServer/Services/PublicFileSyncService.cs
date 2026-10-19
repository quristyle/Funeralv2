using AuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthServer.Services;

/// <inheritdoc />
public class PublicFileSyncService : IPublicFileSyncService
{
    /// <summary>이 클래스가 켜고 끈 행에 남기는 표식. 끌 때 이 표식이 있는 행만 건드린다.</summary>
    private const string Marker = "NoticeSync";

    /// <summary>파일 하나의 공개 여부를 다시 계산해 맞춘다. 공개로 설정된 공지에 붙어 있는지가 기준이다 — is_public AND status = 1 AND NOT is_deleted.</summary>
    private const string Sql = """
        UPDATE scom.filemetadatas AS f
           SET ispublic  = s.should,
               updatedat = now(),
               updatedby = @marker
          FROM (
                SELECT i.id AS file_id,
                       COALESCE(bool_or(n.is_public
                                        AND n.status = 1
                                        AND NOT n.is_deleted
                                        AND NOT nf.is_deleted), false) AS should
                  FROM unnest(@ids) AS i(id)
                  LEFT JOIN scom.notice_files nf ON nf.file_id = i.id::text
                  LEFT JOIN scom.notices      n  ON n.id = nf.notice_id
                 GROUP BY i.id
               ) AS s
         WHERE f.id = s.file_id
           AND f.ispublic IS DISTINCT FROM s.should
           AND (s.should OR f.updatedby = @marker)
        """;

    private readonly AppDbContext _db;
    private readonly ILogger<PublicFileSyncService> _logger;

    public PublicFileSyncService(AppDbContext db, ILogger<PublicFileSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> SyncAsync(IEnumerable<Guid> fileIds)
    {
        var ids = fileIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        try
        {
            var changed = await _db.Database.ExecuteSqlRawAsync(
                Sql,
                new NpgsqlParameter("ids", ids),
                new NpgsqlParameter("marker", Marker));

            if (changed > 0)
            {
                _logger.LogInformation(
                    "공지 첨부의 공개 여부를 {Count}건 맞췄습니다 (대상 {Total}건).", changed, ids.Length);
            }

            return changed;
        }
        catch (Exception ex)
        {
            // 공지 저장 자체는 막지 않는다. 맞추기에 실패하면 첨부가 404 로 보일 뿐이고,
            // 다음 저장이나 docs/sql/notice_public_files.sql 로 다시 맞출 수 있다.
            _logger.LogError(ex, "공지 첨부의 공개 여부를 맞추지 못했습니다.");
            return 0;
        }
    }
}
