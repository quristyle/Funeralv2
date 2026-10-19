using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Menu;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「지시와 결과」 탭의 알맹이. AiTaskAskTab 이 DxTabPage 안에 이것 하나를 끼운다 — 탭 부품 안에 상태를 두면 접개를 눌러도 화면이 안 바뀐다(DxTabPage 는 자식을 제가 그리지 않는다).
/// 여기 남는 상태는 그리는 모습뿐이다(펴 둔 차수 · 덧댄 글). 자료는 받지 않는다.</summary>
public partial class AiTaskAskPane
{
    /// <summary>보고 있는 건. 널이면 판만 비운다 — 탭은 그대로 서야 탭 번호가 밀리지 않는다.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>가장 최근 실행. 「로그 보기」 한 줄을 세울지가 이것으로 갈린다 — 한 번도 안 돈 건에는 펼 로그가 없다.</summary>
    [Parameter] public AiTaskRunDto? Run { get; set; }

    /// <summary>차수별로 가른 지시와 결과. 오래된 차수가 앞이다.</summary>
    [Parameter] public IReadOnlyList<AiTaskTurn> Turns { get; set; } = [];

    /// <summary>이 지시와 함께 올라온 파일들. 첫 차수 아래에만 선다. 처리 파일(AiTaskFilesTab)과 반대 방향이다 — 이쪽은 사람이 붙여 올린 것.</summary>
    [Parameter] public IReadOnlyList<AiTaskFileDto> Files { get; set; } = [];

    /// <summary>음성 단추를 세울 차수. 읽어 줄 요약이 딸린 실행의 번호이고, 없으면 널이라 단추가 한 자리에도 안 선다. 차수마다 세우면 1차에서 눌러도 3차 요약이 읽힌다. 번호 한 칸이면 되므로 요약 자체는 받지 않는다.</summary>
    [Parameter] public long? SpeechRunKey { get; set; }

    /// <summary>음성으로 읽어주기(TTS)가 도는 중인가.</summary>
    [Parameter] public bool Speaking { get; set; }

    /// <summary>지금 이 탭이 켜져 있나. 부르는 쪽이 답한다 — 이 부품은 몇 번째 탭인지 모른다. 거짓이면 번역 판이 돌던 줄을 거둔다. 기본은 참이다(안 주는 화면에서 번역이 조용히 멈추는 쪽보다 한 줄 더 도는 쪽이 낫다).</summary>
    [Parameter] public bool Active { get; set; } = true;

    /// <summary>음성 단추를 눌렀다. <b>읽는 일은 부르는 쪽이 한다.</b></summary>
    [Parameter] public EventCallback OnToggleSpeech { get; set; }

    /// <summary>로그인한 사람이 올린 건인가. 남긴 말을 주고받는 경로가 이 값으로 갈린다.</summary>
    [Parameter] public bool Mine { get; set; }

    /// <summary>「로그 보기」를 눌렀다. 몇 번째 탭인지는 부르는 쪽이 안다.</summary>
    [Parameter] public EventCallback OnOpenLog { get; set; }

    /// <summary>펴고 접은 모습을 어느 건의 것으로 들고 있나. 건이 바뀌면 턴다.</summary>
    [Inject] private RouteInventory Routes { get; set; } = default!;

    /// <summary>지시문을 글자와 화면 링크로 가른다. 실려 있는 화면의 주소만 링크가 된다.</summary>
    private IReadOnlyList<AiTaskAskPiece> AskPieces(string text) =>
        AiTaskAskLinks.Split(text, path => AiTaskAskLinks.Matches(path, Routes.Paths));

    private long? _key;

    /// <summary>펴 둔 차수. 널이면 마지막 차수만이고, 사람이 한 번이라도 접거나 펴면 그 모습이 담긴다. 번호로 담지 않는 까닭은 이어서 지시로 차수가 늘 때 새 마지막 차수를 펴야 하기 때문이다. 손을 댄 뒤에는 우리가 옮기지 않는다.</summary>
    private HashSet<int>? _openTurns;

    /// <summary>「앞에 덧댄 글」을 펴 둔 차수. 처음에는 모두 접혀 있다 — 펴 두면 앞 차수의 요약이 되풀이되어 이번에 더 시킨 것이 묻힌다.</summary>
    private readonly HashSet<int> _openLeads = [];

    /// <summary>그 차수의 결과문을 줄로 갈라 둔 것. 차수마다 담는다 — 그리기마다 새로 가르면 글자가 그대로인 날에도 새 목록이 넘어가 판이 들여오기를 한 바퀴 돈다.</summary>
    private readonly Dictionary<int, (string Text, IReadOnlyList<string> Lines)> _resultLines = [];

    /// <summary>다른 건이 넘어왔으면 펴고 접은 모습을 턴다. 안 털면 앞 건에서 편 차수가 다음 건의 같은 번호로 남아 남의 손자국이 제 손으로 편 것처럼 보인다. 번호 하나로 가른다 — 같은 건을 다시 읽는 동안에는 번호가 그대로라 손자국이 살아남는다.</summary>
    protected override void OnParametersSet()
    {
        if (_key == Item?.TaskKey)
        {
            return;
        }

        _key = Item?.TaskKey;
        _openTurns = null;
        _openLeads.Clear();
        _resultLines.Clear();
    }

    /// <summary>그 차수의 결과문을 줄로 가른다. 빈 줄도 그대로 둔다 — 문단 사이가 붙으면 글이 한 덩어리로 읽힌다.</summary>
    private IReadOnlyList<string> ResultLines(AiTaskTurn turn, string result)
    {
        if (_resultLines.TryGetValue(turn.No, out var had)
            && string.Equals(had.Text, result, StringComparison.Ordinal))
        {
            return had.Lines;
        }

        // 줄 끝이 섞여 들어온다(실행기 · 사람). 맞추지 않으면 캐리지 리턴 한 자가 줄 끝에 남아 옮길 때 함께 올라간다.
        IReadOnlyList<string> lines = result
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        _resultLines[turn.No] = (result, lines);

        return lines;
    }

    /// <summary>옮긴 결과문을 들고 있을 이름(AiLogXlatStore). 건 번호와 차수를 함께 넣는다 — 건만 넣으면 1차 번역이 2차 자리에 서고, 차수만 넣으면 다른 건에 앞 건의 번역이 선다. 로그 열쇠(ai-task:번호)와 겹치지 않아야 한다.</summary>
    private string ResultStateKey(AiTaskTurn turn) => $"ai-task:{_key}:result:{turn.No}";

    /// <summary>그 차수가 펴져 있나. <b>손대기 전에는 마지막 차수만</b>이다.</summary>
    private bool IsTurnOpen(AiTaskTurn turn)
        => _openTurns is { } open ? open.Contains(turn.No) : turn.IsLast;

    /// <summary>차수를 접었다 편다. 첫 손길에 지금 모습을 그대로 담는다 — 그래야 3차를 펴는 손길이 보고 있던 1차를 덮어 접지 않는다.</summary>
    private void ToggleTurn(AiTaskTurn turn)
    {
        _openTurns ??= [.. Turns.Where(IsTurnOpen).Select(t => t.No)];

        if (!_openTurns.Remove(turn.No))
        {
            _openTurns.Add(turn.No);
        }
    }

    /// <summary>그 차수의 「앞에 덧댄 글」이 펴져 있나.</summary>
    private bool IsLeadOpen(AiTaskTurn turn) => _openLeads.Contains(turn.No);

    /// <summary>「앞에 덧댄 글」을 접었다 편다.</summary>
    private void ToggleLead(AiTaskTurn turn)
    {
        if (!_openLeads.Remove(turn.No))
        {
            _openLeads.Add(turn.No);
        }
    }

    /// <summary>펴고 접을 자리의 이름표. 낭독기에게 머리줄과 몸통을 잇는다.</summary>
    private string TurnPaneId(AiTaskTurn turn) => $"pm-peek-turn-{_key}-{turn.No}";

    /// <summary>그 차수의 답 아래 한 줄 — 가지 · 몇 줄이 바뀌었나 · 올린 자리. 올린 자리는 마지막 차수에만 적는다 — 그 값은 작업에 하나뿐이라 차수마다 적으면 1차의 답 아래에 3차가 올린 커밋이 앉는다.</summary>
    private string? TurnHint(AiTaskTurn turn)
    {
        if (turn.Run is not { } run)
        {
            return null;
        }

        var parts = new List<string>();

        if (run.GitBranch is { Length: > 0 } branch)
        {
            parts.Add(branch);
        }

        if (run.DiffStat is { Length: > 0 } diff)
        {
            parts.Add(diff.Split('\n')[^1].Trim());
        }

        if (turn.IsLast && Item?.PushedCommit is { Length: > 0 } commit)
        {
            parts.Add($"올림 {commit[..Math.Min(7, commit.Length)]}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
