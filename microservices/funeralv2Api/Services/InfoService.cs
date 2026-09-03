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

    // ── 호실 히스토리 ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<RoomHistoryDto>> GetRoomHistoriesAsync(
        string? buildingId, string? roomId, DateTime? from, DateTime? to,
        string? keyword, bool? inUse)
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

        // 사용 중 / 출상. 끝난 시각이 있는지로 가린다 (DTO 의 InUse 와 같은 판정이다).
        if (inUse.HasValue)
        {
            assignQuery = inUse.Value
                ? assignQuery.Where(a => a.EndTime == null)
                : assignQuery.Where(a => a.EndTime != null);
        }

        // 성명으로 찾기.
        //
        // 이름은 배정(deceased_rooms)이 아니라 고인(deceaseds)에 있어서, 배정을
        // 먼저 읽고 걸러내면 필요 없는 행까지 다 끌어온다. 이름에 맞는 고인 키를
        // 먼저 구해 배정 질의에 넣는다.
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            var matchedIds = await _dbContext.Deceaseds
                .Where(d => EF.Functions.ILike(d.Name, $"%{trimmed}%"))
                .Select(d => d.Id)
                .ToListAsync();

            if (matchedIds.Count == 0)
            {
                return new List<RoomHistoryDto>();
            }

            assignQuery = assignQuery.Where(a => matchedIds.Contains(a.DeceasedId));
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
                    DepartureDate = deceased?.BurialDate,
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

        return new MyInfoDto
        {
            UserId = userId,
            Role = role,
            BuildingCount = buildingCount,
            RoomsInUse = roomsInUse,
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
