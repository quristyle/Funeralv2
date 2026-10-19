using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <inheritdoc cref="IStatusService"/>
public class StatusService : IStatusService
{
    private readonly AppDbContext _dbContext;
    private readonly IDeviceService _deviceService;

    public StatusService(AppDbContext dbContext, IDeviceService deviceService)
    {
        _dbContext = dbContext;
        _deviceService = deviceService;
    }

    /// <inheritdoc />
    public async Task<FuneralStatusBoardDto> GetBoardAsync(string? buildingId, string? floorId, bool onlyInUse)
    {
        var rows = await BuildAsync(buildingId, floorId, null);

        if (onlyInUse)
        {
            rows = rows.Where(r => r.Status == "USING").ToList();
        }

        return new FuneralStatusBoardDto
        {
            Rooms = rows,
            Summary = new FuneralStatusSummaryDto
            {
                TotalRooms = rows.Count,
                UsingRooms = rows.Count(r => r.Status == "USING"),
                EmptyRooms = rows.Count(r => r.Status != "USING"),
                TotalDevices = rows.Sum(r => r.DeviceCount),
                OnlineDevices = rows.Sum(r => r.OnlineDeviceCount),
            },
        };
    }

    /// <inheritdoc />
    public async Task<FuneralStatusDto?> GetRoomStatusAsync(string roomId)
    {
        var rows = await BuildAsync(null, null, roomId);
        return rows.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<RoomBoardDto> GetRoomBoardAsync(RoomBoardQueryDto query)
    {
        var summaryOnly = string.Equals(query.Detail, "summary", StringComparison.OrdinalIgnoreCase);
        var rows = await BuildAsync(query.BuildingId, query.FloorId, null, query, summaryOnly);

        // 요약 숫자는 살아 있는 장비로만 센다. 장비 서비스는 soft-delete 를 거르지 않으므로(다른 화면과의 계약) 여기서 거른다.
        // 안 거르면 지운 장비가 배너에 '오프라인'으로 남는다.
        var live = await LoadScopedDeviceStatesAsync(query);

        if (summaryOnly)
        {
            // 상황판 밀도: 장비 DTO 조립을 건너뛴다. 공용 장비는 뼈대만 보내야 시설별 오프라인 수가 밀도마다 어긋나지 않는다.
            return new RoomBoardDto
            {
                Rooms = rows,
                CommonDevices = live
                    .Where(d => string.IsNullOrEmpty(d.RoomId))
                    .OrderBy(d => d.SortOrder)
                    .ThenBy(d => d.Name)
                    .Select(d => new DeviceDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Code = d.Code,
                        DeviceType = d.DeviceType,
                        Status = d.Status,
                        SortOrder = d.SortOrder,
                        BuildingId = d.BuildingId,
                        FloorId = d.FloorId,
                    })
                    .ToList(),
                Summary = BuildSummary(rows, live.Count, live.Count(d => d.Status == "ONLINE")),
            };
        }

        // 이름·미디어 합성은 장비 서비스에 있어 거기서 받는다. GetByFilterAsync 는 필터가 전부 비면 빈 목록을 주므로 '회사 전체'는 전체 목록으로 받는다.
        var hasDeviceFilter = !string.IsNullOrWhiteSpace(query.CompanyId)
            || !string.IsNullOrWhiteSpace(query.BuildingId)
            || !string.IsNullOrWhiteSpace(query.FloorId);
        var devices = hasDeviceFilter
            ? await _deviceService.GetByFilterAsync(query.CompanyId, query.BuildingId, query.FloorId, null)
            : await _deviceService.GetAllAsync();

        var liveIds = live.Select(d => d.Id).ToHashSet();
        devices = devices.Where(d => liveIds.Contains(d.Id)).ToList();

        var byRoom = devices
            .Where(d => !string.IsNullOrEmpty(d.RoomId))
            .GroupBy(d => d.RoomId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(d => d.SortOrder).ToList());

        foreach (var row in rows)
        {
            row.Devices = byRoom.TryGetValue(row.RoomId, out var list)
                ? list
                : new List<DeviceDto>();
            row.DeviceCount = row.Devices.Count;
            row.OnlineDeviceCount = row.Devices.Count(d => d.Status == "ONLINE");
        }

        return new RoomBoardDto
        {
            Rooms = rows,
            // 호실에 매이지 않은 장비 — 건물 공용 (입구 안내 등)
            CommonDevices = devices.Where(d => string.IsNullOrEmpty(d.RoomId)).ToList(),
            Summary = BuildSummary(rows, devices.Count, devices.Count(d => d.Status == "ONLINE")),
        };
    }

    private static FuneralStatusSummaryDto BuildSummary(
        List<FuneralStatusDto> rows, int totalDevices, int onlineDevices) => new()
        {
            TotalRooms = rows.Count,
            UsingRooms = rows.Count(r => r.Status == "USING"),
            EmptyRooms = rows.Count(r => r.Status != "USING"),
            TotalDevices = totalDevices,
            OnlineDevices = onlineDevices,
        };

    /// <summary>
    /// 조회 범위의 장비를 id · 상태만 읽는다. 좁히는 순서는 장비 서비스와 같다
    /// (호실 &gt; 층 &gt; 건물 &gt; 회사), 다만 여기서는 조건이 전부 비면 전량이다 —
    /// 대시보드의 '회사 전체' 가 그 경우다.
    /// </summary>
    private async Task<List<DeviceStateRow>> LoadScopedDeviceStatesAsync(RoomBoardQueryDto query)
    {
        var q = _dbContext.Devices.Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.FloorId))
        {
            q = q.Where(d => d.FloorId == query.FloorId);
        }
        else if (!string.IsNullOrWhiteSpace(query.BuildingId))
        {
            q = q.Where(d => d.BuildingId == query.BuildingId);
        }
        else if (!string.IsNullOrWhiteSpace(query.CompanyId))
        {
            var companyBuildingIds = _dbContext.Buildings
                .Where(b => !b.IsDeleted && b.CompanyId == query.CompanyId)
                .Select(b => b.Id);
            q = q.Where(d => d.BuildingId != null && companyBuildingIds.Contains(d.BuildingId));
        }

        return await q
            .Select(d => new DeviceStateRow(
                d.Id, d.Name, d.Code, d.DeviceType, d.Status, d.SortOrder,
                d.RoomId, d.FloorId, d.BuildingId))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// 요약과 공용 장비 뼈대에 쓰는 최소 투영. 탐색 속성도 장비 속성 조인도 없다 —
    /// 시설 수십 곳을 60초마다 세는 것이 이 조회의 용도다.
    /// </summary>
    private sealed record DeviceStateRow(
        string Id, string Name, string Code, string DeviceType, string Status,
        int SortOrder, string? RoomId, string? FloorId, string? BuildingId);

    /// <summary>호실 축으로 배정과 장비 수를 붙인다. 한 번에 엮으면 행이 불어 같은 고인을 여러 번 세므로 조회를 나눠 메모리에서 맞춘다.</summary>
    private async Task<List<FuneralStatusDto>> BuildAsync(
        string? buildingId, string? floorId, string? roomId, RoomBoardQueryDto? board = null,
        bool summaryOnly = false)
    {
        var roomQuery = _dbContext.Rooms.Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            roomQuery = roomQuery.Where(r => r.Id == roomId);
        }

        if (!string.IsNullOrWhiteSpace(buildingId))
        {
            roomQuery = roomQuery.Where(r => r.BuildingId == buildingId);
        }

        if (!string.IsNullOrWhiteSpace(floorId))
        {
            roomQuery = roomQuery.Where(r => r.FloorId == floorId);
        }

        // 호실에는 회사가 없다 — 건물을 거쳐 거른다.
        if (!string.IsNullOrWhiteSpace(board?.CompanyId))
        {
            var companyBuildingIds = _dbContext.Buildings
                .Where(b => !b.IsDeleted && b.CompanyId == board.CompanyId)
                .Select(b => b.Id);
            roomQuery = roomQuery.Where(r => companyBuildingIds.Contains(r.BuildingId));
        }

        var rooms = await roomQuery.AsNoTracking().ToListAsync();
        if (rooms.Count == 0)
        {
            return new List<FuneralStatusDto>();
        }

        var roomIds = rooms.Select(r => r.Id).ToList();

        var buildings = await _dbContext.Buildings
            .Where(b => !b.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(b => b.Id, b => b.Name);

        var floors = await _dbContext.Floors
            .Where(f => !f.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(f => f.Id, f => f.Name);

        // 지금 배정된 것만 — 판정은 RoomOccupancy 기준. 끝나기로 예정된 배정도 아직 현황이다.
        var nowUtc = DateTime.UtcNow;
        var assignments = await _dbContext.DeceasedRooms
            .Where(a => !a.IsDeleted && roomIds.Contains(a.RoomId)
                        && (a.EndTime == null || a.EndTime > nowUtc))
            .AsNoTracking()
            .ToListAsync();

        var current = assignments
            .GroupBy(a => a.RoomId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartTime).First());

        var deceasedIds = current.Values.Select(a => a.DeceasedId).Distinct().ToList();

        // 점유 판정의 정본: 배정이 살아 있고 + 고인이 '장례 진행중'일 때만 사용 중이다.
        var deceaseds = (await _dbContext.Deceaseds
            .Where(d => !d.IsDeleted && deceasedIds.Contains(d.Id))
            .AsNoTracking()
            .ToListAsync())
            .Where(d => DeceasedStatus.IsOccupying(d.Status))
            .ToDictionary(d => d.Id);

        // 대시보드의 고인 필터 — 조건에 안 맞으면 그 호실은 공실로 보인다
        // (호실을 숨기는 것이 아니라 고인만 걸러 낸다. 화면 A 의 기존 동작이다).
        if (board is not null)
        {
            IEnumerable<Deceased> filtered = deceaseds.Values;
            if (!string.IsNullOrWhiteSpace(board.Name))
            {
                filtered = filtered.Where(d => d.Name.Contains(board.Name));
            }

            if (board.CoffinStartDate.HasValue)
            {
                filtered = filtered.Where(d => d.FuneralDate >= board.CoffinStartDate.Value);
            }

            if (board.CoffinEndDate.HasValue)
            {
                filtered = filtered.Where(d => d.FuneralDate <= board.CoffinEndDate.Value);
            }

            if (board.BurialStartDate.HasValue)
            {
                filtered = filtered.Where(d => d.BurialDate >= board.BurialStartDate.Value);
            }

            if (board.BurialEndDate.HasValue)
            {
                filtered = filtered.Where(d => d.BurialDate <= board.BurialEndDate.Value);
            }

            deceaseds = filtered.ToDictionary(d => d.Id);
        }

        // 빈 호실의 마지막 퇴실은 배정 이력의 마지막 종료 시각으로 유도한다. 출상이 배정을 soft-delete 하므로 IsDeleted 를 가리지 않는다.
        // 이미 지난 종료만 센다 — 예정일까지 세면 입실 중인 호실에 미래 퇴실일이 붙는다.
        var endedAssignments = await _dbContext.DeceasedRooms
            .Where(a => roomIds.Contains(a.RoomId) && a.EndTime != null && a.EndTime <= nowUtc)
            .Select(a => new { a.RoomId, a.DeceasedId, a.EndTime })
            .AsNoTracking()
            .ToListAsync();

        var lastEnded = endedAssignments
            .GroupBy(a => a.RoomId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.EndTime).First());

        var lastDepartedNames = new Dictionary<string, string>();
        if (board is not null && lastEnded.Count > 0)
        {
            var lastDeceasedIds = lastEnded.Values.Select(a => a.DeceasedId).Distinct().ToList();
            lastDepartedNames = await _dbContext.Deceaseds
                .Where(d => lastDeceasedIds.Contains(d.Id) && !d.IsDeleted
                            && d.Status == DeceasedStatus.Departed)
                .AsNoTracking()
                .ToDictionaryAsync(d => d.Id, d => d.Name);
        }

        // 상주는 타일에 안 그린다 — 요약 응답에서는 조회 자체를 건너뛴다.
        var chiefByDeceased = new Dictionary<string, string>();
        if (!summaryOnly)
        {
            var mourners = await _dbContext.DeceasedMourners
                .Where(m => !m.IsDeleted && deceasedIds.Contains(m.DeceasedId))
                .AsNoTracking()
                .ToListAsync();

            chiefByDeceased = mourners
                .GroupBy(m => m.DeceasedId)
                .ToDictionary(
                    g => g.Key,
                        // 상주로 표시된 사람이 먼저, 없으면 순서대로.
                    g => string.Join(", ", g.OrderByDescending(m => m.IsChief).ThenBy(m => m.SortOrder).Select(m => m.Name)));
        }

        var devices = await _dbContext.Devices
            .Where(d => !d.IsDeleted && d.RoomId != null && roomIds.Contains(d.RoomId))
            .Select(d => new { d.RoomId, d.Status })
            .AsNoTracking()
            .ToListAsync();

        var deviceByRoom = devices
            .GroupBy(d => d.RoomId!)
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Count(), Online: g.Count(x => x.Status == "ONLINE")));

        // 점유 판정과 사용 일수는 같은 시각을 봐야 한다 — 위에서 잡은 것을 그대로 쓴다.
        var now = nowUtc;
        var result = new List<FuneralStatusDto>(rooms.Count);

        foreach (var room in rooms)
        {
            var dto = new FuneralStatusDto
            {
                RoomId = room.Id,
                RoomName = room.Name,
                RoomShortName = room.ShortName,
                FloorId = room.FloorId,
                FloorName = floors.TryGetValue(room.FloorId, out var fn) ? fn : null,
                BuildingId = room.BuildingId,
                BuildingName = buildings.TryGetValue(room.BuildingId, out var bn) ? bn : null,
                SortOrder = room.SortOrder,
                Status = "EMPTY",
                UpdatedAt = room.UpdatedAt ?? room.CreatedAt,
            };

            if (deviceByRoom.TryGetValue(room.Id, out var counts))
            {
                dto.DeviceCount = counts.Total;
                dto.OnlineDeviceCount = counts.Online;
            }

            if (current.TryGetValue(room.Id, out var assign)
                && deceaseds.TryGetValue(assign.DeceasedId, out var deceased))
            {
                dto.Status = "USING";
                dto.DeceasedId = deceased.Id;
                dto.DeceasedName = deceased.Name;
                dto.DeceasedStatus = DeceasedStatus.Normalize(deceased.Status);

                // 타일이 그리는 것 — 어느 밀도에서나 필요하다.
                dto.CoffinTime = deceased.FuneralDate;
                dto.DischargeTime = deceased.BurialDate;
                dto.StartTime = assign.StartTime;
                dto.UseDays = CountDays(assign.StartTime, null, now);

                // 컬럼 카드에서만 쓰는 것 — 요약 응답에서는 뺀다.
                if (!summaryOnly)
                {
                    dto.DeceasedGender = deceased.Gender;
                    dto.DeceasedAge = deceased.Age;
                    dto.DeathDate = deceased.DeathDate;
                    dto.Religion = deceased.Religion;
                    dto.BurialPlace = deceased.BurialPlot;

                    // 보정본이 있으면 그것을 먼저 쓴다 — 영정은 잘라 낸 것이 정본이다.
                    dto.PhotoFileId = deceased.MemorialEditedPhotoFileId ?? deceased.MemorialPhotoFileId;
                    dto.PhotoUrl = deceased.MemorialEditedPhotoUrl ?? deceased.MemorialPhotoUrl;

                    if (chiefByDeceased.TryGetValue(deceased.Id, out var chief))
                    {
                        dto.ChiefMourner = chief;
                    }
                }

                if (assign.StartTime > dto.UpdatedAt)
                {
                    dto.UpdatedAt = assign.StartTime;
                }
            }

            if (dto.Status == "EMPTY" && lastEnded.TryGetValue(room.Id, out var ended))
            {
                dto.LastVacatedAt = ended.EndTime;
                if (lastDepartedNames.TryGetValue(ended.DeceasedId, out var departedName))
                {
                    dto.LastDepartedDeceasedId = ended.DeceasedId;
                    dto.LastDepartedDeceasedName = departedName;
                }
            }

            result.Add(dto);
        }

        return result
            .OrderBy(r => r.BuildingName)
            .ThenBy(r => r.FloorName)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.RoomName)
            .ToList();
    }

    /// <summary>사용 일수. 하루가 안 돼도 하루로 센다 (옛 시스템과 같은 셈법).</summary>
    internal static int CountDays(DateTime? start, DateTime? end, DateTime now)
    {
        if (start is null)
        {
            return 0;
        }

        // end 가 없으면 아직 쓰는 중이므로 지금까지로 센다.
        DateTime last = end ?? now;
        DateTime first = start.Value;

        if (last < first)
        {
            return 0;
        }

        var days = (int)Math.Ceiling((last - first).TotalDays);
        return days <= 0 ? 1 : days;
    }
}
