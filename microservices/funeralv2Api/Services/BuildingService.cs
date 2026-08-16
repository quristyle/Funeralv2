using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    private readonly IHttpClientFactory _httpClientFactory;

    public BuildingService(AppDbContext context, ILogger<BuildingService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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

        var dtoList = new List<BuildingDto>();
        foreach (var b in list)
        {
            var dto = new BuildingDto
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
                BuildingPhotoGroupId = b.BuildingPhotoGroupId,
                ParkingPhotoGroupId = b.ParkingPhotoGroupId,
                CreatedAt = b.CreatedAt
            };

            dto.BuildingPhotos = await GetThumbnailUrlsFromGroupAsync(b.BuildingPhotoGroupId);
            dto.ParkingPhotos = await GetThumbnailUrlsFromGroupAsync(b.ParkingPhotoGroupId);

            dtoList.Add(dto);
        }

        return dtoList;
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

        var dto = new BuildingDto
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
            BuildingPhotoGroupId = b.BuildingPhotoGroupId,
            ParkingPhotoGroupId = b.ParkingPhotoGroupId,
            CreatedAt = b.CreatedAt
        };

        dto.BuildingPhotos = await GetThumbnailUrlsFromGroupAsync(b.BuildingPhotoGroupId);
        dto.ParkingPhotos = await GetThumbnailUrlsFromGroupAsync(b.ParkingPhotoGroupId);

        return dto;
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
            Remark = dto.Remark,
            BuildingPhotoGroupId = dto.BuildingPhotoGroupId,
            ParkingPhotoGroupId = dto.ParkingPhotoGroupId
        };

        _context.Buildings.Add(b);
        await _context.SaveChangesAsync();

        var retDto = new BuildingDto
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
            BuildingPhotoGroupId = b.BuildingPhotoGroupId,
            ParkingPhotoGroupId = b.ParkingPhotoGroupId,
            CreatedAt = b.CreatedAt
        };

        retDto.BuildingPhotos = await GetThumbnailUrlsFromGroupAsync(b.BuildingPhotoGroupId);
        retDto.ParkingPhotos = await GetThumbnailUrlsFromGroupAsync(b.ParkingPhotoGroupId);

        return retDto;
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
        b.BuildingPhotoGroupId = dto.BuildingPhotoGroupId;
        b.ParkingPhotoGroupId = dto.ParkingPhotoGroupId;
        b.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var retDto = new BuildingDto
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
            BuildingPhotoGroupId = b.BuildingPhotoGroupId,
            ParkingPhotoGroupId = b.ParkingPhotoGroupId,
            CreatedAt = b.CreatedAt
        };

        retDto.BuildingPhotos = await GetThumbnailUrlsFromGroupAsync(b.BuildingPhotoGroupId);
        retDto.ParkingPhotos = await GetThumbnailUrlsFromGroupAsync(b.ParkingPhotoGroupId);

        return retDto;
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

    private async Task<List<string>> GetThumbnailUrlsFromGroupAsync(string? groupId)
    {
        var urls = new List<string>();
        if (string.IsNullOrEmpty(groupId) || !Guid.TryParse(groupId, out _))
        {
            return urls;
        }

        var fileServerUrl = _configuration["Services:FileServer"] ?? "http://localhost:5350";
        var requestUrl = $"{fileServerUrl.TrimEnd('/')}/group/{groupId}";

        var client = _httpClientFactory.CreateClient();
        try
        {
            var response = await client.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(body);
                var success = jsonNode?["success"]?.GetValue<bool>() ?? false;
                if (success)
                {
                    var dataArray = jsonNode?["data"]?["result"]?.AsArray() ?? jsonNode?["data"]?.AsArray();
                    if (dataArray != null)
                    {
                        foreach (var item in dataArray)
                        {
                            var fileId = item?["id"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(fileId))
                            {
                                urls.Add($"/api/file/thumbnail/{fileId}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch thumbnail urls for groupId: {GroupId}", groupId);
        }

        return urls;
    }
}
