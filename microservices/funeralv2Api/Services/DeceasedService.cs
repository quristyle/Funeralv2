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
    private readonly IDeviceHubSender _deviceHubSender;

    public DeceasedService(AppDbContext context, ILogger<DeceasedService> logger, IDeviceHubSender deviceHubSender)
    {
        _context = context;
        _logger = logger;
        _deviceHubSender = deviceHubSender;
    }

    /// <inheritdoc />
    public async Task<List<DeceasedDto>> GetDeceasedListAsync(DeceasedSearchDto searchDto)
    {
        _logger.LogInformation("Fetching deceased list with filters");

        // 1. 기본 고인 쿼리 시작 (삭제되지 않은 고인)
        var query = _context.Deceaseds.AsNoTracking().Where(x => !x.IsDeleted);

        // 2. 회사, 건물, 층, 호실 필터 적용
        // DeceasedRoom 배정 이력 테이블과 조인하여 조건에 맞는 이력이 하나라도 존재하는 고인만 골라냄
        if (!string.IsNullOrEmpty(searchDto.CompanyId) || 
            !string.IsNullOrEmpty(searchDto.BuildingId) || 
            !string.IsNullOrEmpty(searchDto.FloorId) || 
            !string.IsNullOrEmpty(searchDto.RoomId))
        {
            var matchedDeceasedIdsQuery = from dr in _context.DeceasedRooms.Where(x => !x.IsDeleted)
                                         join r in _context.Rooms.Where(x => !x.IsDeleted) on dr.RoomId equals r.Id
                                         join b in _context.Buildings.Where(x => !x.IsDeleted) on r.BuildingId equals b.Id
                                         select new { dr.DeceasedId, dr.RoomId, r.BuildingId, r.FloorId, b.CompanyId };

            if (!string.IsNullOrEmpty(searchDto.CompanyId))
                matchedDeceasedIdsQuery = matchedDeceasedIdsQuery.Where(x => x.CompanyId == searchDto.CompanyId);
            if (!string.IsNullOrEmpty(searchDto.BuildingId))
                matchedDeceasedIdsQuery = matchedDeceasedIdsQuery.Where(x => x.BuildingId == searchDto.BuildingId);
            if (!string.IsNullOrEmpty(searchDto.FloorId))
                matchedDeceasedIdsQuery = matchedDeceasedIdsQuery.Where(x => x.FloorId == searchDto.FloorId);
            if (!string.IsNullOrEmpty(searchDto.RoomId))
                matchedDeceasedIdsQuery = matchedDeceasedIdsQuery.Where(x => x.RoomId == searchDto.RoomId);

            var matchedIds = await matchedDeceasedIdsQuery.Select(x => x.DeceasedId).Distinct().ToListAsync();
            query = query.Where(x => matchedIds.Contains(x.Id));
        }

        // 3. 인적 정보 필터링 적용
        if (!string.IsNullOrEmpty(searchDto.Name))
            query = query.Where(x => x.Name.Contains(searchDto.Name));
        if (!string.IsNullOrEmpty(searchDto.Gender))
            query = query.Where(x => x.Gender == searchDto.Gender);
        if (searchDto.MinAge.HasValue)
            query = query.Where(x => x.Age >= searchDto.MinAge.Value);
        if (searchDto.MaxAge.HasValue)
            query = query.Where(x => x.Age <= searchDto.MaxAge.Value);
        if (!string.IsNullOrEmpty(searchDto.Religion))
            query = query.Where(x => x.Religion == searchDto.Religion);

        // 4. 기간 검색 필터 적용
        if (searchDto.RoomEnterStartDate.HasValue)
            query = query.Where(x => x.FuneralDate >= SpecifyUtc(searchDto.RoomEnterStartDate.Value));
        if (searchDto.RoomEnterEndDate.HasValue)
            query = query.Where(x => x.FuneralDate <= SpecifyUtc(searchDto.RoomEnterEndDate.Value));
        if (searchDto.FuneralStartDate.HasValue)
            query = query.Where(x => x.BurialDate >= SpecifyUtc(searchDto.FuneralStartDate.Value));
        if (searchDto.FuneralEndDate.HasValue)
            query = query.Where(x => x.BurialDate <= SpecifyUtc(searchDto.FuneralEndDate.Value));

        // 5. 장례 상태 필터 적용
        if (!string.IsNullOrEmpty(searchDto.Status))
            query = query.Where(x => x.Status == searchDto.Status);

        // 6. 고인 목록 순서 정렬 및 1차 로드
        var deceasedList = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        if (!deceasedList.Any())
        {
            return new List<DeceasedDto>();
        }

        // 7. 배정된 호실 이력을 메모리 조인을 통해 텍스트로 합성하기 위해 모든 배정 이력, 호실, 건물, 층 정보를 일괄 로드
        var deceasedIds = deceasedList.Select(x => x.Id).ToList();
        
        var deceasedRooms = await (from dr in _context.DeceasedRooms.Where(x => !x.IsDeleted && deceasedIds.Contains(x.DeceasedId))
                                   join r in _context.Rooms.Where(x => !x.IsDeleted) on dr.RoomId equals r.Id
                                   join b in _context.Buildings.Where(x => !x.IsDeleted) on r.BuildingId equals b.Id
                                   join f in _context.Floors.Where(x => !x.IsDeleted) on r.FloorId equals f.Id into floors
                                   from floor in floors.DefaultIfEmpty()
                                   select new
                                   {
                                       dr.DeceasedId,
                                       RoomId = r.Id,
                                       BuildingName = b.Name,
                                       FloorName = floor != null ? floor.Name : "",
                                       RoomName = r.Name,
                                       RoomShortName = r.ShortName
                                   }).ToListAsync();

        // 7-2. 대표상주 목록 일괄 로드 (N+1 성능 저하 방지)
        var chiefMourners = await _context.DeceasedMourners
            .Where(x => !x.IsDeleted && x.IsChief && deceasedIds.Contains(x.DeceasedId))
            .Select(x => new { x.DeceasedId, x.Name })
            .ToListAsync();

        // 8. DTO 매핑 및 배정된 건물, 층, 호실 정보 문자열 결합 (예: "본관 2층 201호, 신관 3층 302호")
        var dtoList = deceasedList.Select(d =>
        {
            var roomsInfo = deceasedRooms.Where(r => r.DeceasedId == d.Id).ToList();
            var roomNamesCombined = string.Join(",", roomsInfo.Select(r => !string.IsNullOrEmpty(r.RoomShortName) ? r.RoomShortName : r.RoomName).Where(name => !string.IsNullOrEmpty(name)));

            var chief = chiefMourners.FirstOrDefault(c => c.DeceasedId == d.Id);

            return new DeceasedDto
            {
                Id = d.Id,
                Name = d.Name,
                Gender = d.Gender,
                Age = d.Age,
                Religion = d.Religion,
                DeathDate = d.DeathDate,
                FuneralDate = d.FuneralDate,
                BurialDate = d.BurialDate,
                RoomId = roomsInfo.FirstOrDefault()?.RoomId ?? d.RoomId,
                RoomName = string.IsNullOrEmpty(roomNamesCombined) ? null : roomNamesCombined,
                Status = d.Status,
                MemorialPhotoUrl = d.MemorialPhotoUrl,
                MemorialPhotoFileId = d.MemorialPhotoFileId,
                MemorialEditedPhotoUrl = d.MemorialEditedPhotoUrl,
                MemorialEditedPhotoFileId = d.MemorialEditedPhotoFileId,
                FamilyPhotoGroupId = d.FamilyPhotoGroupId,
                ChiefMourner = chief?.Name
            };
        }).ToList();

        return dtoList;
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
            DeathDate = SpecifyUtc(dto.DeathDate),
            FuneralDate = SpecifyUtc(dto.FuneralDate),
            BurialDate = SpecifyUtc(dto.BurialDate),
            RoomId = dto.RoomId,
            Status = dto.Status,
            Remark = dto.Remark,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Deceaseds.Add(deceased);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(deceased.RoomId))
        {
            try { await _deviceHubSender.SendDeviceChangedByRoomIdAsync(deceased.RoomId); } catch (Exception ex) { _logger.LogError(ex, "SignalR 알림 전송 중 에러"); }
        }

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

        var oldRoomId = deceased.RoomId;

        deceased.Name = dto.Name;
        deceased.Gender = dto.Gender;
        deceased.Age = dto.Age;
        deceased.Religion = dto.Religion;
        deceased.DeathDate = SpecifyUtc(dto.DeathDate);
        deceased.FuneralDate = SpecifyUtc(dto.FuneralDate);
        deceased.BurialDate = SpecifyUtc(dto.BurialDate);
        deceased.RoomId = dto.RoomId;
        deceased.Status = dto.Status;
        deceased.Remark = dto.Remark;
        deceased.UpdatedBy = "System";
        deceased.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        try 
        { 
            if (!string.IsNullOrEmpty(oldRoomId)) await _deviceHubSender.SendDeviceChangedByRoomIdAsync(oldRoomId);
            if (!string.IsNullOrEmpty(deceased.RoomId) && deceased.RoomId != oldRoomId) await _deviceHubSender.SendDeviceChangedByRoomIdAsync(deceased.RoomId);
        } 
        catch (Exception ex) { _logger.LogError(ex, "SignalR 알림 전송 중 에러"); }

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
            BurialPlot = deceased.BurialPlot,
            MemorialPhotoUrl = deceased.MemorialPhotoUrl,
            MemorialPhotoFileId = deceased.MemorialPhotoFileId,
            MemorialEditedPhotoUrl = deceased.MemorialEditedPhotoUrl,
            MemorialEditedPhotoFileId = deceased.MemorialEditedPhotoFileId,
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

        detail.ChiefMourner = detail.Mourners.FirstOrDefault(m => m.IsChief)?.Name;

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
        deceased.DeathDate = SpecifyUtc(dto.DeathDate);
        deceased.FuneralDate = SpecifyUtc(dto.FuneralDate);
        deceased.BurialDate = SpecifyUtc(dto.BurialDate);
        deceased.RoomId = dto.RoomId;
        deceased.Status = dto.Status;
        deceased.Remark = dto.Remark;
        deceased.Ssn = dto.Ssn;
        deceased.CauseOfDeath = dto.CauseOfDeath;
        deceased.MemorialPhotoUrl = dto.MemorialPhotoUrl;
        deceased.MemorialPhotoFileId = dto.MemorialPhotoFileId;
        deceased.MemorialEditedPhotoUrl = dto.MemorialEditedPhotoUrl;
        deceased.MemorialEditedPhotoFileId = dto.MemorialEditedPhotoFileId;
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
                    StartTime = SpecifyUtc(fDto.StartTime),
                    EndTime = SpecifyUtc(fDto.EndTime),
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
                    existing.StartTime = SpecifyUtc(fDto.StartTime);
                    existing.EndTime = SpecifyUtc(fDto.EndTime);
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
                    StartTime = SpecifyUtc(rDto.StartTime),
                    EndTime = SpecifyUtc(rDto.EndTime),
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
                    existing.StartTime = SpecifyUtc(rDto.StartTime);
                    existing.EndTime = SpecifyUtc(rDto.EndTime);
                    existing.IsDeleted = false;
                }
            }
        }

        await _context.SaveChangesAsync();

        try
        {
            var roomIdsToNotify = existingRooms.Select(x => x.RoomId).Distinct().ToList();
            if (!string.IsNullOrEmpty(deceased.RoomId)) roomIdsToNotify.Add(deceased.RoomId);
            
            foreach (var rId in roomIdsToNotify.Distinct())
            {
                await _deviceHubSender.SendDeviceChangedByRoomIdAsync(rId);
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "SignalR 알림 전송 중 에러"); }

        return await GetDeceasedDetailAsync(id);
    }

    /// <inheritdoc />
    public async Task<DeceasedDetailDto?> GetDeceasedDetailByRoomIdAsync(string roomId)
    {
        _logger.LogInformation("Fetching current deceased detail info by Room ID: {RoomId}", roomId);

        if (string.IsNullOrEmpty(roomId))
        {
            return null;
        }

        // DeceasedRooms 이력 테이블을 기준으로 현재 호실에 배정된 고인을 찾습니다.
        var deceasedId = await (from dr in _context.DeceasedRooms
                                join d in _context.Deceaseds on dr.DeceasedId equals d.Id
                                where dr.RoomId == roomId && !dr.IsDeleted && !d.IsDeleted && d.Status != "COMPLETED"
                                orderby dr.StartTime descending
                                select d.Id).FirstOrDefaultAsync();

        var deceased = deceasedId == null ? null : await _context.Deceaseds.AsNoTracking()
            .Where(d => d.Id == deceasedId)
            .FirstOrDefaultAsync();
        
        if (deceased == null) return null;

        // 기존 GetDeceasedDetailAsync를 재사용하면 불필요한 쿼리가 많으므로,
        // 이 엔드포인트에 최적화된 DTO를 직접 구성합니다.
        var detailDto = await GetDeceasedDetailAsync(deceased.Id);

        if (detailDto == null) return null;

        // 상주 목록을 명시적으로 조회하여 채워줍니다.
        detailDto.Mourners = await _context.DeceasedMourners
            .AsNoTracking()
            .Where(m => m.DeceasedId == deceased.Id && !m.IsDeleted)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .Select(m => new DeceasedMournerDto
            {
                // 필요한 필드만 매핑
                Name = m.Name,
                Relation = m.Relation,
                IsChief = m.IsChief,
            }).ToListAsync();

        return detailDto;
    }

    private static DateTime SpecifyUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt;
    }

    private static DateTime? SpecifyUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        return dt.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : dt;
    }
}
