using System.Text;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class AiTaskView
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>AI 이름을 읽을 곳. 머리띠 칩의 <c>title</c> 에만 쓴다 — 못 읽어도 화면은 그대로다.</summary>
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;

    /// <summary>「지시와 결과」 탭. 물음과 답이 이 한 판에 있다.</summary>
    private const int TabAsk = 0;

    /// <summary>「처리 파일」 탭. 이번 작업이 만들고 고친 파일들. 「무엇을 했나」 다음, 로그 앞에 둔다.</summary>
    private const int TabFiles = 1;

    /// <summary>로그 탭.</summary>
    private const int TabLog = 2;

    /// <summary>그릴 로그 줄 수의 상한. 꼬리 300줄만 그린다 — 수천 줄을 실으면 여는 것부터 느려진다.</summary>
    private const int DrawMax = 300;

    /// <summary>들고 있을 줄 수의 상한.</summary>
    private const int HoldMax = 1000;

    /// <summary>실행이 끝난 뒤 요약을 기다리는 바퀴 수(한 바퀴 3초, 약 105초).
    /// 서버의 요약 시도 시간(93초)보다 짧으면 서버가 만드는 중에 「못 만들었다」를 내건다.</summary>
    private const int SummaryWaitRounds = 35;

    /// <summary>끝난 지 이만큼 지난 건은 요약을 기다리지 않는다 — 지금 만드는 자가 없어 「만드는 중」은 거짓이다.</summary>
    private static readonly TimeSpan SummaryFresh = TimeSpan.FromMinutes(3);

    /// <summary>볼 작업. 부모가 목록에서 다시 집어 넘기므로 돌고 있는 동안 상태가 저절로 새것이 된다.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>지금 보이고 있나. 거짓이 되면 들고 있던 것을 놓고 타이머를 끈다.</summary>
    [Parameter] public bool Live { get; set; } = true;

    /// <summary>볼 건이 없을 때 적을 말. 창과 화면이 다음에 할 일이 다르다.</summary>
    [Parameter] public string MissingText { get; set; } = "그 건을 찾지 못했습니다.";

    /// <summary>다시 읽으라는 신호. 값이 달라지면 된다 — 끝난 건은 상태가 그대로라 상태 변화로는 못 잡는다.
    /// 메서드 호출 대신 값으로 받는 까닭은 새 Item 과 같은 그림에 함께 오기 위해서다.</summary>
    [Parameter] public int ReloadTick { get; set; }

    /// <summary>로그인한 사람. 「내가 올린 건인가」를 가리는 데만 쓴다 (<see cref="IsMine"/>).</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>로그인 아이디. 못 읽었으면 <c>null</c> 이다.</summary>
    private string? _me;

    /// <summary>고를 수 있는 AI 의 코드값과 이름. 칩의 title 에만 쓴다. 못 읽으면 빈 목록이고 코드값이 이름 자리에 선다.</summary>
    private IReadOnlyList<AiModelOption> _kinds = [];

    /// <summary>지금 보고 있는 건. 이것이 바뀔 때만 받아 온 것을 버린다.</summary>
    private long? _key;

    /// <summary>마지막으로 본 상태. 바뀌면 실행 이력을 다시 읽는다.</summary>
    private string? _status;

    /// <summary>마지막으로 따른 <see cref="ReloadTick"/>. 다르면 상태가 같아도 다시 읽는다.</summary>
    private int _tick;

    private int _tab;

    /// <summary>사람이 탭을 만졌나. <b>만진 뒤에는 우리가 옮기지 않는다.</b></summary>
    private bool _pinned;

    /// <summary>로그를 보고 있는 실행. 가장 최근 것이다.</summary>
    private AiTaskRunDto? _run;

    /// <summary>이 건이 몇 번 돌았나. 「지시와 결과」 탭 이름에 적는다. 이어서 지시는 새 작업이 아니라 같은 건에 실행을 하나 더 붙인다.</summary>
    private int _runCount;

    /// <summary>차수별로 가른 지시와 결과. 오래된 차수가 앞이다. 짓는 법은 <see cref="AiTaskTurns.Build"/>, 그리는 일은 <see cref="AiTaskAskTab"/> 이 한다.</summary>
    private IReadOnlyList<AiTaskTurn> _turns = [];

    /// <summary>AI 의 마지막 답. 목록에는 없고 실행 줄에 있다.</summary>
    private string? _result;

    /// <summary>그 답을 AI 가 다시 정리한 처리 요약. 서버가 적어 둔 것을 읽기만 한다 — 없을 수 있다.</summary>
    private AiRunSummary? _summary;

    /// <summary>이 지시와 함께 올라온 파일들. 바이트는 없다 — 이름과 크기뿐이고 그림은 셸 중계 주소로 그린다. 목록은 개수만 싣는다.</summary>
    private IReadOnlyList<AiTaskFileDto> _files = [];

    /// <summary>처리 파일. 이 작업이 만들고 고친 파일들을 차수별로 묶는다(AI 가 저장소에 남긴 것). <see cref="_files"/> 와 반대 방향이다.</summary>
    private IReadOnlyList<AiRunFileTurn> _changed = [];

    /// <summary>로그가 모두 몇 줄인가. 받아 둔 줄과 서버가 센 값 중 큰 쪽을 쓴다 — 받아 둔 것만 세면 건너오기 전에는 0 이다.</summary>
    private int LogCount => Math.Max(_lines.Count, _run?.LogLineCount ?? 0);

    private readonly List<AiLogLineDto> _lines = [];
    private int _lastSeq;

    /// <summary>로그를 한 번이라도 받았나. 로그 탭을 열 때 한 번만 받는다.</summary>
    private bool _logRead;

    /// <summary>돌고 있는 동안만 도는 타이머.</summary>
    private CancellationTokenSource? _poll;

    /// <summary>끝난 뒤 요약을 기다리며 돈 바퀴 수. <see cref="SummaryWaitRounds"/> 까지.</summary>
    private int _summaryWaits;

    /// <summary>기다릴 만큼 기다렸나. 이때부터 「지금 만들기」를 보여 준다 — 그 전에 누르면 서버가 만드는 것과 같은 일을 둘이 한다.</summary>
    private bool _summaryGaveUp;

    /// <summary>사람이 누른 「지금 만들기」가 도는 중인가.</summary>
    private bool _summaryBusy;

    /// <summary>답을 준 실행의 번호. 요약은 그 실행에 딸린 것이라 다시 만들 때도 이것을 준다 — 가장 최근 실행과 갈릴 수 있다.</summary>
    private long? _answeredKey;

    /// <summary>처리 요약을 펴 두었나. 처음에는 참이다. 탭 줄 위에 있어 길면 휴대폰 한 화면을 가리므로, 다른 건을 열면 다시 펴진다.</summary>
    private bool _sumOpen = true;

    /// <summary>음성으로 읽어주기(TTS) 재생 중 여부.</summary>
    private bool _speaking;
    private IJSObjectReference? _speechJs;
    private DotNetObjectReference<AiTaskView>? _dotNetRef;

    /// <summary>답은 왔는데 요약이 아직 없나. 요약은 실행이 끝난 뒤에 적히므로 조금 더 본다 — 알림을 껐어도 요약은 적힌다. 답이 없으면 기다리지 않는다.</summary>
    private bool AwaitingSummary => _summary is null && _result is { Length: > 0 };

    /// <summary>지금 서버가 만들고 있다고 볼 만한가. 포기했거나 오래된 건이면 더 볼 것이 없다.</summary>
    private bool WaitingForSummary => AwaitingSummary && !_summaryGaveUp;

    /// <summary>요약 자리에 적을 한 줄. 기다리는 중 · 지금 만드는 중 · 못 만들었음 세 상태가 다르다.</summary>
    private string SummaryWaitText => (_summaryBusy, _summaryGaveUp) switch
    {
        (true, _) => "처리 요약을 만드는 중입니다…",
        (_, true) => "처리 요약을 아직 만들지 못했습니다. "
            + "AI 가 붐비면 서버가 몇 분 뒤 다시 만들어 채웁니다.",
        _ => "처리 요약을 만드는 중입니다. 잠시 뒤에 이 자리에 나타납니다.",
    };

    /// <summary>그릴 로그 줄. 꼬리만 쓴다 — 글상자와 번역 판이 이것 하나를 봐야 옮긴 줄이 어긋나지 않는다.</summary>
    private IReadOnlyList<string> LogLines
    {
        get
        {
            if (_lines.Count <= DrawMax)
            {
                return [.. _lines.Select(l => l.Text ?? string.Empty)];
            }

            return
            [
                $"… 앞 {_lines.Count - DrawMax}줄은 「자세히 보기」에서",
                .. _lines.Skip(_lines.Count - DrawMax).Select(l => l.Text ?? string.Empty),
            ];
        }
    }

    /// <summary>처리 요약을 접었다 편다. 제목 줄이 통째로 이 단추다.</summary>
    private void ToggleSummary() => _sumOpen = !_sumOpen;

    /// <summary>얼굴 옆 지시자 이름. 아직 못 읽었으면 로그인 아이디다 — 비우면 칸 너비가 한 번 덜컥인다.</summary>
    private string Who(AiTaskDto t) => Faces.Get(t.CreId)?.Name ?? t.CreId ?? "-";

    /// <summary>머리띠 AI 칩의 전체 이름. 고르는 칸과 같은 글자를 쓴다. 못 찾으면 코드값을 그대로 적는다.</summary>
    private string KindName(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "AI 를 알 수 없음";
        }

        return _kinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Text ?? kind;
    }

    /// <summary>처음 열리는 탭. 상태가 고른다.</summary>
    private static int DefaultTab(AiTaskDto t) => t.TaskStatus switch
    {
        // 돌고 있는 동안만 로그다. 끝났으면 지시와 결과 — 물음과 답이 같은 판에 있다.
        "preparing" or "running" => TabLog,
        _ => TabAsk,
    };

    /// <summary>로그인한 사람이 올린 건인가. 남긴 말을 주고받는 경로가 이 값으로 갈린다.
    /// 아이디를 못 읽으면 거짓이다 — 관리자 경로로 돌며 읽음이 안 찍힐 뿐 판은 죽지 않는다.</summary>
    private bool IsMine(AiTaskDto t) =>
        _me is { Length: > 0 } me
        && string.Equals(me, t.CreId?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>로그인 아이디를 한 번 읽어 둔다. 첫 그림 전이다 — 그린 뒤에 바뀌면 남긴 말 부품이 관리자 경로로 한 번 읽은 뒤가 된다.</summary>
    protected override async Task OnInitializedAsync()
    {
        // AuthState 를 보기 전에 읽는다 — 이른 반환에 걸리면 칩 이름이 코드값으로 남는다. 통에 담긴 값이라 두 번째부터는 바로 돌아온다.
        _kinds = await ModelCodes.GetAsync();

        if (AuthState is null)
        {
            return;
        }

        var state = await AuthState;

        _me = state.User.Identity?.Name?.Trim();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!Live)
        {
            if (_key is not null)
            {
                Clear();
            }

            return;
        }

        if (Item is not { } t)
        {
            return;
        }

        if (_key != t.TaskKey)
        {
            Clear();

            _key = t.TaskKey;
            _status = t.TaskStatus;
            _tick = ReloadTick;
            _tab = DefaultTab(t);

            // 지시자의 얼굴을 여기서도 채운다 — 알림이나 메일로 바로 들어오면 목록 화면이 채운 것이 없다. 아는 아이디면 EnsureAsync 가 바로 돌아온다.
            await Faces.EnsureAsync([t.CreId]);

            await LoadFilesAsync(t);
            await LoadRunsAsync();

            if (_tab == TabLog)
            {
                await FetchLogsAsync();
                _logRead = true;
            }

            Follow(t);

            return;
        }

        // 같은 건이다. 사람이 새로고침을 누르면 상태를 보지 않고 다시 읽는다 — 끝난 건은 상태가 그대로라 상태만 보면 아무 일도 없다 (ReloadTick).
        // 보던 탭은 그대로 둔다 — 다시 읽는 것이지 다른 건을 여는 것이 아니다.
        if (_tick != ReloadTick)
        {
            _tick = ReloadTick;
            _status = t.TaskStatus;

            // 포기한 것을 무른다 — 서버 감시자가 뒤늦게 요약을 적을 수 있어 다시 본다.
            // 빈손이면 아래 읽기 뒤에 도로 켜진다.
            _summaryGaveUp = false;

            await LoadFilesAsync(t);
            await LoadRunsAsync();

            if (_tab == TabLog)
            {
                _logRead = true;
                await FetchLogsAsync();
            }

            Follow(t);

            return;
        }

        // **상태가 바뀔 때만** 실행 이력을 다시 읽는다 — 결과문은 그때 생긴다.
        if (_status != t.TaskStatus)
        {
            _status = t.TaskStatus;

            await LoadRunsAsync();

            if (!_pinned)
            {
                var want = DefaultTab(t);

                if (want != _tab)
                {
                    _tab = want;

                    if (want == TabLog && !_logRead)
                    {
                        await FetchLogsAsync();
                        _logRead = true;
                    }
                }
            }

            Follow(t);
        }
    }

    /// <summary>사람이 탭을 만졌다. 이 뒤로는 상태가 탭을 옮기지 않는다.</summary>
    private async Task OnTabClick(TabClickEventArgs e)
    {
        _pinned = true;

        // 로그는 **열 때 받는다.** 결과만 보고 닫는 사람에게 수백 줄을
        // 미리 실어 보낼 이유가 없다.
        if (e.TabIndex == TabLog && !_logRead)
        {
            _logRead = true;
            await FetchLogsAsync();
        }
    }

    /// <summary>「로그 보기」 한 줄이 부른다. 사람이 탭을 누른 것과 같이 친다.</summary>
    private async Task OpenLogTabAsync()
    {
        _pinned = true;
        _tab = TabLog;

        if (!_logRead)
        {
            _logRead = true;
            await FetchLogsAsync();
        }
    }

    /// <summary>함께 올라온 파일 목록. 붙은 것이 있을 때만 묻고, 따라가기는 다시 읽지 않는다 — 첨부는 보낼 때 정해진다.
    /// 못 읽어도 화면은 막지 않는다. 지시와 결과가 본문이고 첨부는 곁들이는 것이다.</summary>
    private async Task LoadFilesAsync(AiTaskDto t)
    {
        if (t.FileCount <= 0)
        {
            _files = [];
            return;
        }

        try
        {
            _files = await Api.FilesAsync(t.TaskKey);
        }
        catch (ApiException)
        {
            _files = [];
        }
    }

    private async Task<bool> LoadRunsAsync(CancellationToken ct = default)
    {
        if (_key is not { } key)
        {
            return false;
        }

        try
        {
            var runs = await Api.RunsAsync(key, ct);
            var newest = runs.FirstOrDefault();
            var changed = newest?.RunKey != _run?.RunKey;

            // 요약은 답을 준 그 실행에서 집는다 — 따로 고르면 지난 실행의 요약이 이번 답 위에 앉는다.
            var answered = runs.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.ResultText));

            _run = newest;
            _runCount = runs.Count;
            _result = answered?.ResultText;
            _answeredKey = answered?.RunKey;
            _summary = AiRunSummary.Parse(answered?.SummaryText);

            // 차수별로 가른다. 실행 줄 하나가 한 차수이고, 그때의 지시문과 답이 그 줄에 함께 있다.
            _turns = Item is { } task ? AiTaskTurns.Build(task, runs) : [];

            // 처리 파일도 차수별이다 — 가장 최근 실행 하나만 집으면 앞 차수가 만든 파일이 화면 어디에도 없다.
            _changed = AiRunFileTurns.Build(_turns);

            // 끝난 지 오래된 건은 기다리는 시늉을 하지 않는다 (SummaryFresh). 시각은 UTC 끼리 잰다.
            if (_summary is null
                && answered?.FinishedAt is { } done
                && AppTime.UtcNow - done > SummaryFresh)
            {
                _summaryGaveUp = true;
            }

            if (changed)
            {
                _lines.Clear();
                _lastSeq = 0;
                _logRead = false;
            }

            return true;
        }
        catch (ApiException)
        {
            // 한 번 못 읽었다고 화면을 깨지 않는다. 다음 바퀴에 다시 본다.
            return false;
        }
    }

    /// <summary>「지금 만들기」. 서버가 요약을 한 번 더 만들고 그 자리에 건다 — 여기서 모델을 부르면 메일과 화면의 요약이 갈린다.
    /// 못 만들어도 오류로 보여 주지 않고 「아직 못 만들었다」로 되돌린다.</summary>
    private async Task RemakeSummaryAsync()
    {
        if (_summaryBusy || _answeredKey is not { } runKey)
        {
            return;
        }

        _summaryBusy = true;
        StateHasChanged();

        try
        {
            var text = await Api.SummarizeAsync(runKey);

            if (AiRunSummary.Parse(text) is { } made)
            {
                _summary = made;
                _summaryGaveUp = false;
            }
        }
        catch (ApiException)
        {
            // 한 번 못 만들었다고 화면을 깨지 않는다. 안내는 그대로 남는다.
        }
        finally
        {
            _summaryBusy = false;
        }
    }

    /// <summary>새 줄만 받아 이어 붙인다.</summary>
    private async Task<bool> FetchLogsAsync(CancellationToken ct = default)
    {
        if (_run is not { } run)
        {
            return false;
        }

        try
        {
            var more = await Api.LogsAsync(run.RunKey, _lastSeq, ct);

            if (more.Count == 0)
            {
                return false;
            }

            _lines.AddRange(more);
            _lastSeq = more[^1].Seq;

            // 들고 있는 줄에도 상한을 둔다. 수만 줄을 쥐면 회로가 그것을 전부 실어 나른다.
            if (_lines.Count > HoldMax)
            {
                _lines.RemoveRange(0, _lines.Count - HoldMax);
            }

            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    /// <summary>돌고 있으면 따라간다. 상태는 부모가 주므로 실행 이력과 로그만 본다. 끝난 뒤 요약이 안 왔으면 조금 더 본다 (<see cref="AwaitingSummary"/>).</summary>
    private void Follow(AiTaskDto t)
    {
        if (!t.IsBusy && !WaitingForSummary)
        {
            StopPoll();
            return;
        }

        if (_poll is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _poll = cts;
        _summaryWaits = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                    if (Item is not { } now || !Live)
                    {
                        return;
                    }

                    var changed = false;

                    // 대기 중에는 실행 줄이 아직 없다. 놓치면 로그가 영영 빈 채로 남으므로 같은 자리를 다시 읽는다.
                    if (_run is null || _run.IsRunning || WaitingForSummary)
                    {
                        changed = await LoadRunsAsync(cts.Token);
                    }

                    if (_tab == TabLog)
                    {
                        _logRead = true;
                        changed |= await FetchLogsAsync(cts.Token);
                    }

                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    if (changed)
                    {
                        await InvokeAsync(StateHasChanged);
                    }

                    if (!now.IsBusy && _run?.IsRunning != true)
                    {
                        // 실행은 끝났다. 요약만 기다리며 몇 바퀴 더 본 뒤 포기한다.
                        if (!WaitingForSummary)
                        {
                            return;
                        }

                        if (++_summaryWaits > SummaryWaitRounds)
                        {
                            // 포기했다는 것을 화면에 남긴다 — 말없이 멈추면 「만드는 중」과 구분이 안 된다.
                            _summaryGaveUp = true;
                            await InvokeAsync(StateHasChanged);

                            return;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 떠났다. 정상이다.
            }
            catch
            {
                // 한 바퀴 실패했다고 화면을 깨지 않는다.
            }
        }, cts.Token);
    }

    private void StopPoll()
    {
        var cts = _poll;
        _poll = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 이미 치웠다.
        }
    }

    private void Clear()
    {
        StopPoll();
        _ = StopSpeechAsync();

        _key = null;
        _status = null;
        _run = null;
        _runCount = 0;
        _result = null;
        _summary = null;
        _changed = [];
        _turns = [];
        _answeredKey = null;
        _summaryWaits = 0;
        _summaryGaveUp = false;
        _summaryBusy = false;
        _files = [];
        _lines.Clear();
        _lastSeq = 0;
        _logRead = false;
        _pinned = false;
        _tab = TabAsk;

        // 다른 건을 열었다. 요약은 다시 펴 둔다 — 접어 둔 것은 앞 건의 사정이지 이 건의 결론이 아니다.
        _sumOpen = true;
    }

    [JSInvokable]
    public async Task OnSpeechStateChanged(bool speaking)
    {
        _speaking = speaking;
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static List<string> BuildSummarySpeechLines(AiRunSummary summary)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary.Headline))
        {
            lines.Add(summary.Headline.Trim());
        }

        if (summary.Points.Count > 0)
        {
            foreach (var point in summary.Points)
            {
                if (!string.IsNullOrWhiteSpace(point))
                {
                    lines.Add(point.Trim());
                }
            }
        }

        if (summary.Checks.Count > 0)
        {
            lines.Add("확인할 사항입니다.");
            foreach (var check in summary.Checks)
            {
                if (!string.IsNullOrWhiteSpace(check))
                {
                    lines.Add(check.Trim());
                }
            }
        }

        return lines;
    }

    private static string BuildSummarySpeechText(AiRunSummary summary) =>
        string.Join('\n', BuildSummarySpeechLines(summary));

    private async Task ToggleSpeechAsync()
    {
        if (_speaking)
        {
            await StopSpeechAsync();
        }
        else
        {
            await StartSpeechAsync();
        }
    }

    private async Task StartSpeechAsync()
    {
        if (_summary is not { } s)
        {
            Toasts.Show("읽을 요약 내용이 없습니다.", NoticeTone.Warning);
            return;
        }

        var lines = BuildSummarySpeechLines(s);
        if (lines.Count == 0)
        {
            Toasts.Show("읽을 요약 내용이 없습니다.", NoticeTone.Warning);
            return;
        }

        try
        {
            // 읽어주기는 공용 부품이다 (AI쳇 음성 모드와 같은 것). 브라우저 대응 코드는 한 벌뿐이어야 한다.
            _speechJs ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/speech.js");
            _dotNetRef ??= DotNetObjectReference.Create(this);

            var ok = await _speechJs.InvokeAsync<bool>("speak", lines, _dotNetRef);
            if (!ok)
            {
                Toasts.Show("이 브라우저는 음성 읽기(TTS)를 지원하지 않거나 사용할 수 없습니다.", NoticeTone.Warning);
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException or ObjectDisposedException)
        {
            // 화면 이동 또는 회로 끊김
        }
    }

    private async Task StopSpeechAsync()
    {
        _speaking = false;
        if (_speechJs is not null)
        {
            try
            {
                await _speechJs.InvokeVoidAsync("stop");
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or InvalidOperationException or TaskCanceledException or ObjectDisposedException)
            {
            }
        }
    }

    public void Dispose()
    {
        StopPoll();
        _ = StopSpeechAsync();
        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopPoll();
        await StopSpeechAsync();
        if (_speechJs is not null)
        {
            try
            {
                await _speechJs.DisposeAsync();
            }
            catch
            {
            }
            _speechJs = null;
        }
        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }
}
