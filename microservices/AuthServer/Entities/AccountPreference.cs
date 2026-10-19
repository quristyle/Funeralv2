using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>계정별 화면 환경설정(테마 · 레이아웃 · 위젯 위치 · 단축키 …). 브라우저가 아니라 계정에 붙여 어느 기기에서 로그인해도 따라오게 한다.</summary>
[Table("account_preferences", Schema = "scom")]
public class AccountPreference : BaseEntity<string>
{
    public AccountPreference()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>계정 키(accounts.id). 계정 하나에 한 행이라 DB 에 UNIQUE 가 걸려 있다.</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    /// <summary>기본값과 다른 항목만 담은 JSON 문자열. <c>jsonb</c> 로 저장한다. 서버가 들여다보지 않으므로 문자열로 들고 있는 것이 가장 싸다.</summary>
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";
}
