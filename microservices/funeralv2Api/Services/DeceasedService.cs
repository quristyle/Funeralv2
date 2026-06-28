using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;

namespace funeralv2Api.Services;

/// <summary>
/// 고인 관리 서비스 구현 클래스
/// </summary>
public class DeceasedService : IDeceasedService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeceasedService> _logger;

    public DeceasedService(AppDbContext context, ILogger<DeceasedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<DeceasedDto>> GetDeceasedListAsync()
    {
        _logger.LogInformation("Fetching deceased list");

        var query = from d in _context.Deceaseds.Where(x => !x.IsDeleted)
                    join r in _context.Rooms.Where(x => !x.IsDeleted) on d.RoomId equals r.Id into rooms
                    from room in rooms.DefaultIfEmpty()
                    orderby d.CreatedAt descending
                    select new DeceasedDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Gender = d.Gender,
                        Age = d.Age,
                        Religion = d.Religion,
                        DeathDate = d.DeathDate,
                        FuneralDate = d.FuneralDate,
                        BurialDate = d.BurialDate,
                        RoomId = d.RoomId,
                        RoomName = room != null ? room.Name : null,
                        Status = d.Status,
                        Remark = d.Remark
                    };

        return await query.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<DeceasedDto> CreateDeceasedAsync(DeceasedCreateDto dto)
    {
        _logger.LogInformation("Creating deceased: {Name}", dto.Name);

        var id = Guid.NewGuid().ToString();
        var deceased = new Deceased
        {
            Id = id,
            Name = dto.Name,
            Gender = dto.Gender,
            Age = dto.Age,
            Religion = dto.Religion,
            DeathDate = dto.DeathDate,
            FuneralDate = dto.FuneralDate,
            BurialDate = dto.BurialDate,
            RoomId = dto.RoomId,
            Status = dto.Status,
            Remark = dto.Remark,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Deceaseds.Add(deceased);
        await _context.SaveChangesAsync();

        // 룸 이름 조회를 위한 개별 바인딩
        string? roomName = null;
        if (!string.IsNullOrEmpty(dto.RoomId))
        {
            var r = await _context.Rooms.FirstOrDefaultAsync(x => x.Id == dto.RoomId && !x.IsDeleted);
            roomName = r?.Name;
        }

        return new DeceasedDto
        {
            Id = deceased.Id,
            Name = deceased.Name,
            Gender = deceased.Gender,
            Age = deceased.Age,
            Religion = deceased.Religion,
            DeathDate = deceased.DeathDate,
            FuneralDate = deceased.FuneralDate,
            BurialDate = deceased.BurialDate,
            RoomId = deceased.RoomId,
            RoomName = roomName,
            Status = deceased.Status,
            Remark = deceased.Remark
        };
    }

    /// <inheritdoc />
    public async Task<DeceasedDto?> UpdateDeceasedAsync(string id, DeceasedUpdateDto dto)
    {
        _logger.LogInformation("Updating deceased: {Id}", id);

        var deceased = await _context.Deceaseds.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (deceased == null)
        {
            return null;
        }

        deceased.Name = dto.Name;
        deceased.Gender = dto.Gender;
        deceased.Age = dto.Age;
        deceased.Religion = dto.Religion;
        deceased.DeathDate = dto.DeathDate;
        deceased.FuneralDate = dto.FuneralDate;
        deceased.BurialDate = dto.BurialDate;
        deceased.RoomId = dto.RoomId;
        deceased.Status = dto.Status;
        deceased.Remark = dto.Remark;
        deceased.UpdatedBy = "System";
        deceased.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        string? roomName = null;
        if (!string.IsNullOrEmpty(dto.RoomId))
        {
            var r = await _context.Rooms.FirstOrDefaultAsync(x => x.Id == dto.RoomId && !x.IsDeleted);
            roomName = r?.Name;
        }

        return new DeceasedDto
        {
            Id = deceased.Id,
            Name = deceased.Name,
            Gender = deceased.Gender,
            Age = deceased.Age,
            Religion = deceased.Religion,
            DeathDate = deceased.DeathDate,
            FuneralDate = deceased.FuneralDate,
            BurialDate = deceased.BurialDate,
            RoomId = deceased.RoomId,
            RoomName = roomName,
            Status = deceased.Status,
            Remark = deceased.Remark
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDeceasedAsync(string id)
    {
        _logger.LogInformation("Soft deleting deceased: {Id}", id);

        var deceased = await _context.Deceaseds.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (deceased == null)
        {
            return false;
        }

        deceased.IsDeleted = true;
        deceased.UpdatedBy = "System";
        deceased.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
