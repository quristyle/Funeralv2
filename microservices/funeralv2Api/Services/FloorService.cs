using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;

namespace funeralv2Api.Services;

/// <summary>
/// 층 관리 서비스 구현체
/// </summary>
public class FloorService : IFloorService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FloorService> _logger;

    public FloorService(AppDbContext context, ILogger<FloorService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 층 목록 조회 (건물 필터 적용)
    /// </summary>
    public async Task<List<FloorDto>> GetFloorsAsync(string? buildingId)
    {
        _logger.LogInformation("Retrieving floors list for BuildingId: {BuildingId}", buildingId);
        
        var query = _context.Floors
            .Include(f => f.Building)
            .Where(f => !f.IsDeleted);

        if (!string.IsNullOrEmpty(buildingId))
        {
            query = query.Where(f => f.BuildingId == buildingId);
        }

        var list = await query
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();

        return list.Select(f => new FloorDto
        {
            Id = f.Id,
            BuildingId = f.BuildingId,
            BuildingName = f.Building?.Name,
            Name = f.Name,

            SortOrder = f.SortOrder,
            Remark = f.Remark
        }).ToList();
    }

    /// <summary>
    /// 단일 층 상세 조회
    /// </summary>
    public async Task<FloorDto?> GetFloorByIdAsync(string id)
    {
        _logger.LogInformation("Retrieving floor details for Id: {Id}", id);
        
        var f = await _context.Floors
            .Include(f => f.Building)
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);

        if (f == null) return null;

        return new FloorDto
        {
            Id = f.Id,
            BuildingId = f.BuildingId,
            BuildingName = f.Building?.Name,
            Name = f.Name,

            SortOrder = f.SortOrder,
            Remark = f.Remark
        };
    }

    /// <summary>
    /// 층 생성
    /// </summary>
    public async Task<FloorDto> CreateFloorAsync(FloorCreateDto dto)
    {
        _logger.LogInformation("Creating new floor. Name: {Name}, BuildingId: {BuildingId}", dto.Name, dto.BuildingId);
        
        var f = new Floor
        {
            BuildingId = dto.BuildingId,
            Name = dto.Name,

            SortOrder = dto.SortOrder,
            Remark = dto.Remark
        };

        _context.Floors.Add(f);
        await _context.SaveChangesAsync();

        // 조인을 포함해 빌딩명을 채우기 위해 다시 조회
        var result = await GetFloorByIdAsync(f.Id);
        return result ?? new FloorDto
        {
            Id = f.Id,
            BuildingId = f.BuildingId,
            Name = f.Name,

            SortOrder = f.SortOrder,
            Remark = f.Remark
        };
    }

    /// <summary>
    /// 층 수정
    /// </summary>
    public async Task<FloorDto?> UpdateFloorAsync(string id, FloorUpdateDto dto)
    {
        _logger.LogInformation("Updating floor. Id: {Id}, Name: {Name}", id, dto.Name);
        
        var f = await _context.Floors
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);

        if (f == null) return null;

        f.Name = dto.Name;

        f.SortOrder = dto.SortOrder;
        f.Remark = dto.Remark;
        f.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetFloorByIdAsync(f.Id);
    }

    /// <summary>
    /// 층 삭제
    /// </summary>
    public async Task<bool> DeleteFloorAsync(string id)
    {
        _logger.LogInformation("Deleting floor. Id: {Id}", id);
        
        var f = await _context.Floors
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);

        if (f == null) return false;

        f.IsDeleted = true;
        f.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
