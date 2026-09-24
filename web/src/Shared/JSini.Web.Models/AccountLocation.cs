namespace JSini.Web.Models;

// ============================================================
// 위치 — **사람이 허락해서** 서버에 남은 좌표.
//
// 이 값은 원래 「내 위치 날씨」를 보내려고 받아 둔 것이다
// (`NotificationSettings.cs` 의 `NotificationPreferenceDto.WeatherLat`).
// 지도 화면은 같은 값을 **점으로 찍어** 보여 줄 뿐 새로 받지 않는다.
//
// 칸 이름은 NotificationServer 의 DTO 와 맞춘다 — 봉투를 그대로 주고받는다.
// ============================================================

/// <summary>
/// 위치를 허용한 계정 하나 — <b>어디에 있고, 쪽지가 닿는가</b>.
/// </summary>
/// <remarks>
/// 한 줄에 둘을 담는다. 점을 찍는 데 필요한 것(좌표 · 이름)과, 그 점을 눌렀을 때
/// <b>곧바로 쪽지를 보낼 수 있는지</b>다. 나뉘어 오면 화면이 점을 다 찍어 놓고
/// 사람마다 「이 사람에게 보낼 수 있나」를 다시 물어야 한다.
/// </remarks>
public sealed class AccountLocationDto
{
    /// <summary>포털 로그인 아이디. <b>쪽지도 이 값 하나로 간다.</b></summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름. 비면 화면이 아이디를 대신 적는다.</summary>
    public string? Name { get; set; }

    /// <summary>회사 · 부서. 같은 이름이 둘일 때 가르는 값이다.</summary>
    public string? Affiliation { get; set; }

    /// <summary>대표 이메일. 없으면 메일로는 두드릴 수 없다.</summary>
    public string? Email { get; set; }

    /// <summary>위도(10진 도).</summary>
    public double Lat { get; set; }

    /// <summary>경도(10진 도).</summary>
    public double Lon { get; set; }

    /// <summary>
    /// 보여 줄 지역 이름(<c>울산광역시 남구 삼산동</c>). <b>비어 있는 경우가
    /// 흔하다</b> — 날씨를 한 번도 안 받으면 채워지지 않는다.
    /// </summary>
    public string? Place { get; set; }

    /// <summary>자리를 마지막으로 <b>잡은</b> 때 — 좌표가 실제로 달라진 때다.</summary>
    public DateTime? LocatedAt { get; set; }

    /// <summary>
    /// 자리를 마지막으로 <b>확인한</b> 때. 안 움직였어도 포털을 열면 찍힌다.
    /// </summary>
    /// <remarks>
    /// 지도에서 이 값이 중요한 까닭은 <b>점이 얼마나 묵었는지</b>가 그것으로만
    /// 갈리기 때문이다. 「잡은 때」가 반 년 전이어도 어제 확인했다면 그 사람은
    /// 줄곧 거기 있는 것이다.
    /// </remarks>
    public DateTime? SyncedAt { get; set; }

    /// <summary>
    /// 「내 위치 날씨」를 켜 두었나. <b>위치 허용 여부와 다르다</b> —
    /// 스위치를 껐어도 좌표는 남아 있다.
    /// </summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>앱 푸시가 실제로 닿는가 — 끄지 않았고 등록한 기기가 있다.</summary>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>
    /// 쪽지를 받을 길이 있는가. <b>둘 중 하나면 된다</b> — 쪽지 찾기·보내기와
    /// 같은 값으로 가른다(<see cref="NoteRecipientDto.CanReceive"/>).
    /// </summary>
    public bool CanReceive => PushReachable || EmailReachable;

    /// <summary>
    /// 마지막으로 자리가 확인된 때. <b>「확인」이 「잡음」보다 늘 나중</b>이라
    /// 둘 중 큰 쪽이다.
    /// </summary>
    /// <remarks>
    /// 화면이 「묵음」을 가르는 값이다. 한쪽만 보면 틀린다 — 「잡은 때」만 보면
    /// 이사를 안 한 사람이 전부 묵은 점이 되고, 「확인한 때」만 보면 그 칸이
    /// 비어 있는 옛 줄(확인 기록이 생기기 전에 저장된 것)이 전부 묵은 점이 된다.
    /// </remarks>
    public DateTime? LastKnownAt =>
        (LocatedAt, SyncedAt) switch
        {
            (null, null) => null,
            (null, var s) => s,
            (var l, null) => l,
            var (l, s) => l > s ? l : s,
        };
}
