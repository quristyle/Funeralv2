using System.Collections.Generic;
using System.Linq;

namespace funeralv2Api.DTOs;

/// <summary>
/// 익명 표출 장비에게 내려보내는 응답을 표출에 필요한 칸만으로 줄인다. 장비 코드는 추측 가능하므로 민감 칸을 뺀다.
/// 고인은 허용 목록으로(새 칸이 생겨도 새지 않게), 장비는 지울 목록으로(새 표출 설정이 조용히 빠지지 않게) 짠다.
/// </summary>
public static class AnonymousDisplayProjection
{
    /// <summary>
    /// 고인 상세를 표출용으로 줄인 새 객체를 돌려준다. 원본은 고치지 않는다 — 응답을 캐시하게 되면 인증 화면 응답까지 깎이기 때문이다.
    /// </summary>
    public static DeceasedDetailDto? ToAnonymousDisplay(this DeceasedDetailDto? source)
    {
        if (source is null)
        {
            return null;
        }

        return new DeceasedDetailDto
        {
            // 화면에 글자로 나가는 것
            Id = source.Id,
            Name = source.Name,
            Gender = source.Gender,
            Age = source.Age,
            Religion = source.Religion,
            DeathDate = source.DeathDate,
            FuneralDate = source.FuneralDate,
            BurialDate = source.BurialDate,
            RoomId = source.RoomId,
            RoomName = source.RoomName,
            Status = source.Status,
            ChiefMourner = source.ChiefMourner,

            // 영정 · 가족 사진
            MemorialPhotoUrl = source.MemorialPhotoUrl,
            MemorialPhotoFileId = source.MemorialPhotoFileId,
            MemorialEditedPhotoUrl = source.MemorialEditedPhotoUrl,
            MemorialEditedPhotoFileId = source.MemorialEditedPhotoFileId,
            FamilyPhotos = source.FamilyPhotos,

            // 상주는 이름 · 관계 · 대표 여부만 (연락처 · 이메일 · 주소는 뺀다)
            Mourners = source.Mourners
                .Select(m => new DeceasedMournerDto
                {
                    Name = m.Name,
                    Relation = m.Relation,
                    RelationName = m.RelationName,
                    IsChief = m.IsChief,
                    SortOrder = m.SortOrder,
                    Contact = string.Empty, // null 을 허용하지 않는 칸이라 빈 값으로 둔다
                })
                .ToList(),

            // 장비별 표출 장식
            DeviceRibbons = source.DeviceRibbons,
            DeviceTextOverlays = source.DeviceTextOverlays,

            // 옮기지 않는 것: Ssn · CauseOfDeath · BurialPlot · Remark(개인정보), Contractor · Manager(연락처), Facilities · Rooms · FamilyPhotoGroupId.
        };
    }

    /// <summary>입구 안내용 호실 목록. 호실 정보는 그대로 두고 고인만 줄인다.</summary>
    public static List<EntranceGuideRoomDto> ToAnonymousDisplay(this List<EntranceGuideRoomDto>? source)
    {
        if (source is null)
        {
            return new List<EntranceGuideRoomDto>();
        }

        return source
            .Select(room => new EntranceGuideRoomDto
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                FloorName = room.FloorName,
                SortOrder = room.SortOrder,
                DeceasedDetail = room.DeceasedDetail.ToAnonymousDisplay(),
            })
            .ToList();
    }

    /// <summary>키오스크 응답. 건물 · 주차 사진은 그대로 두고 고인만 줄인다.</summary>
    public static KioskGuideResponseDto? ToAnonymousDisplay(this KioskGuideResponseDto? source)
    {
        if (source is null)
        {
            return null;
        }

        return new KioskGuideResponseDto
        {
            Rooms = source.Rooms.ToAnonymousDisplay(),
            BuildingPhotos = source.BuildingPhotos,
            ParkingPhotos = source.ParkingPhotos,
        };
    }

    /// <summary>
    /// 장비 상세에서 표출과 무관한 망 정보(내부 IP · MAC · 공인 IP)만 지운다. 요청마다 새 객체이므로 제자리 수정이 안전하다.
    /// </summary>
    public static DeviceDto? ToAnonymousDisplay(this DeviceDto? source)
    {
        if (source is null)
        {
            return null;
        }

        source.IpAddress = null;
        source.MacAddress = null;
        source.PublicIpAddress = null;
        return source;
    }
}
