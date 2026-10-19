namespace ProjMngServer.Models;

/// <summary>AI 가 일할 대상(<c>projmng.ai_target</c>). 경로를 사람이 타이핑하지 않게 하는 표다 — 자유 입력으로 두면 상위 경로(../)가 들어온다. 등록은 관리자가 하고 선택만 화면이 한다.</summary>
public sealed class AiTarget
{
    public long TargetKey { get; set; }

    public string? TargetNm { get; set; }

    /// <summary><c>repo</c>(git 저장소) · <c>folder</c>(그냥 폴더).</summary>
    public string? TargetKind { get; set; } = AiTargetKind.Repo;

    /// <summary>운영 서버의 <b>절대 경로</b>. 등록할 때 검사한다.</summary>
    public string? TargetPath { get; set; }

    public string? RepoUrl { get; set; }

    public string? DefaultRef { get; set; } = "main";

    /// <summary>비공개 저장소일 때 쓸 읽기 전용 키의 이름. 키 자체는 DB 에 넣지 않고 실행기만 읽는 파일에 둔다.</summary>
    public string? CredentialRef { get; set; }

    /// <summary><c>worktree</c> · <c>copy</c> · <c>inplace</c>. <c>inplace</c> 는 원본을 직접 고쳐 되돌릴 방법이 백업뿐이다.</summary>
    public string? IsolationMode { get; set; } = AiTargetIsolation.Worktree;

    /// <summary><c>copy</c> 일 때 복사 상한(MB). 넘으면 실행을 거절한다.</summary>
    public int? MaxSizeMb { get; set; }

    /// <summary>이 대상에 쓸 수 있는 CLI. 쉼표로 잇는다(<c>claude,antigravity,copilot</c>).</summary>
    public string? RunnerKinds { get; set; } = "claude";

    /// <summary>push 를 허용하는 대상인가. <b>켜면 그 작업이 곧 운영 배포가 된다.</b></summary>
    public bool AllowPush { get; set; }

    public string? PushRef { get; set; } = "main";

    /// <summary>push 전에 돌릴 검사 — <c>build</c> · <c>test</c> · <c>none</c>.</summary>
    public string? GateMode { get; set; } = "build";

    /// <summary>이 대상을 집을 수 있는 실행기 이름. 비면 아무 장비나. DB 는 한 벌인데 대상 경로는 장비마다 달라서, 이 칸이 없으면 다른 장비가 집어 「폴더가 없습니다」로 실패한다.</summary>
    public string? RunnerNm { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>지금 이 대상에서 도는 실행. 대상별 동시 1건 잠금이다 — 폴더 대상은 worktree 가 없어 둘이 겹치면 서로의 파일을 덮는다.</summary>
    public long? RunningRunKey { get; set; }

    public string? Comments { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }
}

/// <summary>대상 종류.</summary>
public static class AiTargetKind
{
    public const string Repo = "repo";
    public const string Folder = "folder";

    public static bool IsValid(string? v) => v is Repo or Folder;
}

/// <summary>격리 방법.</summary>
public static class AiTargetIsolation
{
    /// <summary>git worktree. <c>repo</c> 대상의 기본값이다.</summary>
    public const string Worktree = "worktree";

    /// <summary>복사본을 떠서 거기서 돈다. <c>folder</c> 대상의 기본값.</summary>
    public const string Copy = "copy";

    /// <summary><b>격리 없음.</b> 원본을 직접 고친다 — 예외로만 연다.</summary>
    public const string Inplace = "inplace";

    public static bool IsValid(string? v) => v is Worktree or Copy or Inplace;
}
