using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 가입 신청 승인 처리. 관리자만 부른다.
/// </summary>
/// <remarks>
/// <para>
/// <c>AdminClient</c> 에 메서드 셋을 더하지 않고 따로 둔 이유는 <b>신청서와
/// 계정이 다른 것이기 때문</b>이다. 계정 관리는 이미 있는 계정을 다루고,
/// 이쪽은 아직 계정이 아닌 것을 다룬다 — 거절하면 사라진다.
/// </para>
/// <para>
/// 신청하는 쪽(익명)은 셸의 <c>AccountClient</c> 다. 여기는 승인하는 쪽이라
/// 토큰을 붙이는 <see cref="GatewayClient"/> 를 그대로 쓴다.
/// </para>
/// </remarks>
public sealed class SignupClient(GatewayClient gateway)
{
    /// <summary>승인 대기 목록. 오래된 신청이 위로 온다.</summary>
    public Task<IReadOnlyList<SignupPendingDto>> GetPendingAsync(CancellationToken ct = default)
        => gateway.GetListAsync<SignupPendingDto>("auth/system/signup/list", ct);

    /// <summary>승인한다. 이 순간부터 그 사람이 로그인할 수 있다.</summary>
    public Task ApproveAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/signup/{id}/approve", null, ct);

    /// <summary>
    /// 거절한다. <b>신청이 사라진다</b> — 상태만 바꿔 두면 그 아이디가 영영
    /// 묶여서 잘못 적어 거절당한 사람이 다시 신청할 수 없다.
    /// </summary>
    public Task RejectAsync(string id, string? reason, CancellationToken ct = default)
        => gateway.PostAsync(
            $"auth/system/signup/{id}/reject?reason={Uri.EscapeDataString(reason ?? string.Empty)}",
            null, ct);
}

/// <summary>승인 대기 신청 한 줄.</summary>
public sealed class SignupPendingDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>신청자가 적은 말. 소속·담당 업무가 여기 들어온다.</summary>
    public string? Note { get; set; }

    public DateTime RequestedAt { get; set; }
}
