using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>가입 신청 승인 처리. 관리자만 부른다.</summary>
public sealed class SignupClient(GatewayClient gateway)
{
    /// <summary>승인 대기 목록. 오래된 신청이 위로 온다.</summary>
    public Task<IReadOnlyList<SignupPendingDto>> GetPendingAsync(CancellationToken ct = default)
        => gateway.GetListAsync<SignupPendingDto>("auth/system/signup/list", ct);

    /// <summary>승인한다. 이 순간부터 그 사람이 로그인할 수 있다.</summary>
    public Task ApproveAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/signup/{id}/approve", null, ct);

    /// <summary>거절한다. 신청이 사라진다 — 상태만 바꿔 두면 그 아이디가 영영 묶여서 잘못 적어 거절당한 사람이 다시 신청할 수 없다.</summary>
    public Task RejectAsync(string id, string? reason, CancellationToken ct = default)
        => gateway.PostAsync(
            $"auth/system/signup/{id}/reject?reason={Uri.EscapeDataString(reason ?? string.Empty)}",
            null, ct);
    public Task MergeAsync(string pendingId, string targetId, CancellationToken ct = default)
        => gateway.PostAsync(
            $"auth/system/signup/{pendingId}/merge?targetId={Uri.EscapeDataString(targetId)}",
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

    /// <summary>소셜로 들어온 신청이면 공급자 열쇠(<c>kakao</c> · <c>naver</c> · <c>google</c>). 아이디·비밀번호로 직접 신청했으면 <c>null</c>.</summary>
    public string? SocialProvider { get; set; }

    /// <summary>공급자에게서 받은 프로필 사진(https). 없으면 <c>null</c>.</summary>
    public string? PictureUrl { get; set; }

    /// <summary>우리 FileServer 로 옮겨 둔 계정 대표 사진. 없으면 <c>null</c>.</summary>
    public string? Avatar { get; set; }

    /// <summary>표에 그릴 얼굴. 옮겨 둔 사진이 먼저다 — 공급자 주소는 공급자가 바꾸거나 막을 수 있어서, 우리 사진이 없을 때만 물러선다.</summary>
    public string? FaceUrl =>
        Avatar is { Length: > 0 } && JSini.Web.Components.Data.FileDownload.FileIdOf(Avatar) is { } fileId
            ? JSini.Web.Components.Data.FileDownload.ThumbnailUrlFor(fileId)
            : PictureUrl;

    /// <summary>「가입 경로」 칸에 적을 말. 엑셀로 내보낼 때도 이 값이 나가도록 칸의 <c>FieldName</c> 을 여기에 건다.</summary>
    public string Route => SocialProvider?.ToLowerInvariant() switch
    {
        null or "" => "직접 입력",
        "kakao" => "카카오",
        "naver" => "네이버",
        "google" => "구글",
        var other => other,
    };
}
