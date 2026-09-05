using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models;

/// <summary>
/// funeralv2(AuthServer) 계정과 헬프데스크 계정(Admin/Customer)을 잇는 매핑.
///
/// 헬프데스크는 원래 자체 계정(Admins/Customers)으로만 로그인했다. funeralv2 로 계정을 단일화하면서
/// AuthServer 가 발급한 토큰 하나로 헬프데스크 API 를 쓸 수 있어야 하는데,
/// 기존 데이터(요청 작성자·담당자 등)가 모두 헬프데스크 내부 ID 를 참조하고 있어 그 ID 를 버릴 수 없다.
/// 그래서 기존 테이블은 건드리지 않고 이 매핑 테이블만 추가해 두 체계를 연결한다.
/// </summary>
// 컬럼명은 AppDbContext 규칙에 따라 속성명을 소문자로 만든 값이 된다 (authuserid, usertype ...).
// 스키마를 여기 적지 않는다. 적으면 AppDbContext.Schema 설정을 이 표만 안 따라가
// 다른 DB 로 옮겼을 때 이 표에서만 "relation does not exist" 가 난다 — 실제로 그랬다.
[Table("auth_user_links")]
public class AuthUserLink {
  /// <summary>매핑 식별자</summary>
  public int Id { get; set; }

  /// <summary>AuthServer 계정 식별자 (scom.accounts.user_id, JWT 의 NameIdentifier 클레임)</summary>
  [Required]
  public string AuthUserId { get; set; } = string.Empty;

  /// <summary>헬프데스크 계정 종류 — <c>admin</c> 또는 <c>customer</c></summary>
  [Required]
  public string UserType { get; set; } = "customer";

  /// <summary>헬프데스크 내부 계정 ID (jsini.admin.id 또는 jsini.customer.id)</summary>
  public int HelpdeskUserId { get; set; }

  /// <summary>매핑 생성 시각</summary>
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  /// <summary>매핑을 만든 주체</summary>
  public string? CreatedBy { get; set; }
}
