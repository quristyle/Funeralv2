using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 시스템 메뉴 정보 엔티티 클래스
/// </summary>
[Table("system_menus", Schema = "scom")]
public class SystemMenu : BaseEntity
{
    /// <summary>메뉴 고유 ID (Primary Key)</summary>
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>메뉴 이름 (다국어 키 또는 명칭)</summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>라우트 경로</summary>
    [Required]
    [Column("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>컴포넌트 경로</summary>
    [Column("component")]
    public string? Component { get; set; }

    /// <summary>부모 메뉴 ID</summary>
    [Column("pid")]
    public string? Pid { get; set; }

    /// <summary>리다이렉트 경로</summary>
    [Column("redirect")]
    public string? Redirect { get; set; }

    /// <summary>메뉴 유형 (catalog, menu, button 등)</summary>
    [Required]
    [Column("type")]
    public string Type { get; set; } = "menu";

    /// <summary>권한 코드</summary>
    [Column("auth_code")]
    public string? AuthCode { get; set; }

    /// <summary>메뉴 제목</summary>
    [Column("title")]
    public string? Title { get; set; }

    /// <summary>메뉴 아이콘</summary>
    [Column("icon")]
    public string? Icon { get; set; }

    /// <summary>정렬 순서</summary>
    [Column("order_no")]
    public int OrderNo { get; set; } = 0;

    /// <summary>메뉴 숨김 여부</summary>
    [Column("hide_in_menu")]
    public bool HideInMenu { get; set; } = false;

    /// <summary>페이지 캐싱(Keep-Alive) 여부</summary>
    [Column("keep_alive")]
    public bool KeepAlive { get; set; } = true;

    /// <summary>탭 고정 여부</summary>
    [Column("affix_tab")]
    public bool AffixTab { get; set; } = false;

    /// <summary>DOM 캐싱 여부</summary>
    [Column("dom_cached")]
    public bool DomCached { get; set; } = false;

    /// <summary>권한 목록 (콤마 구분)</summary>
    [Column("authority")]
    public string? Authority { get; set; }

    /// <summary>권한 없을 때 메뉴 표시 여부</summary>
    [Column("menu_visible_with_forbidden")]
    public bool MenuVisibleWithForbidden { get; set; } = false;

    /// <summary>외부 링크 URL</summary>
    [Column("link")]
    public string? Link { get; set; }

    /// <summary>Iframe 소스 URL</summary>
    [Column("iframe_src")]
    public string? IframeSrc { get; set; }

    /// <summary>뱃지 유형 (dot 등)</summary>
    [Column("badge_type")]
    public string? BadgeType { get; set; }

    /// <summary>뱃지 내용</summary>
    [Column("badge")]
    public string? Badge { get; set; }

    /// <summary>상태 (0: 비활성, 1: 활성)</summary>
    [Column("status")]
    public int Status { get; set; } = 1;
}
