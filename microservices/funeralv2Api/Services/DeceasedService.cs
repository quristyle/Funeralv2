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

    /// <inheritdoc />
    public async Task<DeceasedDetailDto?> GetDeceasedDetailAsync(string id)
    {
        _logger.LogInformation("Fetching deceased detail info: {Id}", id);

        var deceased = await _context.Deceaseds.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (deceased == null) return null;

        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == deceased.RoomId && !r.IsDeleted);

        var detail = new DeceasedDetailDto
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
            RoomName = room?.Name,
            Status = deceased.Status,
            Remark = deceased.Remark,
            Ssn = deceased.Ssn,
            CauseOfDeath = deceased.CauseOfDeath,
            BurialPlot = deceased.BurialPlot,
            MemorialPhotoUrl = deceased.MemorialPhotoUrl,
            MemorialPhotoFileId = deceased.MemorialPhotoFileId,
            FamilyPhotoGroupId = deceased.FamilyPhotoGroupId
        };

        // 1. 상주 목록 조회
        detail.Mourners = await _context.DeceasedMourners
            .Where(m => m.DeceasedId == id && !m.IsDeleted)
            .OrderBy(m => m.SortOrder)
            .Select(m => new DeceasedMournerDto
            {
                Id = m.Id,
                Name = m.Name,
                Relation = m.Relation,
                Contact = m.Contact,
                Email = m.Email,
                Address = m.Address,
                IsChief = m.IsChief,
                SortOrder = m.SortOrder
            }).ToListAsync();

        // 2. 계약자 조회
        var contractor = await _context.DeceasedContractors.FirstOrDefaultAsync(c => c.DeceasedId == id);
        if (contractor != null)
        {
            detail.Contractor = new DeceasedContractorDto
            {
                Name = contractor.Name,
                Contact = contractor.Contact,
                Relation = contractor.Relation,
                Address = contractor.Address,
                Remark = contractor.Remark,
                SignatureFileId = contractor.SignatureFileId
            };
        }

        // 3. 담당자 조회
        var manager = await _context.DeceasedManagers.FirstOrDefaultAsync(m => m.DeceasedId == id);
        if (manager != null)
        {
            detail.Manager = new DeceasedManagerDto
            {
                DirectorName = manager.DirectorName,
                DirectorContact = manager.DirectorContact,
                MutualAidCompany = manager.MutualAidCompany,
                StaffName = manager.StaffName,
                StaffContact = manager.StaffContact
            };
        }

        // 4. 시설이용 목록 조회
        detail.Facilities = await _context.DeceasedFacilities
            .Where(f => f.DeceasedId == id)
            .Select(f => new DeceasedFacilityDto
            {
                Id = f.Id,
                FacilityType = f.FacilityType,
                StartTime = f.StartTime,
                EndTime = f.EndTime,
                UseHours = f.UseHours,
                UnitPrice = f.UnitPrice,
                TotalPrice = f.TotalPrice,
                Remark = f.Remark
            }).ToListAsync();

        // 5. 호실지정 이력 조회
        detail.Rooms = await (from dr in _context.DeceasedRooms.Where(dr => dr.DeceasedId == id && !dr.IsDeleted)
                             join r in _context.Rooms on dr.RoomId equals r.Id into rooms
                             from roomInfo in rooms.DefaultIfEmpty()
                             orderby dr.StartTime descending
                             select new DeceasedRoomDto
                             {
                                 Id = dr.Id,
                                 RoomId = dr.RoomId,
                                 RoomName = roomInfo != null ? roomInfo.Name : null,
                                 StartTime = dr.StartTime,
                                 EndTime = dr.EndTime
                             }).ToListAsync();

        return detail;
    }

    /// <inheritdoc />
    public async Task<DeceasedDetailDto?> SaveDeceasedDetailAsync(string id, DeceasedDetailDto dto)
    {
        _logger.LogInformation("Saving integrated deceased detail: {Id}", id);

        var deceased = await _context.Deceaseds.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (deceased == null)
        {
            id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
            deceased = new Deceased
            {
                Id = id,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            _context.Deceaseds.Add(deceased);
        }

        // 1. 고인 기본 정보 갱신
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
        deceased.Ssn = dto.Ssn;
        deceased.CauseOfDeath = dto.CauseOfDeath;
        deceased.BurialPlot = dto.BurialPlot;
        deceased.MemorialPhotoUrl = dto.MemorialPhotoUrl;
        deceased.MemorialPhotoFileId = dto.MemorialPhotoFileId;
        deceased.FamilyPhotoGroupId = dto.FamilyPhotoGroupId;
        deceased.UpdatedBy = "System";
        deceased.UpdatedAt = DateTime.UtcNow;

        // 2. 상주 정보 병합 (Merge)
        var existingMourners = await _context.DeceasedMourners.Where(m => m.DeceasedId == id).ToListAsync();
        // 삭제 처리 (DTO에 존재하지 않는 기존 항목)
        var dtoMournerIds = dto.Mourners.Where(m => !string.IsNullOrEmpty(m.Id)).Select(m => m.Id).ToList();
        foreach (var em in existingMourners.Where(em => !em.IsDeleted && !dtoMournerIds.Contains(em.Id)))
        {
            em.IsDeleted = true;
        }
        // 추가 및 수정 처리
        foreach (var mDto in dto.Mourners)
        {
            if (string.IsNullOrEmpty(mDto.Id))
            {
                var newMourner = new DeceasedMourner
                {
                    Id = Guid.NewGuid().ToString(),
                    DeceasedId = id,
                    Name = mDto.Name,
                    Relation = mDto.Relation,
                    Contact = mDto.Contact,
                    Email = mDto.Email,
                    Address = mDto.Address,
                    IsChief = mDto.IsChief,
                    SortOrder = mDto.SortOrder,
                    IsDeleted = false
                };
                _context.DeceasedMourners.Add(newMourner);
            }
            else
            {
                var existing = existingMourners.FirstOrDefault(x => x.Id == mDto.Id);
                if (existing != null)
                {
                    existing.Name = mDto.Name;
                    existing.Relation = mDto.Relation;
                    existing.Contact = mDto.Contact;
                    existing.Email = mDto.Email;
                    existing.Address = mDto.Address;
                    existing.IsChief = mDto.IsChief;
                    existing.SortOrder = mDto.SortOrder;
                    existing.IsDeleted = false; // 혹시 복구되는 경우 대비
                }
            }
        }

        // 3. 계약자 정보 병합 (1:1)
        var contractor = await _context.DeceasedContractors.FirstOrDefaultAsync(c => c.DeceasedId == id);
        if (dto.Contractor != null)
        {
            if (contractor == null)
            {
                contractor = new DeceasedContractor
                {
                    Id = Guid.NewGuid().ToString(),
                    DeceasedId = id,
                    Name = dto.Contractor.Name,
                    Contact = dto.Contractor.Contact,
                    Relation = dto.Contractor.Relation,
                    Address = dto.Contractor.Address,
                    Remark = dto.Contractor.Remark,
                    SignatureFileId = dto.Contractor.SignatureFileId
                };
                _context.DeceasedContractors.Add(contractor);
            }
            else
            {
                contractor.Name = dto.Contractor.Name;
                contractor.Contact = dto.Contractor.Contact;
                contractor.Relation = dto.Contractor.Relation;
                contractor.Address = dto.Contractor.Address;
                contractor.Remark = dto.Contractor.Remark;
                contractor.SignatureFileId = dto.Contractor.SignatureFileId;
            }
        }
        else if (contractor != null)
        {
            _context.DeceasedContractors.Remove(contractor);
        }

        // 4. 담당자 정보 병합 (1:1)
        var manager = await _context.DeceasedManagers.FirstOrDefaultAsync(m => m.DeceasedId == id);
        if (dto.Manager != null)
        {
            if (manager == null)
            {
                manager = new DeceasedManager
                {
                    Id = Guid.NewGuid().ToString(),
                    DeceasedId = id,
                    DirectorName = dto.Manager.DirectorName,
                    DirectorContact = dto.Manager.DirectorContact,
                    MutualAidCompany = dto.Manager.MutualAidCompany,
                    StaffName = dto.Manager.StaffName,
                    StaffContact = dto.Manager.StaffContact
                };
                _context.DeceasedManagers.Add(manager);
            }
            else
            {
                manager.DirectorName = dto.Manager.DirectorName;
                manager.DirectorContact = dto.Manager.DirectorContact;
                manager.MutualAidCompany = dto.Manager.MutualAidCompany;
                manager.StaffName = dto.Manager.StaffName;
                manager.StaffContact = dto.Manager.StaffContact;
            }
        }
        else if (manager != null)
        {
            _context.DeceasedManagers.Remove(manager);
        }

        // 5. 시설이용 목록 병합 (1:N)
        var existingFacilities = await _context.DeceasedFacilities.Where(f => f.DeceasedId == id).ToListAsync();
        var dtoFacilityIds = dto.Facilities.Where(f => !string.IsNullOrEmpty(f.Id)).Select(f => f.Id).ToList();
        // 삭제 대상 제거
        foreach (var ef in existingFacilities.Where(ef => !dtoFacilityIds.Contains(ef.Id)))
        {
            _context.DeceasedFacilities.Remove(ef);
        }
        // 추가 및 수정
        foreach (var fDto in dto.Facilities)
        {
            if (string.IsNullOrEmpty(fDto.Id))
            {
                var newFac = new DeceasedFacility
                {
                    Id = Guid.NewGuid().ToString(),
                    DeceasedId = id,
                    FacilityType = fDto.FacilityType,
                    StartTime = fDto.StartTime,
                    EndTime = fDto.EndTime,
                    UseHours = fDto.UseHours,
                    UnitPrice = fDto.UnitPrice,
                    TotalPrice = fDto.TotalPrice,
                    Remark = fDto.Remark
                };
                _context.DeceasedFacilities.Add(newFac);
            }
            else
            {
                var existing = existingFacilities.FirstOrDefault(x => x.Id == fDto.Id);
                if (existing != null)
                {
                    existing.FacilityType = fDto.FacilityType;
                    existing.StartTime = fDto.StartTime;
                    existing.EndTime = fDto.EndTime;
                    existing.UseHours = fDto.UseHours;
                    existing.UnitPrice = fDto.UnitPrice;
                    existing.TotalPrice = fDto.TotalPrice;
                    existing.Remark = fDto.Remark;
                }
            }
        }

        // 6. 호실지정 이력 병합 (1:N)
        var existingRooms = await _context.DeceasedRooms.Where(dr => dr.DeceasedId == id).ToListAsync();
        var dtoRoomIds = dto.Rooms.Where(r => !string.IsNullOrEmpty(r.Id)).Select(r => r.Id).ToList();
        // 삭제 처리
        foreach (var er in existingRooms.Where(er => !er.IsDeleted && !dtoRoomIds.Contains(er.Id)))
        {
            er.IsDeleted = true;
        }
        // 추가 및 수정
        foreach (var rDto in dto.Rooms)
        {
            if (string.IsNullOrEmpty(rDto.Id))
            {
                var newRoom = new DeceasedRoom
                {
                    Id = Guid.NewGuid().ToString(),
                    DeceasedId = id,
                    RoomId = rDto.RoomId,
                    StartTime = rDto.StartTime,
                    EndTime = rDto.EndTime,
                    IsDeleted = false
                };
                _context.DeceasedRooms.Add(newRoom);
            }
            else
            {
                var existing = existingRooms.FirstOrDefault(x => x.Id == rDto.Id);
                if (existing != null)
                {
                    existing.RoomId = rDto.RoomId;
                    existing.StartTime = rDto.StartTime;
                    existing.EndTime = rDto.EndTime;
                    existing.IsDeleted = false;
                }
            }
        }

        await _context.SaveChangesAsync();

        return await GetDeceasedDetailAsync(id);
    }
}
