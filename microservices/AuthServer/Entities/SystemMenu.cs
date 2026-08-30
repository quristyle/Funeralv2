using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 시스템 메뉴 정보 엔티티 클래스
/// </summary>
[Table("system_menus", Schema = "scom")]
public class SystemMenu : BaseEntity<string>
{
    /// <summary>
    /// SystemMenu 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public SystemMenu()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 메뉴 이름 (다국어 키 또는 명칭)
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 라우트 경로 (URL)
    /// </summary>
    [Required]
    [Column("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 프론트엔드 컴포넌트 파일 경로
    /// </summary>
    [Column("component")]
    public string? Component { get; set; }

    /// <summary>
    /// 부모 메뉴 식별자 (ID)
    /// </summary>
    [Column("pid")]
    public string? Pid { get; set; }

    /// <summary>
    /// 리다이렉트할 경로 (URL)
    /// </summary>
    [Column("redirect")]
    public string? Redirect { get; set; }

    /// <summary>
    /// 메뉴 유형 (catalog, menu, button 등, 기본값: menu)
    /// </summary>
    [Required]
    [Column("type")]
    public string Type { get; set; } = "menu";

    /// <summary>
    /// 권한 식별 코드
    /// </summary>
    [Column("auth_code")]
    public string? AuthCode { get; set; }

    /// <summary>
    /// 메뉴 제목 (화면에 표시할 텍스트)
    /// </summary>
    [Column("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 메뉴 아이콘 명칭 (예: AntDesign 등)
    /// </summary>
    [Column("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Column("order_no")]
    public int OrderNo { get; set; } = 0;

    /// <summary>
    /// 메뉴 표시 숨김 여부
    /// </summary>
    [Column("hide_in_menu")]
    public bool HideInMenu { get; set; } = false;

    /// <summary>
    /// 페이지 캐싱(Keep-Alive) 적용 여부
    /// </summary>
    [Column("keep_alive")]
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// 탭 고정 여부
    /// </summary>
    [Column("affix_tab")]
    public bool AffixTab { get; set; } = false;

    /// <summary>
    /// DOM 캐싱 여부
    /// </summary>
    [Column("dom_cached")]
    public bool DomCached { get; set; } = false;

    /// <summary>
    /// 허용 권한 목록 (콤마 구분)
    /// </summary>
    [Column("authority")]
    public string? Authority { get; set; }

    /// <summary>
    /// 권한이 없을 때 메뉴 표시 여부
    /// </summary>
    [Column("menu_visible_with_forbidden")]
    public bool MenuVisibleWithForbidden { get; set; } = false;

    /// <summary>
    /// 외부 링크 URL
    /// </summary>
    [Column("link")]
    public string? Link { get; set; }

    /// <summary>
    /// Iframe 소스 URL (웹뷰용)
    /// </summary>
    [Column("iframe_src")]
    public string? IframeSrc { get; set; }

    /// <summary>
    /// 뱃지 유형 (예: dot 등)
    /// </summary>
    [Column("badge_type")]
    public string? BadgeType { get; set; }

    /// <summary>
    /// 뱃지에 표시할 텍스트 내용
    /// </summary>
    [Column("badge")]
    public string? Badge { get; set; }

    /// <summary>
    /// 메뉴 사용 상태 (0: 비활성, 1: 활성)
    /// </summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    // ── 화면 크기별 메뉴목록 노출 ────────────────────────────
    //
    // 포털은 PWA 라 휴대폰·태블릿에서도 같은 메뉴를 받는다(40번 문서).
    // 데스크톱에서만 쓸모 있는 화면까지 작은 화면의 메뉴목록에 나오면 목록만 길어진다.
    // 아래 두 값이 false 면 그 크기의 **메뉴목록에서만** 빠진다 —
    // 라우트는 그대로 만들어지므로 주소·즐겨찾기로는 열린다(Status=0 과 다른 뜻이다).

    /// <summary>휴대폰 크기(&lt;768px) 메뉴목록 노출 여부</summary>
    [Column("use_mobile")]
    public bool UseMobile { get; set; } = true;

    /// <summary>태블릿 크기(768~1023px) 메뉴목록 노출 여부</summary>
    [Column("use_tablet")]
    public bool UseTablet { get; set; } = true;

    // ── 권한 항목 사용 설정 ────────────────────────────────
    //
    // role_menus 는 메뉴마다 15가지 권한 칸을 들고 있지만, 메뉴마다 실제로
    // 의미 있는 권한은 다르다. 아래 값으로 그 메뉴가 어떤 권한을 쓰는지 정해두면
    // 역할 권한 화면이 해당 항목만 켜서 보여준다.
    // 사용자 정의 권한 1~8 은 이름을 붙여야 무엇인지 알 수 있으므로 이름도 함께 둔다.

    /// <summary>열람 권한 사용 여부</summary>
    [Column("use_view")]
    public bool UseView { get; set; } = true;

    /// <summary>조회(검색) 권한 사용 여부</summary>
    [Column("use_search")]
    public bool UseSearch { get; set; } = true;

    /// <summary>추가(등록) 권한 사용 여부</summary>
    [Column("use_create")]
    public bool UseCreate { get; set; } = true;

    /// <summary>삭제 권한 사용 여부</summary>
    [Column("use_delete")]
    public bool UseDelete { get; set; } = true;

    /// <summary>수정 권한 사용 여부</summary>
    [Column("use_update")]
    public bool UseUpdate { get; set; } = true;

    /// <summary>출력 권한 사용 여부</summary>
    [Column("use_print")]
    public bool UsePrint { get; set; } = true;

    /// <summary>엑셀 권한 사용 여부</summary>
    [Column("use_excel")]
    public bool UseExcel { get; set; } = true;

    /// <summary>사용자 정의 권한 1 사용 여부</summary>
    [Column("use_cust1")]
    public bool UseCust1 { get; set; }

    /// <summary>사용자 정의 권한 2 사용 여부</summary>
    [Column("use_cust2")]
    public bool UseCust2 { get; set; }

    /// <summary>사용자 정의 권한 3 사용 여부</summary>
    [Column("use_cust3")]
    public bool UseCust3 { get; set; }

    /// <summary>사용자 정의 권한 4 사용 여부</summary>
    [Column("use_cust4")]
    public bool UseCust4 { get; set; }

    /// <summary>사용자 정의 권한 5 사용 여부</summary>
    [Column("use_cust5")]
    public bool UseCust5 { get; set; }

    /// <summary>사용자 정의 권한 6 사용 여부</summary>
    [Column("use_cust6")]
    public bool UseCust6 { get; set; }

    /// <summary>사용자 정의 권한 7 사용 여부</summary>
    [Column("use_cust7")]
    public bool UseCust7 { get; set; }

    /// <summary>사용자 정의 권한 8 사용 여부</summary>
    [Column("use_cust8")]
    public bool UseCust8 { get; set; }

    /// <summary>사용자 정의 권한 1 표시 이름</summary>
    [Column("cust1_name")]
    public string? Cust1Name { get; set; }

    /// <summary>사용자 정의 권한 2 표시 이름</summary>
    [Column("cust2_name")]
    public string? Cust2Name { get; set; }

    /// <summary>사용자 정의 권한 3 표시 이름</summary>
    [Column("cust3_name")]
    public string? Cust3Name { get; set; }

    /// <summary>사용자 정의 권한 4 표시 이름</summary>
    [Column("cust4_name")]
    public string? Cust4Name { get; set; }

    /// <summary>사용자 정의 권한 5 표시 이름</summary>
    [Column("cust5_name")]
    public string? Cust5Name { get; set; }

    /// <summary>사용자 정의 권한 6 표시 이름</summary>
    [Column("cust6_name")]
    public string? Cust6Name { get; set; }

    /// <summary>사용자 정의 권한 7 표시 이름</summary>
    [Column("cust7_name")]
    public string? Cust7Name { get; set; }

    /// <summary>사용자 정의 권한 8 표시 이름</summary>
    [Column("cust8_name")]
    public string? Cust8Name { get; set; }
}
