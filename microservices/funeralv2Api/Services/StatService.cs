using funeralv2Api.Data;
using funeralv2Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <inheritdoc cref="IStatService"/>
/// <remarks>
/// 옛 시스템의 <c>t_goin_pay</c> 자리를 <c>smfr.deceased_facilities</c> 가 맡는다.
/// 옛 표는 고인 한 명당 세 줄(기본료 · 환경부담금 · 시설관리비)이 고정이었고
/// 금액은 프로시저가 채웠다. 지금은 행마다 단가가 적히므로,
/// <b>비용 행이 하나도 없는 고인에게는 기본 항목을 만들어 보여 준다</b> —
/// 옛 화면이 그렇게 보였기 때문이다. 저장하지는 않는다.
/// </remarks>
public class StatService : IStatService
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 비용 행이 없을 때 보여 줄 기본 항목. 옛 시스템에 박혀 있던 값이다.
    /// 단가를 어디에 둘지는 40번 문서의 D-F1 로 남겨 두었다.
    /// </summary>
    private static readonly (string Title, decimal Price, bool PerDay)[] DefaultItems =
    {
        ("기본료", 120_000m, true),
        ("환경부담금", 50_000m, true),
        ("시설관리비", 30_000m, true),
    };

    public StatService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<List<BillingDto>> GetBillingAsync(string? buildingId, DateTime? from, DateTime? to)
    {
        var context = await LoadAsync(buildingId, null, from, to);
        var now = DateTime.UtcNow;

        var result = new List<BillingDto>();

        foreach (var d in context.Deceaseds.Values.OrderByDescending(d => d.DeathDate))
        {
            context.LatestAssignment.TryGetValue(d.Id, out var assign);
            if (!context.Matched.Contains(d.Id))
            {
                continue;
            }

            Entities.Room? room = null;
            if (assign is not null)
            {
                context.Rooms.TryGetValue(assign.RoomId, out room);
            }

            var useDays = StatusService.CountDays(assign?.StartTime, assign?.EndTime, now);

            var facilities = context.Facilities.TryGetValue(d.Id, out var list)
                ? list
                : new List<Entities.DeceasedFacility>();

            var items = facilities.Count > 0
                ? facilities.Select(f => new BillingItemDto
                {
                    Id = f.Id,
                    Title = f.FacilityType,
                    UnitPrice = f.UnitPrice,
                    // 총액이 이미 적혀 있으면 그것을 믿는다. 0 이면 단가로 셈한다.
                    ApplyPerDay = f.TotalPrice == 0 && f.UnitPrice > 0,
                    Amount = f.TotalPrice > 0 ? f.TotalPrice : f.UnitPrice * Math.Max(useDays, 1),
                    Remark = f.Remark,
                }).ToList()
                : DefaultItems.Select(x => new BillingItemDto
                {
                    Id = string.Empty,
                    Title = x.Title,
                    UnitPrice = x.Price,
                    ApplyPerDay = x.PerDay,
                    Amount = x.PerDay ? x.Price * Math.Max(useDays, 1) : x.Price,
                    Remark = "기본 단가 (등록된 비용 없음)",
                }).ToList();

            result.Add(new BillingDto
            {
                DeceasedId = d.Id,
                DeceasedName = d.Name,
                RoomId = room?.Id,
                RoomName = room?.Name,
                BuildingId = room?.BuildingId,
                BuildingName = room != null && context.Buildings.TryGetValue(room.BuildingId, out var bn) ? bn : null,
                StartTime = assign?.StartTime,
                EndTime = assign?.EndTime,
                UseDays = useDays,
                Items = items,
                TotalAmount = items.Sum(i => i.Amount),
                Status = d.Status,
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<List<RoomUsageDto>> GetRoomUsageAsync(string? buildingId, string? roomId, DateTime? from, DateTime? to)
    {
        var context = await LoadAsync(buildingId, roomId, from, to);
        var now = DateTime.UtcNow;

        var result = new List<RoomUsageDto>();

        foreach (var a in context.Assignments)
        {
            if (!context.Rooms.TryGetValue(a.RoomId, out var room))
            {
                continue;
            }

            context.Deceaseds.TryGetValue(a.DeceasedId, out var deceased);
            var useDays = StatusService.CountDays(a.StartTime, a.EndTime, now);

            var amount = context.Facilities.TryGetValue(a.DeceasedId, out var list) && list.Count > 0
                ? list.Sum(f => f.TotalPrice > 0 ? f.TotalPrice : f.UnitPrice * Math.Max(useDays, 1))
                : DefaultItems.Sum(x => x.PerDay ? x.Price * Math.Max(useDays, 1) : x.Price);

            result.Add(new RoomUsageDto
            {
                Id = a.Id,
                RoomId = room.Id,
                RoomName = room.Name,
                FloorName = context.Floors.TryGetValue(room.FloorId, out var fn) ? fn : null,
                BuildingId = room.BuildingId,
                BuildingName = context.Buildings.TryGetValue(room.BuildingId, out var bn) ? bn : null,
                DeceasedId = a.DeceasedId,
                DeceasedName = deceased?.Name ?? "(삭제된 고인)",
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                UseDays = useDays,
                BillingAmount = amount,
                InUse = a.EndTime == null,
            });
        }

        return result.OrderByDescending(r => r.StartTime).ToList();
    }

    /// <inheritdoc />
    public async Task<StatSummaryDto> GetSummaryAsync(string? buildingId, DateTime? from, DateTime? to)
    {
        var usage = await GetRoomUsageAsync(buildingId, null, from, to);

        return new StatSummaryDto
        {
            DeceasedCount = usage.Select(u => u.DeceasedId).Distinct().Count(),
            RoomUsageCount = usage.Count,
            TotalUseDays = usage.Sum(u => u.UseDays),
            TotalAmount = usage.Sum(u => u.BillingAmount),
        };
    }

    /// <summary>
    /// 두 화면이 쓰는 자료를 한 번에 읽는다.
    /// </summary>
    private async Task<StatContext> LoadAsync(string? buildingId, string? roomId, DateTime? from, DateTime? to)
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
        var roomIds = rooms.Keys.ToList();

        var assignQuery = _dbContext.DeceasedRooms
            .Where(a => !a.IsDeleted && roomIds.Contains(a.RoomId));

        if (from.HasValue)
        {
            assignQuery = assignQuery.Where(a => a.EndTime == null || a.EndTime >= from.Value);
        }

        if (to.HasValue)
        {
            assignQuery = assignQuery.Where(a => a.StartTime <= to.Value);
        }

        var assignments = await assignQuery.AsNoTracking().ToListAsync();
        var deceasedIds = assignments.Select(a => a.DeceasedId).Distinct().ToList();

        var deceaseds = await _dbContext.Deceaseds
            .Where(d => !d.IsDeleted && deceasedIds.Contains(d.Id))
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id);

        var facilities = await _dbContext.DeceasedFacilities
            .Where(f => deceasedIds.Contains(f.DeceasedId))
            .AsNoTracking()
            .ToListAsync();

        return new StatContext
        {
            Rooms = rooms,
            Assignments = assignments,
            LatestAssignment = assignments
                .GroupBy(a => a.DeceasedId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartTime).First()),
            Deceaseds = deceaseds,
            Matched = deceasedIds.ToHashSet(),
            Facilities = facilities.GroupBy(f => f.DeceasedId).ToDictionary(g => g.Key, g => g.ToList()),
            Buildings = await _dbContext.Buildings.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name),
            Floors = await _dbContext.Floors.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Name),
        };
    }

    /// <summary>한 번 읽어 둔 자료 묶음</summary>
    private sealed class StatContext
    {
        public Dictionary<string, Entities.Room> Rooms { get; init; } = new();
        public List<Entities.DeceasedRoom> Assignments { get; init; } = new();
        public Dictionary<string, Entities.DeceasedRoom> LatestAssignment { get; init; } = new();
        public Dictionary<string, Entities.Deceased> Deceaseds { get; init; } = new();
        public HashSet<string> Matched { get; init; } = new();
        public Dictionary<string, List<Entities.DeceasedFacility>> Facilities { get; init; } = new();
        public Dictionary<string, string> Buildings { get; init; } = new();
        public Dictionary<string, string> Floors { get; init; } = new();
    }
}
