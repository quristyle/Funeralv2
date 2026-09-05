using System.Linq.Expressions;

namespace funeralv2Api.Entities;

/// <summary>
/// 호실 점유 판정의 정본.
/// </summary>
/// <remarks>
/// <para>
/// 47번 문서 0단계에서 "배정이 살아 있고 + 고인이 장례 진행중" 으로 술어를 모았는데,
/// 그때 '배정이 살아 있다' 를 <c>end_time IS NULL</c> 하나로 봤다. <b>그것이 틀렸다.</b>
/// </para>
/// <para>
/// <c>deceased_rooms.end_time</c> 은 출상 처리가 적는 '실제 퇴실 시각' 만 담는 칸이
/// 아니다. 고인 폼의 <b>'호실 사용 종료 일시'</b> 로 사람이 미리 넣을 수 있고
/// (<see cref="Services.DeceasedService"/> 의 호실 이력 병합), 그렇게 들어온 값은
/// 대개 <b>미래</b>다. null 만 보면 그 배정이 곧바로 '이미 끝난 것' 이 되어
/// <b>입실 중인 호실이 공실로 보인다.</b>
/// </para>
/// <para>
/// 2026-09-05 실제 사례: 고인 홍길동(<c>FUNERAL_IN_PROGRESS</c>)이 JS VIP 1호에
/// 배정돼 있는데 그 행의 <c>end_time</c> 이 2028-06-30 이라 빈소현황이 공실로 그렸다.
/// 같은 화면의 '마지막 퇴실' 이 2028 년으로 찍혀 있던 것이 단서였다.
/// </para>
/// <para>
/// 그래서 판정에 <b>시각을 넣는다.</b> null 이거나 아직 오지 않았으면 살아 있는 배정이다.
/// null 과 과거 값에 대한 동작은 종전과 같아서 기존 데이터에는 영향이 없다.
/// </para>
/// </remarks>
public static class RoomOccupancy
{
    /// <summary>
    /// 배정이 <paramref name="now"/> 시점에 살아 있는가 (메모리에서 셀 때).
    /// </summary>
    public static bool IsActive(DateTime? endTime, DateTime now) =>
        endTime == null || endTime > now;

    /// <summary>
    /// 살아 있는 배정을 고르는 조건 (<c>Where</c> 하나로 끝나는 조회용).
    /// 조인 안에서는 이 식을 쓸 수 없으므로 같은 조건을 손으로 적고 여기를 가리킨다.
    /// </summary>
    public static Expression<Func<DeceasedRoom, bool>> ActiveAt(DateTime now) =>
        dr => !dr.IsDeleted && (dr.EndTime == null || dr.EndTime > now);
}
