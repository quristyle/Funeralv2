using AuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthServer.Services;

/// <inheritdoc />
public class PublicFileSyncService : IPublicFileSyncService
{
    /// <summary>이 클래스가 켜고 끈 행에 남기는 표식. 끌 때 이 표식이 있는 행만 건드린다.</summary>
    private const string Marker = "NoticeSync";

    /// <summary>
    /// 파일 하나의 공개 여부를 다시 계산해 맞춘다.
    /// </summary>
    /// <remarks>
    /// 판정 기준은 <b>'공개로 설정된 공지에 붙어 있는가'</b> 다 —
    /// <c>is_public AND status = 1 AND NOT is_deleted</c>.
    ///
    /// 게시 기간(<c>start_at</c> · <c>end_at</c>)은 보지 않는다. 팝업 조회는 그것까지 보지만
    /// (<see cref="NoticeService.GetPopupAsync"/>), 기간은 아무도 저장을 누르지 않아도 지나간다.
    /// 기간까지 반영하려면 주기 작업이 하나 더 필요한데, 얻는 것은 '기간이 끝난 공지의 첨부를
    /// 주소를 아는 사람이 더 받을 수 있다' 를 막는 것뿐이다. 그만한 값이 아니다.
    ///
    /// 한 파일이 여러 공지에 붙어 있을 수 있어 <c>bool_or</c> 로 모은다.
    /// 어느 하나라도 공개면 공개다.
    ///
    /// 마지막 줄이 중요하다 — <c>s.should OR f.updatedby = 'NoticeSync'</c>.
    /// 켜는 것은 언제나 하지만 <b>끄는 것은 이 클래스가 켠 것만</b> 한다.
    /// 소개 사이트 자료실처럼 다른 이유로 공개된 파일을 공지에서 뗐다는 이유로 닫으면 안 된다.
    /// </remarks>
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
