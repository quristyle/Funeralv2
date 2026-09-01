using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <inheritdoc cref="IBuildingMusicService"/>
public class BuildingMusicService : IBuildingMusicService
{
    private readonly AppDbContext _dbContext;

    public BuildingMusicService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<List<BuildingMusicDto>> GetBuildingsForMusicAsync(string mediaSourceId)
    {
        var buildings = await _dbContext.Buildings
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .AsNoTracking()
            .ToListAsync();

        var mapped = await _dbContext.BuildingMusics
            .Where(m => m.MediaSourceId == mediaSourceId && !m.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(m => m.BuildingId, m => m.Id);

        return buildings.Select(b => new BuildingMusicDto
        {
            BuildingId = b.Id,
            BuildingName = b.Name,
            BuildingShortName = b.ShortName,
            Address = b.Address,
            SortOrder = b.SortOrder,
            Mapped = mapped.ContainsKey(b.Id),
            MappingId = mapped.TryGetValue(b.Id, out var id) ? id : null,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<BuildingMusicDto>> SaveAsync(string userId, string mediaSourceId, List<string> buildingIds)
    {
        var wanted = buildingIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToHashSet();

        var existing = await _dbContext.BuildingMusics
            .Where(m => m.MediaSourceId == mediaSourceId && !m.IsDeleted)
            .ToListAsync();

        // 빠진 것은 지운다. 되살릴 수 있게 지움 표시만 한다.
        foreach (var row in existing.Where(r => !wanted.Contains(r.BuildingId)))
        {
            row.IsDeleted = true;
            row.UpdatedBy = userId;
            row.UpdatedAt = DateTime.UtcNow;
        }

        var have = existing.Where(r => !r.IsDeleted).Select(r => r.BuildingId).ToHashSet();

        // 새로 켠 것 중 예전에 지웠던 행이 있으면 그것을 되살린다 — 행이 쌓이지 않게.
        var revivable = await _dbContext.BuildingMusics
            .Where(m => m.MediaSourceId == mediaSourceId && m.IsDeleted && wanted.Contains(m.BuildingId))
            .ToListAsync();

        foreach (var row in revivable)
        {
            row.IsDeleted = false;
            row.UpdatedBy = userId;
            row.UpdatedAt = DateTime.UtcNow;
            have.Add(row.BuildingId);
        }

        var order = 0;
        foreach (var buildingId in wanted.Where(id => !have.Contains(id)))
        {
            _dbContext.BuildingMusics.Add(new BuildingMusic
            {
                BuildingId = buildingId,
                MediaSourceId = mediaSourceId,
                SortOrder = order++,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _dbContext.SaveChangesAsync();
        return await GetBuildingsForMusicAsync(mediaSourceId);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetMusicIdsForBuildingAsync(string buildingId)
    {
        return await _dbContext.BuildingMusics
            .Where(m => m.BuildingId == buildingId && !m.IsDeleted)
            .OrderBy(m => m.SortOrder)
            .Select(m => m.MediaSourceId)
            .AsNoTracking()
            .ToListAsync();
    }
}
