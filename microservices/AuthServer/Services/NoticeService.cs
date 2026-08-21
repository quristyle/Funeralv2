using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 공지 서비스 구현체
/// </summary>
/// <remarks>
/// 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
/// </remarks>
public class NoticeService : INoticeService
{
    private readonly AppDbContext _db;

    public NoticeService(AppDbContext db)
    {
        _db = db;
    }

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

        return ToDto(notice);
    }

    public async Task<bool> UpdateAsync(string id, SaveNoticeDto request, string? userId)
    {
        var notice = await _db.Notices
            .Include(n => n.Files)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (notice is null) return false;

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

        // 첨부파일은 보내온 목록이 곧 최종 상태다.
        // 빠진 것은 지우고, 새로 들어온 것만 추가한다.
        // (FileServer 의 실물 파일은 지우지 않는다 — 다른 곳에서 참조할 수 있고,
        //  공지에서 뗀 것만으로 원본을 없애면 되돌릴 수 없다.)
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
        return true;
    }

    public async Task<bool> DeleteAsync(string id, string? userId)
    {
        var notice = await _db.Notices.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        if (notice is null) return false;

        notice.IsDeleted = true;
        notice.UpdatedAt = DateTime.UtcNow;
        notice.UpdatedBy = userId;

        await _db.SaveChangesAsync();
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

    /// <summary>
    /// 화면에서 온 일시를 UTC 로 맞춘다.
    /// Npgsql 의 timestamptz 는 Kind 가 Utc 가 아니면 저장 시 예외를 낸다.
    /// </summary>
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
