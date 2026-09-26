using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiTargetList
{
    [Inject] private AiTargetClient Api { get; set; } = default!;
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;

    public sealed record PickOption(string Value, string Text);

    private static readonly PickOption[] Kinds =
    [
        new("repo", "저장소"), new("folder", "폴더"),
    ];

    private static readonly PickOption[] Isolations =
    [
        new("worktree", "worktree"), new("copy", "복사본"), new("inplace", "원본 직접"),
    ];

    /// <summary>
    /// 쓸 수 있는 AI. <b>값은 실행기가 아는 이름이어야 한다</b> —
    /// 화면에 보이는 글자와 저장되는 실행기 값이 다르다.
    /// </summary>
    private IReadOnlyList<PickOption> RunnerOptions = [];

    /// <summary>
    /// 고를 수 있는 장비 이름. <b>목록에 없는 이름도 적을 수 있다</b>
    /// (<c>AllowUserInput</c>) — 장비가 늘 때마다 화면을 고치지 않으려는 것이다.
    /// </summary>
    private static readonly string[] Runners = ["jsini-prod", "quri-dev"];

    private static readonly PickOption[] Gates =
    [
        new("build", "빌드"), new("test", "빌드+테스트"), new("none", "안 함"),
    ];

    private IReadOnlyList<AiTargetDto> _rows = [];
    private IReadOnlyList<string> _roots = [];

    private string Hint => $"{_rows.Count(t => t.IsEnabled)} / {_rows.Count}건 사용";

    /// <summary>
    /// 허용 뿌리는 <b>서버가 알려 준 값</b>이다. 화면에 박으면 설정을 바꿨을 때
    /// 안내만 옛말이 된다.
    /// </summary>
    private string RootsNotice => _roots.Count == 0
        ? "경로는 서버가 허용한 뿌리 아래여야 합니다. 등록은 관리자만 하고, 작업 화면은 여기 등록된 것에서 고르기만 합니다."
        : $"경로는 다음 아래여야 합니다 — {string.Join(" · ", _roots)}. 작업 화면은 여기 등록된 것에서 고르기만 합니다.";

    protected override async Task OnInitializedAsync()
    {
        var models = await ModelCodes.GetAsync();
        RunnerOptions = [.. models.Select(x => new PickOption(x.Value, x.Text))];
        await LoadRootsAsync();
        await SearchAsync();
    }

    /// <summary>
    /// 안내에 쓸 값이라 <b>못 받아도 화면을 막지 않는다</b> — 뿌리를 모르면
    /// 문구만 일반적인 말로 바뀐다.
    /// </summary>
    private async Task LoadRootsAsync()
    {
        try
        {
            _roots = await Api.AllowedRootsAsync();
        }
        catch (ApiException)
        {
            _roots = [];
        }
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync();
        return _rows.Count;
    }, "등록된 대상이 없습니다.", "대상을 읽지 못했습니다");

    /// <summary>
    /// 새 대상의 기본값. <b>「올리기」는 꺼 둔 채로 시작한다</b> —
    /// 켜는 것은 사람이 한 번 더 생각하고 할 일이다.
    /// </summary>
    private void FillNew(AiTargetDto t)
    {
        t.TargetKind = "repo";
        t.IsolationMode = "worktree";
        t.DefaultRef = "main";
        t.PushRef = "main";
        t.RunnerKinds = RunnerOptions.FirstOrDefault()?.Value ?? "claude";
        t.GateMode = "build";
        t.AllowPush = false;
        t.IsEnabled = true;
    }

    private async Task SaveAsync((AiTargetDto Item, bool IsNew) e)
    {
        // 폴더에 worktree 는 있을 수 없다. 서버도 바로잡지만 여기서 먼저
        // 맞춰 두면 저장 뒤 값이 바뀌어 돌아오는 것을 안 본다.
        if (e.Item.TargetKind == "folder" && e.Item.IsolationMode == "worktree")
        {
            e.Item.IsolationMode = "copy";
        }

        if (e.IsNew)
        {
            await Api.CreateAsync(e.Item);
        }
        else
        {
            await Api.UpdateAsync(e.Item);
        }
    }

    private Task DeleteAsync(AiTargetDto t) => Api.DeleteAsync(t.TargetKey);

    // ── 「쓸 수 있는 AI」 칸 ────────────────────────────────
    //
    // DB 에는 쉼표로 이어 둔다. 표 하나를 더 두지 않는
    // 이유는 값이 둘뿐이고 실행기가 그 문자열을 그대로 읽기 때문이다.
    // 화면에서만 목록과 문자열을 오간다.

    /// <summary>고른 값들. 모르는 값이 섞여 있어도 버리지 않는다 —
    /// 설정으로 CLI 가 늘어난 뒤에 이 화면을 안 고쳤을 수 있다.</summary>
    private static IEnumerable<string> RunnerValues(string? kinds) =>
        (kinds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string JoinRunners(IEnumerable<string> values) => string.Join(',', values);

    /// <summary>이 CLI 가 켜져 있나.</summary>
    private static bool HasRunner(AiTargetDto t, string kind) =>
        RunnerValues(t.RunnerKinds).Contains(kind);

    /// <summary>
    /// 켜고 끈다. <b>모르는 값은 건드리지 않는다</b> — 설정으로 CLI 가 늘어난
    /// 뒤 이 화면을 안 고쳤다면, 여기서 목록을 다시 쓰는 순간 그 값이 사라진다.
    /// </summary>
    private static void ToggleRunner(AiTargetDto t, string kind, bool on)
    {
        var picked = RunnerValues(t.RunnerKinds).ToList();

        if (on)
        {
            if (!picked.Contains(kind))
            {
                picked.Add(kind);
            }
        }
        else
        {
            picked.Remove(kind);
        }

        t.RunnerKinds = string.Join(',', picked);
    }
}
