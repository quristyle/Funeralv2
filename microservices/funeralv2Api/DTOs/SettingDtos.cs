namespace funeralv2Api.DTOs;

/// <summary>
/// 계정별 업무 설정 한 줄. 옛 <c>t_account_conf</c> 와 <c>t_code</c> 를 합친 모양이다.
/// </summary>
public class AccountSettingDto
{
    /// <summary>설정 코드 (옛 <c>conf_cd</c>)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>화면에 보일 이름 (옛 <c>t_code.cd_nm</c>)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>왜 있는 설정인지</summary>
    public string? Description { get; set; }

    /// <summary>묶음 이름. 화면에서 구역을 나눈다.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>지금 값</summary>
    public bool Enabled { get; set; }

    /// <summary>아무것도 저장한 적 없을 때 쓰는 값</summary>
    public bool DefaultValue { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 설정 한 줄을 바꾼다.
/// </summary>
public class AccountSettingUpdateDto
{
    public bool Enabled { get; set; }
}

/// <summary>
/// 여러 줄을 한 번에 바꾼다. 화면이 저장 버튼 하나로 끝내도록.
/// </summary>
public class AccountSettingBulkUpdateDto
{
    public Dictionary<string, bool> Settings { get; set; } = new();
}
