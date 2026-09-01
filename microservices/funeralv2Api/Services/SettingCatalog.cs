using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 장례식장 업무 설정에 어떤 것이 있는지 적어 둔 곳.
/// </summary>
/// <remarks>
/// 옛 시스템은 설정 이름을 <c>smfr.t_code</c> 에 두고 값을 <c>t_account_conf</c> 에
/// 두었다. 코드가 여덟뿐이고 늘 코드와 화면이 함께 바뀌었으므로, 표를 하나 더 두는 대신
/// 여기에 적는다. 값만 <c>smfr.account_settings</c> 에 저장한다.
///
/// 옛 여덟 중 넷은 옮기지 않았다 — <c>page_tab_view</c> · <c>side_bar_open</c> ·
/// <c>side_menu_expend</c> · <c>side_bar_autohide</c> 는 vben 의 개인 환경설정
/// (탭 표시 · 사이드바 접기)이 이미 하는 일이다. 같은 것을 두 군데서 켜면 서로 어긋난다.
/// 40번 문서의 D-F3 에 적어 두었다.
/// </remarks>
public static class SettingCatalog
{
    /// <summary>설정 하나의 정의</summary>
    public record Definition(string Code, string Name, string GroupName, bool DefaultValue, string Description);

    /// <summary>
    /// 다룰 수 있는 설정 전부. 순서가 화면에 나오는 순서다.
    /// </summary>
    public static readonly IReadOnlyList<Definition> All = new List<Definition>
    {
        new("multy_room_use", "기존 고객 연결 기능 사용", "빈소 운영", false,
            "고인을 새로 만들지 않고 이미 있는 고인을 다른 호실에 다시 연결할 수 있게 한다. 한 상가가 빈소를 옮기거나 둘을 함께 쓸 때 쓴다."),

        new("auto_goin_name", "호실 이름 대신 고인 명칭 사용", "빈소 운영", false,
            "장비 화면의 제목을 호실 이름이 아니라 고인 이름으로 띄운다. 옛 시스템의 '호실-자동생성 대신 고인 명칭 사용' 이다."),

        new("machine_create_auto_conf", "장비 추가 시 기본값 자동 세팅", "장비", true,
            "장비를 새로 등록할 때 영정 1개 · 좌 140 · 우 170 · 화면비 16:10 을 미리 넣어 준다."),

        new("hide_company", "회사 선택 칸 숨기기", "화면", false,
            "회사가 하나뿐인 곳에서 검색 조건의 회사 칸을 감춘다."),
    };

    /// <summary>코드로 정의를 찾는다. 없으면 null.</summary>
    public static Definition? Find(string code) =>
        All.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 저장된 값을 정의에 얹어 화면이 쓸 모양으로 만든다.
    /// 저장된 적 없는 설정은 기본값으로 채운다.
    /// </summary>
    public static List<AccountSettingDto> Merge(IEnumerable<Entities.AccountSetting> saved)
    {
        var map = saved
            .GroupBy(s => s.SettingCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);

        return All.Select(d =>
        {
            map.TryGetValue(d.Code, out var row);
            return new AccountSettingDto
            {
                Code = d.Code,
                Name = d.Name,
                Description = d.Description,
                GroupName = d.GroupName,
                DefaultValue = d.DefaultValue,
                Enabled = row is null ? d.DefaultValue : IsOn(row.SettingValue),
                UpdatedAt = row?.UpdatedAt ?? row?.CreatedAt,
            };
        }).ToList();
    }

    /// <summary>옛 표기(<c>Y</c>/<c>N</c>)를 참·거짓으로 읽는다.</summary>
    public static bool IsOn(string? value) =>
        string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || value == "1";

    /// <summary>참·거짓을 옛 표기로 적는다.</summary>
    public static string ToStored(bool enabled) => enabled ? "Y" : "N";
}
