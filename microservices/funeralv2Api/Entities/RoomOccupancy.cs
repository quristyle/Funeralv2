using System.Linq.Expressions;

namespace funeralv2Api.Entities;

/// <summary>
/// 호실 점유 판정의 정본. 배정이 살아 있음 = end_time 이 없거나 기준 시각보다 뒤다.
/// end_time 은 사람이 미리 넣는 사용 종료 예정일이라 미래일 수 있다 — null 만 보면 입실 중인 호실이 공실로 뜬다.
/// </summary>
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
