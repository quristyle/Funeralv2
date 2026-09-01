using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <inheritdoc cref="IInfoService"/>
public class InfoService : IInfoService
{
    /// <summary>
    /// 장비 화면을 웹으로 열 수 있는 주소 서식. <c>{code}</c> 자리에 장비 인증 코드가 들어간다.
    /// </summary>
    /// <remarks>
    /// 옛 시스템은 <c>/client_machine/{번호}/index.jsp</c> 를 새 창으로 띄웠다.
    /// 지금 재생 장비는 설치형(.deb 등)이라 그에 해당하는 웹 주소가 **아직 없다.**
    /// 그래서 기본값을 두지 않는다 — 없으면 화면이 "주소가 설정되지 않았다" 고 말한다.
    /// 정해지면 <c>appsettings.Local.json</c> 에 적는다.
    /// 40번 문서의 D-F4.
    /// </remarks>
    public const string PreviewUrlTemplateKey = "Device:PreviewUrlTemplate";

    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public InfoService(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    // ── 알림정보 ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<NoticeDto>> GetNoticesAsync(string userId, string? buildingId, bool includeExpired)
    {
        var now = DateTime.UtcNow;

        var query = _dbContext.FuneralNotices.Where(n => !n.IsDeleted);

        // 받는 사람이 비어 있으면 전체 공지, 적혀 있으면 그 사람만.
        query = query.Where(n => n.TargetUserId == null || n.TargetUserId == string.Empty || n.TargetUserId == userId);

        if (!string.IsNullOrWhiteSpace(buildingId))
        {
            query = query.Where(n => n.BuildingId == null || n.BuildingId == string.Empty || n.BuildingId == buildingId);
        }

        if (!includeExpired)
        {
            query = query.Where(n => (n.StartAt == null || n.StartAt <= now) && (n.EndAt == null || n.EndAt >= now));
        }

        var notices = await query
            .OrderByDescending(n => n.IsImportant)
            .ThenByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return await ToDtosAsync(userId, notices);
    }

    /// <inheritdoc />
    public async Task<NoticeDto?> GetNoticeByIdAsync(string userId, string id)
    {
        var notice = await _dbContext.FuneralNotices
            .Where(n => n.Id == id && !n.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (notice is null)
        {
            return null;
        }

        return (await ToDtosAsync(userId, new List<FuneralNotice> { notice })).FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<NoticeDto> CreateNoticeAsync(string userId, NoticeCreateDto dto)
    {
        var notice = new FuneralNotice
        {
            Title = dto.Title,
            Content = dto.Content,
            NoticeType = string.IsNullOrWhiteSpace(dto.NoticeType) ? "NOTICE" : dto.NoticeType,
            IsImportant = dto.IsImportant,
            TargetUserId = string.IsNullOrWhiteSpace(dto.TargetUserId) ? null : dto.TargetUserId,
            BuildingId = string.IsNullOrWhiteSpace(dto.BuildingId) ? null : dto.BuildingId,
            TargetPage = dto.TargetPage,
            TargetParam = dto.TargetParam,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.FuneralNotices.Add(notice);
        await _dbContext.SaveChangesAsync();

        return (await ToDtosAsync(userId, new List<FuneralNotice> { notice })).First();
    }

    /// <inheritdoc />
    public async Task<NoticeDto?> UpdateNoticeAsync(string userId, string id, NoticeUpdateDto dto)
    {
        var notice = await _dbContext.FuneralNotices.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        if (notice is null)
        {
            return null;
        }

        notice.Title = dto.Title;
        notice.Content = dto.Content;
        notice.NoticeType = string.IsNullOrWhiteSpace(dto.NoticeType) ? "NOTICE" : dto.NoticeType;
        notice.IsImportant = dto.IsImportant;
        notice.TargetUserId = string.IsNullOrWhiteSpace(dto.TargetUserId) ? null : dto.TargetUserId;
        notice.BuildingId = string.IsNullOrWhiteSpace(dto.BuildingId) ? null : dto.BuildingId;
        notice.TargetPage = dto.TargetPage;
        notice.TargetParam = dto.TargetParam;
        notice.StartAt = dto.StartAt;
        notice.EndAt = dto.EndAt;
        notice.UpdatedBy = userId;
        notice.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return (await ToDtosAsync(userId, new List<FuneralNotice> { notice })).First();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteNoticeAsync(string id)
    {
        var notice = await _dbContext.FuneralNotices.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        if (notice is null)
        {
            return false;
        }

        notice.IsDeleted = true;
        notice.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkNoticeReadAsync(string userId, string id)
    {
        var exists = await _dbContext.FuneralNotices.AnyAsync(n => n.Id == id && !n.IsDeleted);
        if (!exists)
        {
            return false;
        }

        var already = await _dbContext.FuneralNoticeReads
            .AnyAsync(r => r.NoticeId == id && r.UserId == userId && !r.IsDeleted);

        if (already)
        {
            return true;
        }

        _dbContext.FuneralNoticeReads.Add(new FuneralNoticeRead
        {
            NoticeId = id,
            UserId = userId,
            ReadAt = DateTime.UtcNow,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<int> CountUnreadNoticesAsync(string userId, string? buildingId)
    {
        var notices = await GetNoticesAsync(userId, buildingId, includeExpired: false);
        return notices.Count(n => !n.IsRead);
    }

    /// <summary>
    /// 알림에 건물 이름과 읽음 여부를 붙인다.
    /// </summary>
    private async Task<List<NoticeDto>> ToDtosAsync(string userId, List<FuneralNotice> notices)
    {
        if (notices.Count == 0)
        {
            return new List<NoticeDto>();
        }

        var ids = notices.Select(n => n.Id).ToList();

        var readIds = await _dbContext.FuneralNoticeReads
            .Where(r => r.UserId == userId && !r.IsDeleted && ids.Contains(r.NoticeId))
            .Select(r => r.NoticeId)
            .ToListAsync();

        var readSet = readIds.ToHashSet();

        var buildingIds = notices
            .Where(n => !string.IsNullOrEmpty(n.BuildingId))
            .Select(n => n.BuildingId!)
            .Distinct()
            .ToList();

        var buildings = buildingIds.Count == 0
            ? new Dictionary<string, string>()
            : await _dbContext.Buildings
                .Where(b => buildingIds.Contains(b.Id))
                .AsNoTracking()
                .ToDictionaryAsync(b => b.Id, b => b.Name);

        return notices.Select(n => new NoticeDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            NoticeType = n.NoticeType,
            IsImportant = n.IsImportant,
            TargetUserId = n.TargetUserId,
            BuildingId = n.BuildingId,
            BuildingName = n.BuildingId != null && buildings.TryGetValue(n.BuildingId, out var bn) ? bn : null,
            TargetPage = n.TargetPage,
            TargetParam = n.TargetParam,
            StartAt = n.StartAt,
            EndAt = n.EndAt,
            Author = n.CreatedBy,
            CreatedAt = n.CreatedAt,
            IsRead = readSet.Contains(n.Id),
        }).ToList();
    }

    // ── 호실 히스토리 ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<RoomHistoryDto>> GetRoomHistoriesAsync(string? buildingId, string? roomId, DateTime? from, DateTime? to)
    {
        var roomQuery = _dbContext.Rooms.Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(buildingId))
        {
            roomQuery = roomQuery.Where(r => r.BuildingId == buildingId);
        }

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            roomQuery = roomQuery.Where(r => r.Id == roomId);
        }

        var rooms = await roomQuery.AsNoTracking().ToDictionaryAsync(r => r.Id);
        if (rooms.Count == 0)
        {
            return new List<RoomHistoryDto>();
        }

        var roomIds = rooms.Keys.ToList();

        var assignQuery = _dbContext.DeceasedRooms
            .Where(a => !a.IsDeleted && roomIds.Contains(a.RoomId));

        // 기간이 겹치는 것을 고른다 — 기간 안에 시작했거나 아직 안 끝난 것.
        if (from.HasValue)
        {
            assignQuery = assignQuery.Where(a => a.EndTime == null || a.EndTime >= from.Value);
        }

        if (to.HasValue)
        {
            assignQuery = assignQuery.Where(a => a.StartTime <= to.Value);
        }

        var assignments = await assignQuery.AsNoTracking().ToListAsync();
        if (assignments.Count == 0)
        {
            return new List<RoomHistoryDto>();
        }

        var deceasedIds = assignments.Select(a => a.DeceasedId).Distinct().ToList();

        var deceaseds = await _dbContext.Deceaseds
            .Where(d => deceasedIds.Contains(d.Id))
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id);

        var buildings = await _dbContext.Buildings.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        var floors = await _dbContext.Floors.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Name);

        var now = DateTime.UtcNow;

        return assignments
            .Select(a =>
            {
                var room = rooms[a.RoomId];
                deceaseds.TryGetValue(a.DeceasedId, out var deceased);

                return new RoomHistoryDto
                {
                    Id = a.Id,
                    RoomId = room.Id,
                    RoomName = room.Name,
                    FloorName = floors.TryGetValue(room.FloorId, out var fn) ? fn : null,
                    BuildingId = room.BuildingId,
                    BuildingName = buildings.TryGetValue(room.BuildingId, out var bn) ? bn : null,
                    DeceasedId = a.DeceasedId,
                    DeceasedName = deceased?.Name ?? "(삭제된 고인)",
                    Gender = deceased?.Gender,
                    Age = deceased?.Age,
                    MemorialPhotoFileId = deceased?.MemorialEditedPhotoFileId ?? deceased?.MemorialPhotoFileId,
                    MemorialPhotoUrl = deceased?.MemorialEditedPhotoUrl ?? deceased?.MemorialPhotoUrl,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    UseDays = StatusService.CountDays(a.StartTime, a.EndTime, now),
                    FuneralDate = deceased?.BurialDate,
                    BurialPlot = deceased?.BurialPlot,
                    InUse = a.EndTime == null,
                    Departed = a.EndTime != null,
                    Status = deceased?.Status ?? string.Empty,
                };
            })
            .OrderByDescending(h => h.StartTime)
            .ToList();
    }

    // ── 고인 정보 조회 ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<DeceasedLookupDto>> SearchDeceasedAsync(
        string? keyword, string? buildingId, string? roomId, DateTime? from, DateTime? to, string? status)
    {
        var query = _dbContext.Deceaseds.Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(d =>
                EF.Functions.ILike(d.Name, $"%{k}%")
                || (d.BurialPlot != null && EF.Functions.ILike(d.BurialPlot, $"%{k}%"))
                || (d.Remark != null && EF.Functions.ILike(d.Remark, $"%{k}%")));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status == status);
        }

        if (from.HasValue)
        {
            query = query.Where(d => d.DeathDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(d => d.DeathDate <= to.Value);
        }

        var deceaseds = await query
            .OrderByDescending(d => d.DeathDate)
            .AsNoTracking()
            .ToListAsync();

        if (deceaseds.Count == 0)
        {
            return new List<DeceasedLookupDto>();
        }

        var ids = deceaseds.Select(d => d.Id).ToList();

        var assignments = await _dbContext.DeceasedRooms
            .Where(a => !a.IsDeleted && ids.Contains(a.DeceasedId))
            .AsNoTracking()
            .ToListAsync();

        // 고인 한 명이 방을 옮겼을 수 있다. 가장 최근 배정을 대표로 삼는다.
        var latest = assignments
            .GroupBy(a => a.DeceasedId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartTime).First());

        var rooms = await _dbContext.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id);
        var buildings = await _dbContext.Buildings.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        var floors = await _dbContext.Floors.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Name);

        var mourners = await _dbContext.DeceasedMourners
            .Where(m => !m.IsDeleted && ids.Contains(m.DeceasedId))
            .AsNoTracking()
            .ToListAsync();

        var mournerByDeceased = mourners
            .GroupBy(m => m.DeceasedId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.OrderByDescending(m => m.IsChief).ThenBy(m => m.SortOrder).Select(m => m.Name)));

        var result = new List<DeceasedLookupDto>(deceaseds.Count);

        foreach (var d in deceaseds)
        {
            latest.TryGetValue(d.Id, out var assign);
            Entities.Room? room = null;
            if (assign is not null)
            {
                rooms.TryGetValue(assign.RoomId, out room);
            }

            // 건물·호실로 좁히는 것은 배정이 있어야 판단할 수 있어 여기서 거른다.
            if (!string.IsNullOrWhiteSpace(buildingId) && room?.BuildingId != buildingId)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(roomId) && room?.Id != roomId)
            {
                continue;
            }

            result.Add(new DeceasedLookupDto
            {
                Id = d.Id,
                Name = d.Name,
                Gender = d.Gender,
                Age = d.Age,
                Religion = d.Religion,
                MemorialPhotoFileId = d.MemorialEditedPhotoFileId ?? d.MemorialPhotoFileId,
                MemorialPhotoUrl = d.MemorialEditedPhotoUrl ?? d.MemorialPhotoUrl,
                DeathDate = d.DeathDate,
                FuneralDate = d.FuneralDate,
                BurialDate = d.BurialDate,
                BurialPlot = d.BurialPlot,
                Status = d.Status,
                RoomId = room?.Id,
                RoomName = room?.Name,
                FloorName = room != null && floors.TryGetValue(room.FloorId, out var fn) ? fn : null,
                BuildingId = room?.BuildingId,
                BuildingName = room != null && buildings.TryGetValue(room.BuildingId, out var bn) ? bn : null,
                StartTime = assign?.StartTime,
                EndTime = assign?.EndTime,
                MournerNames = mournerByDeceased.TryGetValue(d.Id, out var mn) ? mn : null,
                CreatedAt = d.CreatedAt,
            });
        }

        return result;
    }

    // ── 나의 정보 ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<MyInfoDto> GetMyInfoAsync(string userId, string? role)
    {
        var settings = await _dbContext.AccountSettings
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        var buildingCount = await _dbContext.Buildings.CountAsync(b => !b.IsDeleted);
        var roomsInUse = await _dbContext.DeceasedRooms.CountAsync(a => !a.IsDeleted && a.EndTime == null);
        var unread = await CountUnreadNoticesAsync(userId, null);

        return new MyInfoDto
        {
            UserId = userId,
            Role = role,
            BuildingCount = buildingCount,
            RoomsInUse = roomsInUse,
            UnreadNoticeCount = unread,
            Settings = SettingCatalog.Merge(settings),
        };
    }

    // ── 미리보기 ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<DevicePreviewDto>> GetDevicePreviewsAsync(string? buildingId, string? roomId)
    {
        var query = _dbContext.Devices.Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(buildingId))
        {
            query = query.Where(d => d.BuildingId == buildingId);
        }

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            query = query.Where(d => d.RoomId == roomId);
        }

        var devices = await query
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();

        if (devices.Count == 0)
        {
            return new List<DevicePreviewDto>();
        }

        var rooms = await _dbContext.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name);
        var buildings = await _dbContext.Buildings.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);

        var template = _configuration[PreviewUrlTemplateKey];

        return devices.Select(d => new DevicePreviewDto
        {
            Id = d.Id,
            Name = d.Name,
            DeviceCode = d.Code,
            DeviceType = d.DeviceType,
            RoomId = d.RoomId,
            RoomName = d.RoomId != null && rooms.TryGetValue(d.RoomId, out var rn) ? rn : null,
            BuildingId = d.BuildingId,
            BuildingName = d.BuildingId != null && buildings.TryGetValue(d.BuildingId, out var bn) ? bn : null,
            IsOnline = d.Status == "ONLINE",
            LastConnectedAt = d.LastSeenAt,

            PreviewUrl = BuildPreviewUrl(template, d.Code),
        }).ToList();
    }

    /// <summary>
    /// 설정된 서식에 장비 코드를 끼워 미리보기 주소를 만든다.
    /// 서식이 없거나 코드가 없으면 빈 문자열이다 — 화면이 그때 버튼을 잠근다.
    /// </summary>
    internal static string BuildPreviewUrl(string? template, string? code)
    {
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return template.Replace("{code}", Uri.EscapeDataString(code), StringComparison.OrdinalIgnoreCase);
    }
}
