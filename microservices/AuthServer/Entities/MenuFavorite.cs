using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 사용자별 즐겨찾기 메뉴 엔티티 클래스 (계정 - 메뉴 N:M 관계 해소용 매핑 테이블)
///
/// <para>
/// 자주 쓰는 화면을 사용자가 직접 모아 두는 곳이다. 탭을 오른쪽 눌러 추가하고,
/// 왼쪽 사이드바 맨 위 '즐겨찾기' 묶음에서 바로 열 수 있다.
/// </para>
///
/// <para>
/// <b>메뉴를 경로가 아니라 식별자로 가리킨다.</b> 화면(탭)이 아는 것은 경로뿐이라
/// 등록·해제 API 는 경로를 받지만, 저장할 때는 <c>scom.system_menus</c> 를 찾아
/// 그 식별자를 넣는다. 경로를 그대로 저장하면 메뉴 관리에서 경로를 고치는 순간
/// 즐겨찾기가 아무 곳도 가리키지 않는 값으로 조용히 남는다.
/// </para>
///
/// <para>
/// 제목·아이콘은 여기 담지 않는다. 메뉴 쪽 값이 정본이고, 읽을 때 함께 조회한다.
/// 베껴 두면 메뉴 제목을 고쳤을 때 즐겨찾기만 옛 이름으로 남는다.
/// </para>
/// </summary>
[Table("menu_favorites", Schema = "scom")]
public class MenuFavorite : BaseEntity<int>
{
    /// <summary>
    /// 연관된 사용자 계정 식별자 (ID) — <c>scom.accounts.id</c>
    /// </summary>
    /// <remarks>
    /// 게이트웨이가 보내는 <c>X-User-Id</c> 는 로그인 아이디(<c>accounts.user_id</c>)라
    /// 이 값과 다르다. 서비스에서 계정을 찾아 이 식별자로 바꿔 담는다.
    /// </remarks>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 사용자 계정 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    /// <summary>
    /// 즐겨찾기한 메뉴 식별자 (ID) — <c>scom.system_menus.id</c>
    /// </summary>
    [Required]
    [Column("menu_id")]
    public string MenuId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 메뉴 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(MenuId))]
    public virtual SystemMenu? Menu { get; set; }

    /// <summary>
    /// 사이드바 즐겨찾기 묶음에서의 표시 순서 (작은 값이 위)
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }
}
