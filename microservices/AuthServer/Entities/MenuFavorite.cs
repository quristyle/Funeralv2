using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>사용자별 즐겨찾기 메뉴(계정 - 메뉴 N:M 매핑 테이블). 메뉴를 경로가 아니라 식별자로 가리킨다.</summary>
[Table("menu_favorites", Schema = "scom")]
public class MenuFavorite : BaseEntity<int>
{
    /// <summary>연관된 사용자 계정 식별자(<c>scom.accounts.id</c>). 게이트웨이의 X-User-Id 는 로그인 아이디라 다르므로 계정을 찾아 이 식별자로 바꿔 담는다.</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>연관된 사용자 계정 엔티티 탐색 속성</summary>
    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    /// <summary>즐겨찾기한 메뉴 식별자 (ID) — <c>scom.system_menus.id</c></summary>
    [Required]
    [Column("menu_id")]
    public string MenuId { get; set; } = string.Empty;

    /// <summary>연관된 메뉴 엔티티 탐색 속성</summary>
    [ForeignKey(nameof(MenuId))]
    public virtual SystemMenu? Menu { get; set; }

    /// <summary>사이드바 즐겨찾기 묶음에서의 표시 순서 (작은 값이 위)</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }
}
