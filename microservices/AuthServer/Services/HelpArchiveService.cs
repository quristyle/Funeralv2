using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 자료실 서비스 구현체
/// </summary>
/// <remarks>
/// 자료실은 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
///
/// 쓰기 권한은 화면을 믿지 않고 여기서 다시 확인한다. 화면의 `v-perm` 은
/// 버튼을 숨기는 장치일 뿐, 요청을 직접 보내면 그냥 통과하기 때문이다.
/// </remarks>
public class HelpArchiveService : IHelpArchiveService
{
    /// <summary>권한을 읽어 올 메뉴 경로</summary>
    private const string MenuPath = "/help/archive";

    private readonly AppDbContext _db;
    private readonly IMenuService _menus;

    public HelpArchiveService(AppDbContext db, IMenuService menus)
    {
        _db = db;
        _menus = menus;
    }

    /// <inheritdoc />
    public async Task<bool> CanManageAsync(string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);

        // 등록·수정·삭제 중 하나라도 있으면 관리자로 본다.
        // 셋을 따로 보면 "수정은 되는데 목록에 비활성이 안 보인다" 같은 어긋남이 생긴다.
        return perm.CanCreate || perm.CanUpdate || perm.CanDelete;
    }

    /// <inheritdoc />
    public async Task<HelpArchiveListDto> GetListAsync(string userId, string? keyword, string? category)
    {
        var canManage = await CanManageAsync(userId);

        var query = _db.HelpArchives
            .Include(a => a.Files!.Where(f => !f.IsDeleted))
            .Where(a => !a.IsDeleted);

        // 비활성 항목은 관리자에게만 보인다.
        if (!canManage) query = query.Where(a => a.Status == 1);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(a => a.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            // 파일명도 검색 대상에 넣는다. 자료실에서는 "그 파일 어디 있지" 로 찾는 일이 많다.
            query = query.Where(a =>
                EF.Functions.ILike(a.Title, $"%{kw}%") ||
                (a.Description != null && EF.Functions.ILike(a.Description, $"%{kw}%")) ||
                a.Files!.Any(f => !f.IsDeleted && EF.Functions.ILike(f.FileName, $"%{kw}%")));
        }

        var list = await query
            .OrderBy(a => a.Category)
            .ThenBy(a => a.OrderNo)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        // 분류 추천 목록은 검색 조건과 무관하게 전체에서 뽑는다.
        // 검색 결과에만 있는 분류를 보여 주면 등록 창의 선택지가 검색에 따라 흔들린다.
        var categories = await _db.HelpArchives
            .Where(a => !a.IsDeleted && a.Category != null && a.Category != "")
            .Select(a => a.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return new HelpArchiveListDto
        {
            Items = list.Select(ToDto).ToList(),
            CanManage = canManage,
            Categories = categories
        };
    }

    /// <inheritdoc />
    public async Task<HelpArchiveDto?> GetByIdAsync(string userId, string id)
    {
        var canManage = await CanManageAsync(userId);

        var archive = await _db.HelpArchives
            .Include(a => a.Files!.Where(f => !f.IsDeleted))
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && (canManage || a.Status == 1));

        return archive is null ? null : ToDto(archive);
    }

    /// <inheritdoc />
    public async Task<HelpArchiveDto?> CreateAsync(SaveHelpArchiveDto request, string userId)
    {
        if (!await CanManageAsync(userId)) return null;

        var archive = new HelpArchive
        {
            Category = Normalize(request.Category),
            Title = request.Title.Trim(),
            Description = request.Description,
            OrderNo = request.OrderNo,
            Status = request.Status
        };

        _db.HelpArchives.Add(archive);
        AddFiles(archive.Id, request.Files);

        await _db.SaveChangesAsync();

        // 방금 넣은 첨부까지 담아 돌려준다.
        return await GetByIdAsync(userId, archive.Id);
    }

    /// <inheritdoc />
    public async Task<HelpArchiveSaveResult> UpdateAsync(string id, SaveHelpArchiveDto request, string userId)
    {
        if (!await CanManageAsync(userId)) return HelpArchiveSaveResult.Forbidden;

        var archive = await _db.HelpArchives
            .Include(a => a.Files)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (archive is null) return HelpArchiveSaveResult.NotFound;

        archive.Category = Normalize(request.Category);
        archive.Title = request.Title.Trim();
        archive.Description = request.Description;
        archive.OrderNo = request.OrderNo;
        archive.Status = request.Status;

        // ── 첨부 맞추기 ────────────────────────────────────────
        //
        // 보낸 목록이 그대로 남는다. 빠진 것은 지우고 새로 온 것은 넣는다.
        // 이미 있는 것은 건드리지 않는다 — 지우고 다시 넣으면
        // 파일별 다운로드 수가 초기화된다.
        var keep = request.Files.Select(f => f.FileId).ToHashSet();
        var existing = (archive.Files ?? new List<HelpArchiveFile>())
            .Where(f => !f.IsDeleted)
            .ToList();

        foreach (var file in existing.Where(f => !keep.Contains(f.FileId)))
        {
            file.IsDeleted = true;
        }

        var have = existing.Select(f => f.FileId).ToHashSet();
        AddFiles(archive.Id, request.Files.Where(f => !have.Contains(f.FileId)));

        // 남긴 파일의 순서는 보낸 대로 다시 맞춘다.
        foreach (var file in existing.Where(f => keep.Contains(f.FileId)))
        {
            var sent = request.Files.First(f => f.FileId == file.FileId);
            file.SortNo = sent.SortNo;
        }

        await _db.SaveChangesAsync();
        return HelpArchiveSaveResult.Ok;
    }

    /// <inheritdoc />
    public async Task<HelpArchiveSaveResult> DeleteAsync(string id, string userId)
    {
        if (!await CanManageAsync(userId)) return HelpArchiveSaveResult.Forbidden;

        var archive = await _db.HelpArchives
            .Include(a => a.Files)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (archive is null) return HelpArchiveSaveResult.NotFound;

        archive.IsDeleted = true;

        // 첨부도 같이 내린다. FileServer 의 실제 파일은 지우지 않는다 —
        // 같은 파일을 다른 곳(공지 등)에서 참조하고 있을 수 있다.
        foreach (var file in archive.Files ?? new List<HelpArchiveFile>())
        {
            file.IsDeleted = true;
        }

        await _db.SaveChangesAsync();
        return HelpArchiveSaveResult.Ok;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveDownloadAsync(string userId, string archiveId, string fileId)
    {
        var canManage = await CanManageAsync(userId);

        var file = await _db.HelpArchiveFiles
            .Include(f => f.Archive)
            .FirstOrDefaultAsync(f =>
                f.ArchiveId == archiveId &&
                f.FileId == fileId &&
                !f.IsDeleted &&
                f.Archive != null &&
                !f.Archive.IsDeleted &&
                (canManage || f.Archive.Status == 1));

        if (file is null) return null;

        // 세는 데 실패해도 내려받기는 막지 않는다 — 숫자는 곁들이는 정보다.
        try
        {
            file.DownloadCount += 1;
            if (file.Archive is not null) file.Archive.DownloadCount += 1;
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 무시한다.
        }

        return DownloadUrl(fileId);
    }

    /// <summary>분류는 공백만 있으면 없는 것으로 본다.</summary>
    private static string? Normalize(string? category) =>
        string.IsNullOrWhiteSpace(category) ? null : category.Trim();

    /// <summary>FileServer 의 실제 내려받기 주소.</summary>
    private static string DownloadUrl(string fileId) => $"/api/file/download/id/{fileId}";

    private void AddFiles(string archiveId, IEnumerable<SaveHelpArchiveFileDto> files)
    {
        foreach (var f in files)
        {
            if (string.IsNullOrWhiteSpace(f.FileId)) continue;

            _db.HelpArchiveFiles.Add(new HelpArchiveFile
            {
                ArchiveId = archiveId,
                FileId = f.FileId,
                FileName = f.FileName,
                FileSize = f.FileSize,
                ContentType = f.ContentType,
                SortNo = f.SortNo
            });
        }
    }

    private static HelpArchiveDto ToDto(HelpArchive a) => new()
    {
        Id = a.Id,
        Category = a.Category,
        Title = a.Title,
        Description = a.Description,
        OrderNo = a.OrderNo,
        Status = a.Status,
        DownloadCount = a.DownloadCount,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
        Files = (a.Files ?? new List<HelpArchiveFile>())
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.SortNo)
            .ThenBy(f => f.FileName)
            .Select(f => new HelpArchiveFileDto
            {
                Id = f.Id,
                FileId = f.FileId,
                FileName = f.FileName,
                FileSize = f.FileSize,
                ContentType = f.ContentType,
                SortNo = f.SortNo,
                DownloadCount = f.DownloadCount,
                // 화면은 이 주소만 열면 된다. 세는 것과 넘기는 것은 서버가 한다.
                DownloadUrl = $"/api/auth/help/archives/{a.Id}/files/{f.FileId}/download"
            })
            .ToList()
    };
}
