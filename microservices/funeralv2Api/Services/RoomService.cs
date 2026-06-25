using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;

namespace funeralv2Api.Services;

/// <summary>
/// 호실 관리 서비스 구현체
/// </summary>
public class RoomService : IRoomService
{
    private readonly AppDbContext _context;
    private readonly ILogger<RoomService> _logger;

    public RoomService(AppDbContext context, ILogger<RoomService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 호실 목록 조회 (층 필터 적용)
    /// </summary>
    public async Task<List<RoomDto>> GetRoomsAsync(string? floorId)
    {
        _logger.LogInformation("Retrieving rooms list for FloorId: {FloorId}", floorId);
        
        var query = _context.Rooms
            .Include(r => r.Floor)
            .Where(r => !r.IsDeleted);

        if (!string.IsNullOrEmpty(floorId))
        {
            query = query.Where(r => r.FloorId == floorId);
        }

        var list = await query
            .OrderBy(r => r.Name)
            .ToListAsync();

        return list.Select(r => new RoomDto
        {
            Id = r.Id,
            BuildingId = r.BuildingId,
            FloorId = r.FloorId,
            FloorName = r.Floor?.Name,
            Name = r.Name,
            RoomType = r.RoomType,
            Status = r.Status,
            Remark = r.Remark
        }).ToList();
    }

    /// <summary>
    /// 단일 호실 상세 조회
    /// </summary>
    public async Task<RoomDto?> GetRoomByIdAsync(string id)
    {
        _logger.LogInformation("Retrieving room details for Id: {Id}", id);
        
        var r = await _context.Rooms
            .Include(r => r.Floor)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (r == null) return null;

        return new RoomDto
        {
            Id = r.Id,
            BuildingId = r.BuildingId,
            FloorId = r.FloorId,
            FloorName = r.Floor?.Name,
            Name = r.Name,
            RoomType = r.RoomType,
            Status = r.Status,
            Remark = r.Remark
        };
    }

    /// <summary>
    /// 호실 생성
    /// </summary>
    public async Task<RoomDto> CreateRoomAsync(RoomCreateDto dto)
    {
        _logger.LogInformation("Creating new room. Name: {Name}, FloorId: {FloorId}", dto.Name, dto.FloorId);
        
        var r = new Room
        {
            BuildingId = dto.BuildingId,
            FloorId = dto.FloorId,
            Name = dto.Name,
            RoomType = dto.RoomType,
            Status = dto.Status,
            Remark = dto.Remark
        };

        _context.Rooms.Add(r);
        await _context.SaveChangesAsync();

        var result = await GetRoomByIdAsync(r.Id);
        return result ?? new RoomDto
        {
            Id = r.Id,
            BuildingId = r.BuildingId,
            FloorId = r.FloorId,
            Name = r.Name,
            RoomType = r.RoomType,
            Status = r.Status,
            Remark = r.Remark
        };
    }

    /// <summary>
    /// 호실 수정
    /// </summary>
    public async Task<RoomDto?> UpdateRoomAsync(string id, RoomUpdateDto dto)
    {
        _logger.LogInformation("Updating room. Id: {Id}, Name: {Name}", id, dto.Name);
        
        var r = await _context.Rooms
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (r == null) return null;

        r.Name = dto.Name;
        r.RoomType = dto.RoomType;
        r.Status = dto.Status;
        r.Remark = dto.Remark;
        r.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetRoomByIdAsync(r.Id);
    }

    /// <summary>
    /// 호실 삭제
    /// </summary>
    public async Task<bool> DeleteRoomAsync(string id)
    {
        _logger.LogInformation("Deleting room. Id: {Id}", id);
        
        var r = await _context.Rooms
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (r == null) return false;

        r.IsDeleted = true;
        r.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
