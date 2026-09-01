using funeralv2Api.Data;
using funeralv2Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <inheritdoc cref="IStatusService"/>
/// <remarks>
/// 옛 시스템은 현황 화면마다 프로시저가 따로 있었다
/// (<c>monitor/room_status</c> · <c>room_status_simple</c> · <c>mobile/room_status</c>).
/// 셋이 담는 내용은 같고 그리는 모양만 달라서, 여기서는 한 벌만 만들고
/// 화면이 필요한 것만 골라 그린다.
/// </remarks>
public class StatusService : IStatusService
{
    private readonly AppDbContext _dbContext;

    public StatusService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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

    /// <summary>
    /// 호실을 축으로 현재 배정과 장비 수를 붙인다.
    /// </summary>
    /// <remarks>
    /// 조회를 네 번으로 나눈 이유는 EF 가 한 번에 엮으면 호실 × 장비 × 상주로
    /// 행이 불어나 같은 고인을 여러 번 세기 때문이다. 호실 수가 백 단위라
    /// 나눠 읽고 메모리에서 맞추는 편이 싸다.
    /// </remarks>
    private async Task<List<FuneralStatusDto>> BuildAsync(string? buildingId, string? floorId, string? roomId)
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

        // 지금 배정된 것만. 끝난 배정(EndTime 이 있는 것)은 이력이지 현황이 아니다.
        var assignments = await _dbContext.DeceasedRooms
            .Where(a => !a.IsDeleted && roomIds.Contains(a.RoomId) && a.EndTime == null)
            .AsNoTracking()
            .ToListAsync();

        var current = assignments
            .GroupBy(a => a.RoomId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartTime).First());

        var deceasedIds = current.Values.Select(a => a.DeceasedId).Distinct().ToList();

        var deceaseds = await _dbContext.Deceaseds
            .Where(d => !d.IsDeleted && deceasedIds.Contains(d.Id))
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id);

        var mourners = await _dbContext.DeceasedMourners
            .Where(m => !m.IsDeleted && deceasedIds.Contains(m.DeceasedId))
            .AsNoTracking()
            .ToListAsync();

        var chiefByDeceased = mourners
            .GroupBy(m => m.DeceasedId)
            .ToDictionary(
                g => g.Key,
                // 상주로 표시된 사람이 먼저, 없으면 순서대로. 옛 화면도 상주 한 명만 보여 줬다.
                g => string.Join(", ", g.OrderByDescending(m => m.IsChief).ThenBy(m => m.SortOrder).Select(m => m.Name)));

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

        var now = DateTime.UtcNow;
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
                dto.DeceasedGender = deceased.Gender;
                dto.DeceasedAge = deceased.Age;
                dto.Religion = deceased.Religion;

                // 보정본이 있으면 그것을 먼저 쓴다 — 영정은 잘라 낸 것이 정본이다.
                dto.PhotoFileId = deceased.MemorialEditedPhotoFileId ?? deceased.MemorialPhotoFileId;
                dto.PhotoUrl = deceased.MemorialEditedPhotoUrl ?? deceased.MemorialPhotoUrl;

                dto.CoffinTime = deceased.FuneralDate;
                dto.DischargeTime = deceased.BurialDate;
                dto.BurialPlace = deceased.BurialPlot;
                dto.StartTime = assign.StartTime;
                dto.UseDays = CountDays(assign.StartTime, null, now);

                if (chiefByDeceased.TryGetValue(deceased.Id, out var chief))
                {
                    dto.ChiefMourner = chief;
                }

                if (assign.StartTime > dto.UpdatedAt)
                {
                    dto.UpdatedAt = assign.StartTime;
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

    /// <summary>
    /// 사용 일수를 센다. 하루가 안 돼도 하루로 친다 — 옛 시스템의 셈법이다.
    /// </summary>
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
