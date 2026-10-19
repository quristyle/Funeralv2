namespace funeralv2Api.DTOs;

/// <summary>
/// 건물별 음원 배정 화면의 한 줄. 위에 음원 목록, 아래에 건물 목록을 두고 건물 줄의 체크로 연결을 켠다.
/// </summary>
public class BuildingMusicDto
{
    public string BuildingId { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string? BuildingShortName { get; set; }
    public string? Address { get; set; }
    public int SortOrder { get; set; }

    /// <summary>이 건물에 이 음원이 배정돼 있는지 (옛 <c>mapping</c>)</summary>
    public bool Mapped { get; set; }

    /// <summary>배정 행의 아이디. 배정돼 있을 때만 있다.</summary>
    public string? MappingId { get; set; }
}

/// <summary>
/// 음원 하나를 어느 건물들에 배정할지 한 번에 정한다.
/// </summary>
public class BuildingMusicSaveDto
{
    /// <summary>배정할 건물 아이디 목록. 여기 없는 건물은 배정이 풀린다.</summary>
    public List<string> BuildingIds { get; set; } = new();
}
