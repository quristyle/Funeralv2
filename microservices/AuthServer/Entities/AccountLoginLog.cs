using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 로그인 시도 기록 (성공·실패 모두)
/// </summary>
/// <remarks>
/// `accounts` 의 <c>last_login_at</c> · <c>last_login_ip</c> 는 **마지막 한 번**만 남는다.
/// 그것만으로는 계정 화면이 "지금 이 접속" 밖에 보여 줄 수 없다.
///
/// 사람이 자기 계정 화면에서 실제로 궁금해하는 것은 셋이다.
///   · 지난번에는 언제·어디서 들어왔나
///   · 누가 내 아이디로 로그인을 시도했나
///   · 이 계정을 얼마나 써 왔나
///
/// 앞의 둘은 낯선 접근을 알아채는 단서다. 그래서 시도를 한 줄씩 쌓는다.
/// **실패도 남긴다** — 성공만 남기면 두드림을 볼 수 없다.
/// </remarks>
[Table("account_login_logs", Schema = "scom")]
public class AccountLoginLog : BaseEntity<string>
{
    public AccountLoginLog()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 계정 키(accounts.id). 계정을 못 찾은 실패는 비어 있다.
    /// </summary>
    [Column("account_id")]
    public string? AccountId { get; set; }

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    /// <summary>
    /// 입력된 로그인 아이디. 계정을 못 찾은 실패도 무엇을 시도했는지 남는다.
    /// </summary>
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

    /// <summary>
    /// 비밀번호는 맞았지만 쓸 수 있는 계정이 아니다 (승인 대기 · 정지).
    ///
    /// <para>
    /// 비밀번호가 맞았다는 뜻이므로 <see cref="BadPassword"/> 와 섞으면 안 된다 —
    /// 「누가 내 아이디를 두드리고 있다」를 보려고 만든 기록에서 이 둘은 무게가
    /// 아주 다르다.
    /// </para>
    /// </summary>
    public const string NotActive = "NOT_ACTIVE";
}
