using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「지시와 결과」 탭 한 장. <b>부르는 쪽의 <c>DxTabs</c> 안에 끼워 쓴다</b> —
/// 셋이든 넷이든 다섯이든 골라 담는다(머리말).
/// </summary>
/// <remarks>
/// <b>자료는 받아 오지 않는다.</b> 실행 이력도 로그도 이 판을 가진 화면이 읽어
/// 넘겨 준다 — 탭이 제 손으로 또 물으면 같은 값을 두 번 받아 오면서 둘이
/// 어긋날 자리만 생긴다(<see cref="AiTaskView"/> 머리말).
/// <para>
/// 여기 남는 상태는 <b>그리는 모습</b>뿐이다 — 어느 차수를 펴 두었나
/// (<see cref="_openTurns"/>), 「앞에 덧댄 글」을 폈나(<see cref="_openLeads"/>).
/// </para>
/// </remarks>
public partial class AiTaskAskTab
{
    /// <summary>보고 있는 건. <b>널이면 판만 비워 두고 탭은 선다.</b></summary>
    /// <remarks>
    /// 탭을 통째로 빼지 않는 까닭은 <b>탭 번호가 밀리기 때문</b>이다. 부르는
    /// 쪽은 「로그는 몇 번째」를 상수로 들고 있는데(<c>AiTaskView.TabLog</c>),
    /// 자료가 없는 찰나에만 탭이 하나 줄면 그 숫자가 그 찰나에만 틀린다.
    /// </remarks>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>
    /// 가장 최근 실행. <b>「로그 보기」 한 줄을 세울지가 이것으로 갈린다</b> —
    /// 한 번도 안 돈 건에는 펼 로그가 없다.
    /// </summary>
    [Parameter] public AiTaskRunDto? Run { get; set; }

    /// <summary>
    /// <b>차수별로 가른 지시와 결과.</b> 오래된 차수가 앞이다
    /// (<see cref="AiTaskTurns.Build"/>).
    /// </summary>
    [Parameter] public IReadOnlyList<AiTaskTurn> Turns { get; set; } = [];

    /// <summary>
    /// 이 건이 <b>몇 번 돌았나.</b> 탭 이름에 적는다 — 두 번 이상 돈 건에만.
    /// </summary>
    /// <remarks>
    /// <b><see cref="Turns"/> 를 세지 않는다.</b> 보냈는데 아직 안 집혀 간
    /// 차수가 끝에 하나 더 붙을 수 있어(<see cref="AiTaskTurns.Build"/>)
    /// 그것까지 「돈 횟수」로 세면 한 번도 안 돈 건이 「(1회)」가 된다.
    /// </remarks>
    [Parameter] public int RunCount { get; set; }

    /// <summary>
    /// 이 지시와 <b>함께 올라온 파일들.</b> 첫 차수 아래에만 선다.
    /// </summary>
    /// <remarks>
    /// 「처리 파일」 탭(<see cref="AiTaskFilesTab"/>)과 <b>반대 방향이다</b> —
    /// 그쪽은 AI 가 저장소에 남긴 것이고 이쪽은 사람이 붙여 올린 것이다.
    /// </remarks>
    [Parameter] public IReadOnlyList<AiTaskFileDto> Files { get; set; } = [];

    /// <summary>
    /// 음성 단추를 <b>어느 차수에 세울까</b> — 읽어 줄 요약이 딸린 실행의
    /// 번호다. 읽어 줄 것이 없으면 널이고, 그러면 단추가 한 자리에도 안 선다.
    /// </summary>
    /// <remarks>
    /// <b>차수마다 세우지 않는다.</b> 읽어 주는 것은 「처리 요약」이고 그 요약은
    /// 한 실행에 딸린 것이라(<c>AiTaskView._answeredKey</c>), 차수마다 세우면
    /// 1차 자리에서 눌러도 3차의 요약이 읽힌다.
    /// <para>
    /// <b>요약 자체는 안 받는다.</b> 그리는 자리가 탭 줄 <b>위</b>라
    /// (<see cref="AiTaskView"/>) 이 탭이 요약으로 하는 일은 「단추를 세울까」
    /// 하나뿐이다 — 번호 한 칸이면 그 말이 다 된다. 요약을 담은 타입은
    /// 모듈 안에만 있어(<c>internal</c>) 파라미터로 내걸 수도 없다.
    /// </para>
    /// </remarks>
    [Parameter] public long? SpeechRunKey { get; set; }

    /// <summary>음성으로 읽어주기(TTS)가 도는 중인가.</summary>
    [Parameter] public bool Speaking { get; set; }

    /// <summary>
    /// <b>지금 이 탭이 켜져 있나.</b> 부르는 쪽이 답한다 — 이 부품은 제가 몇
    /// 번째 탭인지 모르고, 앞에 몇 장이 끼워졌는지가 화면마다 다르다
    /// (<see cref="AiTaskLogTab.Active"/> 와 같은 까닭·같은 값).
    /// </summary>
    /// <remarks>
    /// 쓰는 자리는 결과문의 번역 판 하나다 — 거짓이 되면 돌고 있는 줄을
    /// 거둔다(<see cref="LogTranslateView.Active"/>). <b>참이 기본이다</b> —
    /// 안 주는 화면에서 번역이 조용히 안 도는 쪽보다, 탭을 떠난 뒤에도 한 줄이
    /// 더 도는 쪽이 낫다.
    /// </remarks>
    [Parameter] public bool Active { get; set; } = true;

    /// <summary>음성 단추를 눌렀다. <b>읽는 일은 부르는 쪽이 한다.</b></summary>
    [Parameter] public EventCallback OnToggleSpeech { get; set; }

    /// <summary>
    /// <b>로그인한 사람이 올린 건인가.</b> 남긴 말을 어느 경로로 주고받을지가
    /// 이 값으로 갈린다(<see cref="AiTaskNotes"/>).
    /// </summary>
    [Parameter] public bool Mine { get; set; }

    /// <summary>
    /// 「로그 보기」를 눌렀다. <b>몇 번째 탭인지는 부르는 쪽이 안다</b> —
    /// 앞에 몇 장이 끼워졌는지가 화면마다 다르다.
    /// </summary>
    [Parameter] public EventCallback OnOpenLog { get; set; }

    /// <summary>
    /// 탭 이름을 <b>직접 적고 싶을 때.</b> 비우면 회차를 붙여 스스로 짓는다.
    /// </summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// 탭에 적을 이름. <b>두 번 이상 돈 건에만 회차를 적는다</b>(2026-10-12).
    /// </summary>
    /// <remarks>
    /// <b>한 번 돈 건에 「(1회)」는 적지 않는다.</b> 그것이 보통이라 아무것도
    /// 가르지 못하면서 탭 이름만 길어진다 — 이 숫자가 하는 말은 「이 글은 한
    /// 번에 나온 것이 아니다」 하나뿐이고, 그 말이 참인 건에만 선다.
    /// <para>
    /// <b>「회」를 붙인다.</b> 옆의 둘이 이미 맨숫자를 쓰고 있어(처리 파일 N ·
    /// 로그 N줄) 여기도 맨숫자면 셋이 같은 종류로 읽힌다 — 이것만 개수가
    /// 아니라 <b>횟수</b>다.
    /// </para>
    /// </remarks>
    private string TabText => Text
        ?? (RunCount < 2 ? "지시와 결과" : $"지시와 결과 ({RunCount}회)");

    /// <summary>
    /// 펴고 접은 모습을 어느 건의 것으로 들고 있나. <b>건이 바뀌면 턴다.</b>
    /// </summary>
    private long? _key;

    /// <summary>
    /// 펴 둔 차수. <b>널이면 「마지막 차수만」</b>이고, 사람이 한 번이라도
    /// 접거나 펴면 그 순간의 모습이 여기 담긴다.
    /// </summary>
    /// <remarks>
    /// 처음부터 집합을 들지 않는 까닭은 <b>차수가 도중에 늘기 때문</b>이다.
    /// 지켜보는 동안 이어서 지시가 한 번 더 돌면 차수가 하나 늘어나는데,
    /// 그때 펴야 할 것은 새로 생긴 마지막 차수다 — 번호를 담아 두면 그 자리가
    /// 접힌 채로 서고 사람은 방금 보낸 것이 어디 갔는지 찾게 된다.
    /// <para>
    /// 사람이 손을 댄 뒤로는 우리가 옮기지 않는다. 탭을 안 옮기는 것
    /// (<c>AiTaskView._pinned</c>)과 같은 까닭이다 — <b>읽던 자리를 잃는다.</b>
    /// </para>
    /// </remarks>
    private HashSet<int>? _openTurns;

    /// <summary>
    /// 「앞에 덧댄 글」을 펴 둔 차수. <b>처음에는 모두 접혀 있다.</b>
    /// </summary>
    /// <remarks>
    /// 그 글은 서버가 지은 것이지 사람이 적은 것이 아니다(<see cref="AiTaskAsk"/>).
    /// 펴 두면 차수마다 앞 차수의 요약이 되풀이되어, <b>이번에 무엇을 더
    /// 시켰는지가 그 되풀이에 묻힌다.</b>
    /// </remarks>
    private readonly HashSet<int> _openLeads = [];

    /// <summary>
    /// 그 차수의 결과문을 <b>줄로 갈라 둔 것.</b> 번역 판은 줄 목록을 받는다
    /// (<see cref="LogTranslateView.Lines"/>).
    /// </summary>
    /// <remarks>
    /// <b>차수마다 담아 둔다.</b> 그리기마다 새로 가르면 글자가 한 자도
    /// 안 바뀐 날에도 새 목록이 넘어가, 판이 그때마다 들여오기를 한 바퀴
    /// 돈다(<c>LogTranslateView.SyncSource</c>).
    /// </remarks>
    private readonly Dictionary<int, (string Text, IReadOnlyList<string> Lines)> _resultLines = [];

    /// <summary>
    /// 다른 건이 넘어왔으면 <b>펴고 접은 모습을 턴다.</b>
    /// </summary>
    /// <remarks>
    /// 안 털면 앞 건에서 3차를 펴 둔 사람이 다음 건에서도 3차만 펴진 판을
    /// 본다 — 차수 번호는 건마다 1 부터 다시 매겨져서, <b>남의 건의 손자국</b>이
    /// 제 손으로 편 것처럼 보인다.
    /// <para>
    /// 널로 돌리지 않고 <see cref="Turns"/> 가 비는 것까지 기다리지도 않는다 —
    /// 번호 하나로 가른다. 같은 건을 다시 읽는 동안(따라가기)에는 번호가
    /// 그대로라 손자국이 살아남는다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 그 차수의 결과문을 줄로 가른다. <b>빈 줄도 그대로 둔다</b> — 문단
    /// 사이가 붙으면 글이 통째로 한 덩어리로 읽힌다.
    /// </summary>
    private IReadOnlyList<string> ResultLines(AiTaskTurn turn, string result)
    {
        if (_resultLines.TryGetValue(turn.No, out var had)
            && string.Equals(had.Text, result, StringComparison.Ordinal))
        {
            return had.Lines;
        }

        // 줄 끝이 섞여 들어온다(실행기가 찍는 것 · 사람이 적어 넣은 것).
        // 안 맞추면 `\r` 한 자가 줄 끝에 남아 옮길 때 함께 올라간다.
        IReadOnlyList<string> lines = result
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        _resultLines[turn.No] = (result, lines);

        return lines;
    }

    /// <summary>
    /// 옮긴 결과문을 <b>어느 이름으로 들고 있을지</b>(<see cref="AiLogXlatStore"/>).
    /// </summary>
    /// <remarks>
    /// <b>건 번호와 차수를 함께 넣는다.</b> 건만 넣으면 1차를 옮긴 글이 2차
    /// 자리에 그대로 서고, 차수만 넣으면 다른 건의 1차에 앞 건의 번역이 선다.
    /// <para>
    /// <b>로그의 열쇠와 겹치지 않아야 한다</b> — 그쪽은 <c>ai-task:{번호}</c>
    /// 이고(<see cref="AiTaskView"/>) 여기에는 <c>:result:</c> 한 토막이 더
    /// 붙는다. 겹치면 줄 수가 달라 번역이 통째로 버려진다
    /// (<c>LogTranslateView.SyncSource</c>).
    /// </para>
    /// </remarks>
    private string ResultStateKey(AiTaskTurn turn) => $"ai-task:{_key}:result:{turn.No}";

    /// <summary>그 차수가 펴져 있나. <b>손대기 전에는 마지막 차수만</b>이다.</summary>
    private bool IsTurnOpen(AiTaskTurn turn)
        => _openTurns is { } open ? open.Contains(turn.No) : turn.IsLast;

    /// <summary>차수를 접었다 편다. 머리줄이 통째로 이 단추다.</summary>
    /// <remarks>
    /// <b>첫 손길에 지금 모습을 그대로 담는다</b> — 그래야 3차를 펴는 손길이
    /// 보고 있던 1차를 덮어 접지 않는다.
    /// </remarks>
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

    /// <summary>
    /// 그 차수의 답 아래 한 줄 — 가지 · 몇 줄이 바뀌었나 · 올린 자리.
    /// </summary>
    /// <remarks>
    /// <b>올린 자리는 마지막 차수에만 적는다.</b> 그 값은 작업에 하나뿐이라
    /// (<c>ai_task.pushed_commit</c>) 차수마다 적으면 1차의 답 아래에
    /// <b>3차가 올린 커밋</b>이 앉는다.
    /// </remarks>
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
