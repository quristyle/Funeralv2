using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;

namespace funeralv2Api.Services;

/// <summary>
/// 건물 관리 서비스 구현체
/// </summary>
public class BuildingService : IBuildingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BuildingService> _logger;

    public BuildingService(AppDbContext context, ILogger<BuildingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 건물 목록 조회 (회사 필터 적용)
    /// </summary>
    public async Task<List<BuildingDto>> GetBuildingsAsync(string? companyId)
    {
        _logger.LogInformation("Retrieving buildings list for CompanyId: {CompanyId}", companyId);
        var query = _context.Buildings
            .Where(b => !b.IsDeleted);

        if (!string.IsNullOrEmpty(companyId))
        {
            query = query.Where(b => b.CompanyId == companyId);
        }

        var list = await query
            .OrderBy(b => b.Name)
            .ToListAsync();

        return list.Select(b => new BuildingDto
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            Name = b.Name,
            ShortName = b.ShortName,
            Abbreviation = b.Abbreviation,
            Address = b.Address,
            ZipCode = b.ZipCode,
            AddressDetail = b.AddressDetail,
            Remark = b.Remark,
            CreatedAt = b.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// 단일 건물 상세 조회
    /// </summary>
    public async Task<BuildingDto?> GetBuildingByIdAsync(string id)
    {
        _logger.LogInformation("Retrieving building details for Id: {Id}", id);
        var b = await _context.Buildings
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

        if (b == null) return null;

        return new BuildingDto
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            Name = b.Name,
            ShortName = b.ShortName,
            Abbreviation = b.Abbreviation,
            Address = b.Address,
            ZipCode = b.ZipCode,
            AddressDetail = b.AddressDetail,
            Remark = b.Remark,
            CreatedAt = b.CreatedAt
        };
    }

    /// <summary>
    /// 건물 생성
    /// </summary>
    public async Task<BuildingDto> CreateBuildingAsync(BuildingCreateDto dto)
    {
        _logger.LogInformation("Creating new building. Name: {Name}, ShortName: {ShortName}, CompanyId: {CompanyId}", dto.Name, dto.ShortName, dto.CompanyId);
        var b = new Building
        {
            CompanyId = dto.CompanyId,
            Name = dto.Name,
            ShortName = dto.ShortName,
            Abbreviation = dto.Abbreviation,
            Address = dto.Address,
            ZipCode = dto.ZipCode,
            AddressDetail = dto.AddressDetail,
            Remark = dto.Remark
        };

        _context.Buildings.Add(b);
        await _context.SaveChangesAsync();

        return new BuildingDto
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            Name = b.Name,
            ShortName = b.ShortName,
            Abbreviation = b.Abbreviation,
            Address = b.Address,
            ZipCode = b.ZipCode,
            AddressDetail = b.AddressDetail,
            Remark = b.Remark,
            CreatedAt = b.CreatedAt
        };
    }

    /// <summary>
    /// 건물 수정
    /// </summary>
    public async Task<BuildingDto?> UpdateBuildingAsync(string id, BuildingUpdateDto dto)
    {
        _logger.LogInformation("Updating building. Id: {Id}, Name: {Name}, ShortName: {ShortName}", id, dto.Name, dto.ShortName);
        var b = await _context.Buildings
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

        if (b == null) return null;

        b.Name = dto.Name;
        b.ShortName = dto.ShortName;
        b.Abbreviation = dto.Abbreviation;
        b.Address = dto.Address;
        b.ZipCode = dto.ZipCode;
        b.AddressDetail = dto.AddressDetail;
        b.Remark = dto.Remark;
        b.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new BuildingDto
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            Name = b.Name,
            ShortName = b.ShortName,
            Abbreviation = b.Abbreviation,
            Address = b.Address,
            ZipCode = b.ZipCode,
            AddressDetail = b.AddressDetail,
            Remark = b.Remark,
            CreatedAt = b.CreatedAt
        };
    }

    /// <summary>
    /// 건물 삭제
    /// </summary>
    public async Task<bool> DeleteBuildingAsync(string id)
    {
        _logger.LogInformation("Deleting building. Id: {Id}", id);
        var b = await _context.Buildings
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

        if (b == null) return false;

        b.IsDeleted = true;
        b.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
