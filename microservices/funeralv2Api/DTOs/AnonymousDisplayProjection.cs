using System.Collections.Generic;
using System.Linq;

namespace funeralv2Api.DTOs;

/// <summary>
/// 익명(로그인하지 않은) 표출 장비에게 내려보낼 응답으로 줄여 주는 투영.
/// </summary>
/// <remarks>
/// <para>
/// 게이트웨이에는 빈소 사이니지 플레이어를 위한 익명 라우트가 여섯 개 있다
/// (<c>device/code/{code}</c> · <c>deceased/deviceCode/{deviceCode}</c> ·
/// <c>deceased/guide/…</c> · <c>deceased/kiosk/…</c> · <c>building/source/{id}</c> ·
/// SignalR 허브). 플레이어는 로그인하지 않고 <b>장비 코드만</b> 들고 부른다.
/// </para>
/// <para>
/// 그런데 장비 코드는 <c>JSI-06-0001</c> 처럼 <b>추측 가능</b>하다. 그래서 이 응답들은
/// "코드를 맞힌 사람" 에게 그대로 열린다. 그런데 지금까지 <b>화면에 쓰지도 않는</b>
/// 개인정보가 함께 실려 나갔다 — 주민번호 · 사인 · 비고 · 상주 연락처 · 이메일 ·
/// 주소 · 계약자 · 장례지도사 연락처, 그리고 장비의 내부 IP · MAC · 공인 IP 까지다.
/// </para>
/// <para>
/// 여기서 <b>표출에 필요한 것만</b> 남긴다. 결정 D-M2
/// (docs/analysis/46-player-media-anonymous-access.md 6절).
/// </para>
/// <para>
/// <b>고인 쪽은 '남길 것' 목록(허용 목록)으로 짰다.</b> 필요한 칸이 스무 개인데 민감한
/// 칸이 그만큼 많아서, 새 칸이 DTO 에 생겼을 때 <b>가만히 있으면 새지 않는</b> 쪽이
/// 맞다. 반대로 <b>장비 쪽은 '지울 것' 목록</b>이다 — 표출 설정 칸이 마흔 개가 넘고
/// 민감한 것은 셋뿐이라, 허용 목록으로 짜면 새 표출 설정이 생길 때마다
/// <b>플레이어에서 조용히 빠진다.</b>
/// </para>
/// <para>
/// 인증이 필요한 화면(포털의 고인 상세 · 장비 관리)은 이 투영을 지나지 않는다.
/// 같은 DTO 를 쓰지만 다른 엔드포인트다.
/// </para>
/// </remarks>
public static class AnonymousDisplayProjection
{
    /// <summary>
    /// 고인 상세를 표출에 필요한 칸만 남겨 <b>새 객체로</b> 복사한다.
    /// </summary>
    /// <remarks>
    /// 원본을 고치지 않는다. 같은 요청 안에서 다른 곳이 원본을 함께 보고 있을 수 있고,
    /// 나중에 서비스가 응답을 캐시하게 되면 제자리 수정은 인증된 화면의 응답까지 깎는다.
    /// </remarks>
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

            // 아래는 **일부러 옮기지 않는다.**
            //   Ssn · CauseOfDeath · BurialPlot · Remark  — 개인정보이고 표출에 쓰지 않는다
            //   Contractor · Manager                      — 계약자 · 장례지도사 연락처
            //   Facilities · Rooms · FamilyPhotoGroupId    — 표출에 쓰지 않는다
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
    /// 장비 상세에서 <b>표출과 무관한 망 정보만 지운다.</b>
    /// </summary>
    /// <remarks>
    /// 내부 IP · MAC · 공인 IP 다. 플레이어는 이 셋을 읽지 않는다 —
    /// 설정 화면이 운영체제에서 직접 알아내 저장하는 값이라 되받을 필요가 없다.
    /// 공인 IP 는 그 장례식장의 회선을 가리키므로 굳이 내보낼 이유가 없다.
    /// <para>
    /// 여기만 제자리에서 지운다. 서비스가 요청마다 새 객체를 만들고(캐시 없음),
    /// 표출 설정 칸이 마흔 개가 넘어 복사 목록으로 두면 새 설정이 생길 때마다
    /// 플레이어에서 조용히 빠지기 때문이다.
    /// </para>
    /// </remarks>
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
