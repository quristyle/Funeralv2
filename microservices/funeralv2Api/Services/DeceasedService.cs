using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public DeceasedService(
        AppDbContext context, 
        ILogger<DeceasedService> logger, 
        IDeviceHubSender deviceHubSender,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _deviceHubSender = deviceHubSender;
        _configuration = configuration;
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
                RoomId = roomsInfo.FirstOrDefault()?.RoomId,
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
            // RoomId = dto.RoomId, // 직접 할당 대신 DeceasedRooms 테이블 사용
            Status = dto.Status,
            Remark = dto.Remark,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Deceaseds.Add(deceased);

        if (!string.IsNullOrEmpty(dto.RoomId))
        {
            var newRoomAssignment = new DeceasedRoom
            {
                Id = Guid.NewGuid().ToString(),
                DeceasedId = id,
                RoomId = dto.RoomId,
                StartTime = DateTime.UtcNow,
                IsDeleted = false
            };
            _context.DeceasedRooms.Add(newRoomAssignment);
        }

        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(dto.RoomId))
        {
            try { await _deviceHubSender.SendDeviceChangedByRoomIdAsync(dto.RoomId); } catch (Exception ex) { _logger.LogError(ex, "SignalR 알림 전송 중 에러"); }
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
            RoomId = dto.RoomId,
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

        var currentRoomAssignment = await _context.DeceasedRooms
            .Where(dr => dr.DeceasedId == id && !dr.IsDeleted)
            .OrderByDescending(dr => dr.StartTime)
            .FirstOrDefaultAsync();

        var oldRoomId = currentRoomAssignment?.RoomId;

        // 호실 변경 감지 및 처리
        if (oldRoomId != dto.RoomId)
        {
            if (currentRoomAssignment != null)
            {
                currentRoomAssignment.EndTime = DateTime.UtcNow;
                currentRoomAssignment.IsDeleted = true; // 이전 배정은 비활성화
            }

            if (!string.IsNullOrEmpty(dto.RoomId))
            {
                var newRoomAssignment = new DeceasedRoom
                {
                    Id = Guid.NewGuid().ToString(),
                    DeceasedId = id,
                    RoomId = dto.RoomId,
                    StartTime = DateTime.UtcNow,
                    IsDeleted = false
                };
                _context.DeceasedRooms.Add(newRoomAssignment);
            }
        }

        deceased.Name = dto.Name;
        deceased.Gender = dto.Gender;
        deceased.Age = dto.Age;
        deceased.Religion = dto.Religion;
        deceased.DeathDate = SpecifyUtc(dto.DeathDate);
        deceased.FuneralDate = SpecifyUtc(dto.FuneralDate);
        deceased.BurialDate = SpecifyUtc(dto.BurialDate);
        // deceased.RoomId = dto.RoomId; // 직접 업데이트 대신 DeceasedRooms 사용
        deceased.Status = dto.Status;
        deceased.Remark = dto.Remark;
        deceased.UpdatedBy = "System";
        deceased.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        try 
        { 
            if (!string.IsNullOrEmpty(oldRoomId)) await _deviceHubSender.SendDeviceChangedByRoomIdAsync(oldRoomId);
            if (!string.IsNullOrEmpty(dto.RoomId) && dto.RoomId != oldRoomId) await _deviceHubSender.SendDeviceChangedByRoomIdAsync(dto.RoomId);
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
            RoomId = dto.RoomId,
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
    public async Task<DeceasedDetailDto?> GetDeceasedDetailAsync(string deceased_id)
    {
        _logger.LogInformation("Fetching deceased detail info: {Id}", deceased_id);

        var deceased = await _context.Deceaseds.FirstOrDefaultAsync(x => x.Id == deceased_id && !x.IsDeleted);

        



        if (deceased == null) return null;

        var currentRoomAssignment = await _context.DeceasedRooms
            .Where(dr => dr.DeceasedId == deceased_id && !dr.IsDeleted)
            .OrderByDescending(dr => dr.StartTime)
            .FirstOrDefaultAsync();

        var roomId = currentRoomAssignment?.RoomId;
        var room = roomId != null ? await _context.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted) : null;

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
            RoomId = roomId, // 올바른 RoomId 사용
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

        detail.FamilyPhotos = await GetFileUrlsFromGroupAsync(deceased.FamilyPhotoGroupId);

        // ... (상주, 계약자, 담당자, 시설이용, 호실이력 등 나머지 조회 로직은 동일)

        // 1. 상주 목록 조회
        var mourners = await _context.DeceasedMourners
            .AsNoTracking()
            .Where(m => m.DeceasedId == deceased_id && !m.IsDeleted)
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

        var famTypes = await GetFamTypeRelationNamesAsync();
        foreach (var m in mourners)
        {
            if (famTypes.TryGetValue(m.Relation, out var codeName))
            {
                m.RelationName = codeName;
            }
            else
            {
                m.RelationName = m.Relation;
            }
        }

        detail.Mourners = mourners;

        detail.ChiefMourner = detail.Mourners.FirstOrDefault(m => m.IsChief)?.Name;

        // 2. 계약자 조회
        var contractor = await _context.DeceasedContractors.FirstOrDefaultAsync(c => c.DeceasedId == deceased_id);
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
        var manager = await _context.DeceasedManagers.FirstOrDefaultAsync(m => m.DeceasedId == deceased_id);
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
            .Where(f => f.DeceasedId == deceased_id)
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
        detail.Rooms = await (from dr in _context.DeceasedRooms.Where(dr => dr.DeceasedId == deceased_id && !dr.IsDeleted)
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
            
            foreach (var rId in roomIdsToNotify.Distinct())
            {
                await _deviceHubSender.SendDeviceChangedByRoomIdAsync(rId);
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "SignalR 알림 전송 중 에러"); }

        return await GetDeceasedDetailAsync(id);
    }

    /// <inheritdoc />
    public async Task<DeceasedDetailDto?> GetDeceasedDetailByDeviceCodeAsync(string deviceCode)
    {
        _logger.LogInformation("GetDeceasedDetailByDeviceCodeAsync ID: {DeviceCode}", deviceCode);

        if (string.IsNullOrEmpty(deviceCode))
        {
            return null;
        }

// 1. 장비 코드로 장비 조회
        var device = await _context.Devices
            .Where(d => d.Code == deviceCode )
            .FirstOrDefaultAsync();


        _logger.LogInformation("1111111111111111device: {device}", device);
        if (device == null)
        {
        _logger.LogInformation(" 2222222222 device: {device}", device);
            return null;
        }



        // DeceasedRooms 이력 테이블을 기준으로 현재 호실에 배정된 고인을 찾습니다.
        var deceasedId = await (from dr in _context.DeceasedRooms
                                join d in _context.Deceaseds on dr.DeceasedId equals d.Id
                                where dr.RoomId == device.RoomId && !dr.IsDeleted && !d.IsDeleted 
                                orderby dr.StartTime descending
                                select d.Id).FirstOrDefaultAsync();

                                
        _logger.LogInformation("33333333333333: {deceasedId}", deceasedId);




// 2. 고인 상세 정보 조회
        var deceased = deceasedId == null ? null : await _context.Deceaseds.AsNoTracking()
            .Where(d => d.Id == deceasedId)
            .FirstOrDefaultAsync();

                               
        _logger.LogInformation("444444444444: {deceased}", deceased);





        
        if (deceased == null) return null;

        // 기존 GetDeceasedDetailAsync를 재사용하면 불필요한 쿼리가 많으므로,
        // 이 엔드포인트에 최적화된 DTO를 직접 구성합니다.
        var detailDto = await GetDeceasedDetailAsync(deceasedId);

        
        _logger.LogInformation("55555555555: {detailDto}", detailDto);




        if (detailDto == null) return null;

        // 상주 목록을 명시적으로 조회하여 채워줍니다.
        var mourners = await _context.DeceasedMourners
            .AsNoTracking()
            .Where(m => m.DeceasedId == deceased.Id && !m.IsDeleted)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .Select(m => new DeceasedMournerDto
            {
                Name = m.Name,
                Relation = m.Relation,
                IsChief = m.IsChief,
            }).ToListAsync();

        var famTypes = await GetFamTypeRelationNamesAsync();
        foreach (var m in mourners)
        {
            if (famTypes.TryGetValue(m.Relation, out var codeName))
            {
                m.RelationName = codeName;
            }
            else
            {
                m.RelationName = m.Relation;
            }
        }

        detailDto.Mourners = mourners;




        // 6. 현재 호실의 장비에 설정된 장식 정보 조회
   
      
        _logger.LogInformation("ㅇㅇㅇㅇㅇㅇㅇㅇㅇㅇdevice: {device}", device);




            if (device != null)
            {
        _logger.LogInformation("ㅇㅇㅇㅇㅇㅇㅇㅇㅇㅇdevice: {device.Code}", device.Code);

                detailDto.DeviceRibbons = await (from dr in _context.DeviceRibbons.Where(r => r.DeviceId == device.Id && !r.IsDeleted)
                                              join ms in _context.MediaSources on dr.MediaSourceId equals ms.Id
                                              orderby dr.SortOrder
                                              select new DeviceRibbonDto
                                              {
                                                  Id = dr.Id,
                                                  DeviceId = dr.DeviceId,
                                                  MediaSourceId = dr.MediaSourceId,
                                                  MediaSourceName = ms.Name,
                                                  MediaSourceUrl = ms.Url,
                                                  MediaSourceThumbnailUrl = ms.ThumbnailUrl,
                                                  PositionLeft = dr.PositionLeft,
                                                  PositionTop = dr.PositionTop,
                                                  Width = dr.Width,
                                                  Height = dr.Height,
                                                  SortOrder = dr.SortOrder,
                                                  Remark = dr.Remark
                                              }).ToListAsync();

                // 7. 현재 호실의 장비에 설정된 글자(텍스트 오버레이) 정보 조회
                detailDto.DeviceTextOverlays = await _context.DeviceTextOverlays
                    .Where(t => t.DeviceId == device.Id && !t.IsDeleted)
                    .OrderBy(t => t.SortOrder)
                    .Select(t => new DeviceTextOverlayDto
                    {
                        Id = t.Id,
                        DeviceId = t.DeviceId,
                        TextContent = t.TextContent,
                        FontSize = t.FontSize,
                        FontColor = t.FontColor,
                        BackgroundColor = t.BackgroundColor,
                        TextAlign = t.TextAlign,
                        FontWeight = t.FontWeight,
                        PositionLeft = t.PositionLeft,
                        PositionTop = t.PositionTop,
                        Width = t.Width,
                        Height = t.Height,
                        SortOrder = t.SortOrder,
                        Remark = t.Remark
                    })
                    .ToListAsync();
            }
        








        return detailDto;
    }

    /// <inheritdoc />
    public async Task<List<EntranceGuideRoomDto>> GetEntranceGuideRoomsByDeviceCodeAsync(string deviceCode)
    {
        _logger.LogInformation("GetEntranceGuideRoomsByDeviceCodeAsync: {DeviceCode}", deviceCode);

        if (string.IsNullOrEmpty(deviceCode))
        {
            return new List<EntranceGuideRoomDto>();
        }

        // 1. 장비 코드로 장비 조회
        var device = await _context.Devices
            .Where(d => d.Code == deviceCode)
            .FirstOrDefaultAsync();

        if (device == null)
        {
            _logger.LogWarning("Device not found for code: {DeviceCode}", deviceCode);
            return new List<EntranceGuideRoomDto>();
        }

        // 2. 장비의 층 ID 또는 건물 ID에 속한 모든 호실 조회
        List<Room> rooms = new List<Room>();
        if (!string.IsNullOrEmpty(device.FloorId))
        {
            _logger.LogInformation("Fetching rooms for FloorId: {FloorId}", device.FloorId);
            rooms = await _context.Rooms
                .Include(r => r.Floor)
                .Where(r => r.FloorId == device.FloorId && r.Status == "ACTIVE" && !r.IsDeleted)
                .OrderBy(r => r.SortOrder)
                .ToListAsync();
        }
        else if (!string.IsNullOrEmpty(device.BuildingId))
        {
            _logger.LogInformation("Fetching rooms for BuildingId: {BuildingId}", device.BuildingId);
            rooms = await _context.Rooms
                .Include(r => r.Floor)
                .Where(r => r.BuildingId == device.BuildingId && r.Status == "ACTIVE" && !r.IsDeleted)
                .OrderBy(r => r.SortOrder)
                .ToListAsync();
        }

        var result = new List<EntranceGuideRoomDto>();

        // 3. 각 호실별로 현재 배정된 고인 정보 조회
        foreach (var room in rooms)
        {
            var deceasedId = await (from dr in _context.DeceasedRooms
                                    join d in _context.Deceaseds on dr.DeceasedId equals d.Id
                                    where dr.RoomId == room.Id && !dr.IsDeleted && !d.IsDeleted && d.Status != "COMPLETED"
                                    orderby dr.StartTime descending
                                    select d.Id).FirstOrDefaultAsync();

            DeceasedDetailDto? detailDto = null;
            if (deceasedId != null)
            {
                detailDto = await GetDeceasedDetailAsync(deceasedId);
                if (detailDto != null)
                {
                    // 상주 목록 채우기 (GetDeceasedDetailByDeviceCodeAsync 와 동일한 스펙)
                    var mourners = await _context.DeceasedMourners
                        .AsNoTracking()
                        .Where(m => m.DeceasedId == deceasedId && !m.IsDeleted)
                        .OrderBy(m => m.SortOrder)
                        .ThenBy(m => m.Name)
                        .Select(m => new DeceasedMournerDto
                        {
                            Name = m.Name,
                            Relation = m.Relation,
                            IsChief = m.IsChief,
                        }).ToListAsync();

                    var famTypes = await GetFamTypeRelationNamesAsync();
                    foreach (var m in mourners)
                    {
                        if (famTypes.TryGetValue(m.Relation, out var codeName))
                        {
                            m.RelationName = codeName;
                        }
                        else
                        {
                            m.RelationName = m.Relation;
                        }
                    }

                    detailDto.Mourners = mourners;
                }
            }

            result.Add(new EntranceGuideRoomDto
            {
                RoomId = room.Id,
                RoomName = room.Name,
                FloorName = room.Floor?.Name ?? string.Empty,
                SortOrder = room.SortOrder,
                DeceasedDetail = detailDto
            });
        }

        return result;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<KioskGuideResponseDto> GetKioskRoomsByDeviceCodeAsync(string deviceCode)
    {
        _logger.LogInformation("GetKioskRoomsByDeviceCodeAsync: {DeviceCode}", deviceCode);
        var responseDto = new KioskGuideResponseDto();

        if (string.IsNullOrEmpty(deviceCode))
        {
            return responseDto;
        }

        // 1. 장비 코드로 장비 조회
        var device = await _context.Devices
            .Where(d => d.Code == deviceCode)
            .FirstOrDefaultAsync();

        if (device == null)
        {
            _logger.LogWarning("Device not found for code: {DeviceCode}", deviceCode);
            return responseDto;
        }

        // 2. 장비가 속한 건물 ID 파악
        var buildingId = device.BuildingId;
        if (string.IsNullOrEmpty(buildingId) && !string.IsNullOrEmpty(device.FloorId))
        {
            // FloorId가 있으면 Floor 엔티티를 조회하여 BuildingId를 구함
            var floor = await _context.Floors.FindAsync(device.FloorId);
            if (floor != null)
            {
                buildingId = floor.BuildingId;
            }
        }

        if (string.IsNullOrEmpty(buildingId))
        {
            _logger.LogWarning("BuildingId not found for device: {DeviceCode}", deviceCode);
            return responseDto;
        }

        // 3. 건물의 전경 이미지 및 주차장 이미지 URL 목록 가져오기
        var building = await _context.Buildings
            .Where(b => b.Id == buildingId && !b.IsDeleted)
            .FirstOrDefaultAsync();
        if (building != null)
        {
            responseDto.BuildingPhotos = await GetFileUrlsFromGroupAsync(building.BuildingPhotoGroupId);
            responseDto.ParkingPhotos = await GetFileUrlsFromGroupAsync(building.ParkingPhotoGroupId);
        }

        // 4. 해당 건물의 모든 호실 조회 (층 필터링 없음, 오직 건물 기준)
        _logger.LogInformation("Fetching rooms for BuildingId: {BuildingId}", buildingId);
        var rooms = await _context.Rooms
            .Include(r => r.Floor)
            .Where(r => r.BuildingId == buildingId && r.Status == "ACTIVE" && !r.IsDeleted)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        var roomList = new List<EntranceGuideRoomDto>();

        // 5. 각 호실별로 현재 배정된 고인 정보 조회 및 DTO 매핑
        foreach (var room in rooms)
        {
            var deceasedId = await (from dr in _context.DeceasedRooms
                                    join d in _context.Deceaseds on dr.DeceasedId equals d.Id
                                    where dr.RoomId == room.Id && !dr.IsDeleted && !d.IsDeleted && d.Status != "COMPLETED"
                                    orderby dr.StartTime descending
                                    select d.Id).FirstOrDefaultAsync();

            DeceasedDetailDto? detailDto = null;
            if (deceasedId != null)
            {
                detailDto = await GetDeceasedDetailAsync(deceasedId);
                if (detailDto != null)
                {
                    // 상주 목록 채우기 (GetDeceasedDetailByDeviceCodeAsync 와 동일한 스펙)
                    var mourners = await _context.DeceasedMourners
                        .AsNoTracking()
                        .Where(m => m.DeceasedId == deceasedId && !m.IsDeleted)
                        .OrderBy(m => m.SortOrder)
                        .ThenBy(m => m.Name)
                        .Select(m => new DeceasedMournerDto
                        {
                            Name = m.Name,
                            Relation = m.Relation,
                            IsChief = m.IsChief,
                        }).ToListAsync();

                    var famTypes = await GetFamTypeRelationNamesAsync();
                    foreach (var m in mourners)
                    {
                        if (famTypes.TryGetValue(m.Relation, out var codeName))
                        {
                            m.RelationName = codeName;
                        }
                        else
                        {
                            m.RelationName = m.Relation;
                        }
                    }

                    detailDto.Mourners = mourners;
                }
            }

            roomList.Add(new EntranceGuideRoomDto
            {
                RoomId = room.Id,
                RoomName = room.Name,
                FloorName = room.Floor?.Name ?? string.Empty,
                SortOrder = room.SortOrder,
                DeceasedDetail = detailDto
            });
        }

        responseDto.Rooms = roomList;
        return responseDto;
    }

    private async Task<List<string>> GetFileUrlsFromGroupAsync(string? groupId)
    {
        var urls = new List<string>();
        if (string.IsNullOrEmpty(groupId) || !Guid.TryParse(groupId, out _))
        {
            return urls;
        }

        var fileServerUrl = _configuration["Services:FileServer"] ?? "http://localhost:5350";
        var requestUrl = $"{fileServerUrl.TrimEnd('/')}/group/{groupId}";

        using var client = new HttpClient();
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
                            var downloadUrl = item?["downloadUrl"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                urls.Add(downloadUrl);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch file urls for groupId: {GroupId}", groupId);
        }

        return urls;
    }

    private async Task<Dictionary<string, string>> GetFamTypeRelationNamesAsync()
    {
        var relationNames = new Dictionary<string, string>();
        var authServerUrl = _configuration["Services:AuthServer"] ?? "http://localhost:5264";
        var requestUrl = $"{authServerUrl.TrimEnd('/')}/system/common-code/FAM_TYPE?hierarchical=false";

        using var client = new HttpClient();
        try
        {
            _logger.LogInformation("Requesting FAM_TYPE codes from AuthServer: {RequestUrl}", requestUrl);
            var response = await client.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(responseContent);
                if (jsonNode != null && jsonNode["success"]?.GetValue<bool>() == true)
                {
                    var dataArray = jsonNode["data"]?["result"]?.AsArray();
                    if (dataArray != null)
                    {
                        foreach (var item in dataArray)
                        {
                            var codeValue = item?["codeValue"]?.GetValue<string>() ?? item?["CodeValue"]?.GetValue<string>();
                            var codeName = item?["codeName"]?.GetValue<string>() ?? item?["CodeName"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(codeValue) && !string.IsNullOrEmpty(codeName))
                            {
                                relationNames[codeValue] = codeName;
                            }
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to fetch FAM_TYPE codes. Status: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching FAM_TYPE codes from AuthServer");
        }

        return relationNames;
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
