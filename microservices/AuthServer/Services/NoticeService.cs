using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>공지 서비스 구현체</summary>
/// 저장할 때마다 첨부의 익명 열람 허용을 맞춘다 — 공지가 공개인데 첨부가 비공개면 로그인 전 화면에서 404 가 된다.
public class NoticeService : INoticeService
{
    private readonly AppDbContext _db;
    private readonly IPublicFileSyncService _publicFiles;

    public NoticeService(AppDbContext db, IPublicFileSyncService publicFiles)
    {
        _db = db;
        _publicFiles = publicFiles;
    }

    /// <summary>파일 아이디 문자열을 Guid 로 바꾼다. notice_files.file_id 는 text 라 깨진 값이 있을 수 있어 파싱되는 것만 쓴다.</summary>
    private static IEnumerable<Guid> ToGuids(IEnumerable<string> ids) =>
        ids.Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
           .Where(g => g.HasValue)
           .Select(g => g!.Value);

    public async Task<List<NoticeDto>> GetAllAsync(string? keyword)
    {
        var query = _db.Notices
            .Include(n => n.Files.Where(f => !f.IsDeleted))
            .Where(n => !n.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(n =>
                EF.Functions.ILike(n.Title, $"%{kw}%") ||
                (n.Content != null && EF.Functions.ILike(n.Content, $"%{kw}%")));
        }

        var list = await query
            .OrderBy(n => n.OrderNo)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    public async Task<NoticeDto?> GetByIdAsync(string id)
    {
        var notice = await _db.Notices
            .Include(n => n.Files.Where(f => !f.IsDeleted))
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        return notice is null ? null : ToDto(notice);
    }

    public async Task<List<NoticeDto>> GetPopupAsync(bool publicOnly)
    {
        var now = DateTime.UtcNow;

        var query = _db.Notices
            .Include(n => n.Files.Where(f => !f.IsDeleted))
            .Where(n => !n.IsDeleted && n.Status == 1 && n.IsPopup)
            // 게시 기간을 비워 두면 제한 없음으로 본다.
            .Where(n => n.StartAt == null || n.StartAt <= now)
            .Where(n => n.EndAt == null || n.EndAt >= now);

        if (publicOnly)
        {
            query = query.Where(n => n.IsPublic);
        }

        var list = await query
            .OrderBy(n => n.OrderNo)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    public async Task<NoticeDto> CreateAsync(SaveNoticeDto request, string? userId)
    {
        var notice = new Notice
        {
            Title = request.Title,
            Content = request.Content,
            IsPublic = request.IsPublic,
            IsPopup = request.IsPopup,
            StartAt = ToUtc(request.StartAt),
            EndAt = ToUtc(request.EndAt),
            Status = request.Status,
            OrderNo = request.OrderNo,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        foreach (var file in request.Files)
        {
            notice.Files.Add(NewFile(notice.Id, file, userId));
        }

        _db.Notices.Add(notice);
        await _db.SaveChangesAsync();

        // 첨부의 공개 여부를 맞춘다. 저장 뒤에 해야 한다 — 판정이 방금 저장한 공지 행을 읽는다.
        await _publicFiles.SyncAsync(ToGuids(notice.Files.Select(f => f.FileId)));

        return ToDto(notice);
    }

    public async Task<bool> UpdateAsync(string id, SaveNoticeDto request, string? userId)
    {
        var notice = await _db.Notices
            .Include(n => n.Files)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (notice is null) return false;

        // 고치기 전의 첨부 목록. 떼어 낸 파일도 다시 판정해야 공개로 남지 않는다.
        var before = notice.Files.Select(f => f.FileId).ToList();

        notice.Title = request.Title;
        notice.Content = request.Content;
        notice.IsPublic = request.IsPublic;
        notice.IsPopup = request.IsPopup;
        notice.StartAt = ToUtc(request.StartAt);
        notice.EndAt = ToUtc(request.EndAt);
        notice.Status = request.Status;
        notice.OrderNo = request.OrderNo;
        notice.UpdatedAt = DateTime.UtcNow;
        notice.UpdatedBy = userId;

        // 첨부는 보내온 목록이 최종 상태다. 빠진 것은 지우고 새 것만 더한다. FileServer 실물은 다른 곳에서 참조할 수 있어 지우지 않는다.
        var keep = request.Files.Select(f => f.FileId).ToHashSet();

        foreach (var existing in notice.Files.Where(f => !f.IsDeleted))
        {
            if (!keep.Contains(existing.FileId))
            {
                existing.IsDeleted = true;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = userId;
            }
        }

        var already = notice.Files
            .Where(f => !f.IsDeleted)
            .Select(f => f.FileId)
            .ToHashSet();

        foreach (var file in request.Files.Where(f => !already.Contains(f.FileId)))
        {
            notice.Files.Add(NewFile(notice.Id, file, userId));
        }

        await _db.SaveChangesAsync();

        // 고치기 전에 붙어 있던 것과 지금 붙어 있는 것을 모두 다시 판정한다.
        await _publicFiles.SyncAsync(ToGuids(before.Concat(request.Files.Select(f => f.FileId))));

        return true;
    }

    public async Task<bool> DeleteAsync(string id, string? userId)
    {
        var notice = await _db.Notices
            .Include(n => n.Files)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (notice is null) return false;

        notice.IsDeleted = true;
        notice.UpdatedAt = DateTime.UtcNow;
        notice.UpdatedBy = userId;

        await _db.SaveChangesAsync();

        // 지운 공지의 첨부는 더 이상 공개가 아니다.
        await _publicFiles.SyncAsync(ToGuids(notice.Files.Select(f => f.FileId)));

        return true;
    }

    private static NoticeFile NewFile(string noticeId, SaveNoticeFileDto dto, string? userId) => new()
    {
        NoticeId = noticeId,
        FileId = dto.FileId,
        FileName = dto.FileName,
        FileSize = dto.FileSize,
        ContentType = dto.ContentType,
        SortNo = dto.SortNo,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = userId
    };

    /// <summary>화면에서 온 일시를 UTC 로 맞춘다. Npgsql 의 timestamptz 는 Kind 가 Utc 가 아니면 저장할 때 예외가 난다.</summary>
    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } => value,
        { Kind: DateTimeKind.Local } => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    private static NoticeDto ToDto(Notice n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Content = n.Content,
        IsPublic = n.IsPublic,
        IsPopup = n.IsPopup,
        StartAt = n.StartAt,
        EndAt = n.EndAt,
        Status = n.Status,
        OrderNo = n.OrderNo,
        CreatedAt = n.CreatedAt,
        CreatedBy = n.CreatedBy,
        Files = n.Files
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.SortNo)
            .Select(f => new NoticeFileDto
            {
                Id = f.Id,
                FileId = f.FileId,
                FileName = f.FileName,
                FileSize = f.FileSize,
                ContentType = f.ContentType,
                SortNo = f.SortNo
            })
            .ToList()
    };
}
