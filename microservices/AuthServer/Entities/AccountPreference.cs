using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 계정별 화면 환경설정 (테마 · 레이아웃 · 위젯 위치 · 단축키 …)
/// </summary>
/// <remarks>
/// 예전에는 브라우저 로컬스토리지에만 있어서 <b>사람이 아니라 브라우저에 붙었다.</b>
/// 다른 PC 에서 로그인하면 기본값으로 돌아가고, 캐시를 지우면 사라졌다.
/// 계정에 붙여 두면 어디서 로그인해도 따라온다.
///
/// <para>
/// <b>서버는 이 값을 해석하지 않는다.</b> 프론트가 만든 JSON 을 그대로 보관하고
/// 그대로 돌려준다. 설정 항목이 40개가 넘고 상위 동기화마다 늘어나므로,
/// 칸으로 쪼개면 항목이 생길 때마다 마이그레이션이 필요하다.
/// </para>
///
/// <para>
/// 담기는 것은 <b>기본값과 다른 항목만</b>이다(프론트의 <c>diffPreference</c>).
/// 전체를 담으면 나중에 프레임워크 기본값이 바뀌어도 옛 값이 박혀 따라오지 않는다.
/// 실제로 그 사고를 겪었다 — 상위 동기화가 로그아웃 버튼 위치의 기본값을 바꿨을 때
/// 저장돼 있던 전체 값이 우선해서 새 기본값이 반영되지 않았다.
/// </para>
/// </remarks>
[Table("account_preferences", Schema = "scom")]
public class AccountPreference : BaseEntity<string>
{
    public AccountPreference()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 계정 키(accounts.id). 계정 하나에 한 행이라 DB 에 UNIQUE 가 걸려 있다.
    /// </summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    /// <summary>
    /// 기본값과 다른 항목만 담은 JSON 문자열. <c>jsonb</c> 로 저장한다.
    /// 서버가 들여다보지 않으므로 문자열로 들고 있는 것이 가장 싸다.
    /// </summary>
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";
}
