using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// AI 작업 대상 — <c>projmng/ai-targets</c>.
/// </summary>
/// <remarks>
/// <b>경로를 보내는 유일한 클라이언트다.</b> 작업 쪽은 <c>targetKey</c> 만
/// 보낸다 — 등록은 관리자가, 선택은 작성자가 한다(설계 9.5).
/// </remarks>
public sealed class AiTargetClient(GatewayClient gateway)
{
    private const string Url = "projmng/ai-targets";

    public Task<IReadOnlyList<AiTargetDto>> ListAsync(
        bool onlyEnabled = false, CancellationToken ct = default)
        => gateway.GetListAsync<AiTargetDto>(
            onlyEnabled ? $"{Url}?onlyEnabled=true" : Url, ct);

    /// <summary>
    /// 경로를 어디 아래에 둘 수 있는지. <b>화면에 규칙을 박지 않으려고 물어본다</b> —
    /// 박아 두면 서버 설정을 바꿨을 때 안내만 옛말이 된다.
    /// </summary>
    public Task<IReadOnlyList<string>> AllowedRootsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<string>($"{Url}/allowed-roots", ct);

    public Task<AiTargetDto?> CreateAsync(AiTargetDto item, CancellationToken ct = default)
        => gateway.PostAsync<AiTargetDto>(Url, item, ct);

    public Task<AiTargetDto?> UpdateAsync(AiTargetDto item, CancellationToken ct = default)
        => gateway.PutAsync<AiTargetDto>($"{Url}/{item.TargetKey}", item, ct);

    public Task DeleteAsync(long targetKey, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{targetKey}", ct);
}

/// <summary>AI 가 일할 대상 한 건.</summary>
public sealed class AiTargetDto
{
    public long TargetKey { get; set; }

    public string? TargetNm { get; set; }

    /// <summary><c>repo</c>(git 저장소) · <c>folder</c>(그냥 폴더).</summary>
    public string? TargetKind { get; set; } = "repo";

    /// <summary>운영 서버의 절대 경로. 서버가 검사한다.</summary>
    public string? TargetPath { get; set; }

    public string? RepoUrl { get; set; }
    public string? DefaultRef { get; set; } = "main";
    public string? CredentialRef { get; set; }

    /// <summary><c>worktree</c> · <c>copy</c> · <c>inplace</c>.</summary>
    public string? IsolationMode { get; set; } = "worktree";

    public int? MaxSizeMb { get; set; }
    public string? RunnerKinds { get; set; } = "claude";

    /// <summary><b>켜면 그 대상의 작업이 운영 배포를 일으킬 수 있다.</b></summary>
    public bool AllowPush { get; set; }

    public string? PushRef { get; set; } = "main";
    public string? GateMode { get; set; } = "build";

    /// <summary>
    /// 이 대상을 집을 수 있는 실행기 이름. <b>비면 아무 장비나.</b>
    /// </summary>
    /// <remarks>
    /// DB 는 한 벌인데 경로는 장비마다 다르다 — 개발 장비의 폴더를 운영
    /// 실행기가 집어 가 「대상 폴더가 없습니다」로 실패한 적이 있다.
    /// </remarks>
    public string? RunnerNm { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>지금 이 대상에서 도는 실행. 있으면 다른 작업이 못 들어간다.</summary>
    public long? RunningRunKey { get; set; }

    public string? Comments { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }

    public string KindText => TargetKind == "folder" ? "폴더" : "저장소";

    public string IsolationText => IsolationMode switch
    {
        "worktree" => "worktree",
        "copy" => "복사본",
        "inplace" => "원본 직접",
        _ => IsolationMode ?? string.Empty,
    };
}
