using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>로그인 시도 기록 (성공·실패 모두)</summary>
/// 계정의 마지막 로그인은 한 번만 남으므로, 시도를 한 줄씩 쌓아 낯선 접근과 두드림을 알아볼 수 있게 한다.
/// 실패도 남긴다 — 성공만 남기면 두드림을 볼 수 없다.
[Table("account_login_logs", Schema = "scom")]
public class AccountLoginLog : BaseEntity<string>
{
    public AccountLoginLog()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>계정 키(accounts.id). 계정을 못 찾은 실패는 비어 있다.</summary>
    [Column("account_id")]
    public string? AccountId { get; set; }

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    /// <summary>입력된 로그인 아이디. 계정을 못 찾은 실패도 무엇을 시도했는지 남는다.</summary>
    [Required]
    [Column("login_id")]
    public string LoginId { get; set; } = string.Empty;

    /// <summary>성공했는지</summary>
    [Column("success")]
    public bool Success { get; set; }

    /// <summary>실패 이유. 성공이면 null</summary>
    [Column("fail_reason")]
    public string? FailReason { get; set; }

    /// <summary>접속 IP. 게이트웨이 뒤이므로 X-Forwarded-For 의 첫 값이다.</summary>
    [Column("ip")]
    public string? Ip { get; set; }

    /// <summary>브라우저·기기. 낯선 접속을 알아보는 단서다.</summary>
    [Column("user_agent")]
    public string? UserAgent { get; set; }
}

/// <summary>로그인 실패 이유</summary>
public static class LoginFailReason
{
    /// <summary>그 아이디의 계정이 없다</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>비밀번호가 다르다</summary>
    public const string BadPassword = "BAD_PASSWORD";

    /// <summary>비밀번호는 맞았지만 쓸 수 있는 계정이 아니다(승인 대기 · 정지).</summary>
    /// BadPassword 와 섞으면 「누가 내 아이디를 두드리나」를 볼 수 없으므로 따로 둔다.
    public const string NotActive = "NOT_ACTIVE";
}
