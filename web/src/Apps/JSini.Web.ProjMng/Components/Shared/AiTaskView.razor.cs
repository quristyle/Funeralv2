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

    /// <summary>
    /// AI 이름을 읽을 곳. <b>머리띠의 칩에 달 <c>title</c> 하나에만 쓴다</b> —
    /// 칩 자신은 공통코드를 몰라도 서므로(<see cref="AiRunnerMark"/>) 이것을
    /// 못 읽어도 화면은 그대로다.
    /// </summary>
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;

    /// <summary>「지시와 결과」 탭. 물음과 답이 이 한 판에 있다.</summary>
    private const int TabAsk = 0;

    /// <summary>
    /// 「처리 파일」 탭 — <b>이번 작업이 만들고 고친 파일들</b>(2026-10-12).
    /// </summary>
    /// <remarks>
    /// <b>끼운 자리가 가운데다.</b> 「무엇을 했나」(결과) 다음에 오는 물음이
    /// 「그래서 어디를 건드렸나」고, 로그는 그 둘이 못 미더울 때 펴는 것이라
    /// 맨 끝이다. 번호가 밀려 로그가 1 에서 2 가 되었다 — 이 숫자를 쓰는
    /// 자리는 여기 이 파일뿐이다(따라가기 · 로그 바닥 붙이기 · 첫 탭 고르기).
    /// </remarks>
    private const int TabFiles = 1;

    /// <summary>로그 탭.</summary>
    private const int TabLog = 2;

    /// <summary>
    /// 그릴 로그 줄 수의 상한. <b>꼬리 300줄만</b> 그린다 — 모바일 회로가
    /// 수천 줄을 실어 나르면 여는 것부터 느려진다.
    /// </summary>
    private const int DrawMax = 300;

    /// <summary>들고 있을 줄 수의 상한.</summary>
    private const int HoldMax = 1000;

    /// <summary>
    /// 실행이 끝난 뒤 <b>요약만 기다리며</b> 더 돌 바퀴 수. 한 바퀴가 3초이니
    /// 105초쯤이다 — 서버가 요약에 거는 시간(한 번에 45초, 빈손이면 3초 쉬고 한 번 더:
    /// <c>AiTasks:SummaryAttempts</c> = 93초)보다 넉넉하다.
    /// <b>서버의 상한보다 짧으면 안 된다</b> — 그러면 서버가 아직 만들고 있는데
    /// 화면이 먼저 「못 만들었다」를 내건다.
    /// </summary>
    private const int SummaryWaitRounds = 35;

    /// <summary>
    /// 끝난 지 이만큼 지난 건은 <b>기다리지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// 서버가 요약을 시도하는 것은 실행이 끝나는 그 자리뿐이고(길어야 93초),
    /// 그 뒤로는 몇 분 주기의 감시자가 줍는다(<c>AiSummaryCatchUp</c>). 그러니
    /// <b>어제 끝난 건을 열어 놓고 「만드는 중입니다」를 105초 동안 보여 주는
    /// 것은 거짓말이다</b> — 아무도 지금 만들고 있지 않다. 그런 건에는 곧바로
    /// 사정과 단추를 내건다.
    /// </remarks>
    private static readonly TimeSpan SummaryFresh = TimeSpan.FromMinutes(3);

    /// <summary>
    /// 볼 작업. <b>부모가 목록에서 다시 집어 넘긴다</b> — 그래서 돌고 있는
    /// 동안 상태와 총작업시간이 저절로 새것이 된다.
    /// </summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>
    /// 지금 보이고 있나. 창이면 열려 있는 동안, 화면이면 늘 참이다.
    /// 거짓이 되면 들고 있던 것을 놓고 타이머를 끈다.
    /// </summary>
    [Parameter] public bool Live { get; set; } = true;

    /// <summary>볼 건이 없을 때 적을 말. 창과 화면이 다음에 할 일이 다르다.</summary>
    [Parameter] public string MissingText { get; set; } = "그 건을 찾지 못했습니다.";

    /// <summary>
    /// <b>다시 읽으라는 신호.</b> 값이 **달라지기만 하면** 된다 — 부모가
    /// 새로고침을 누를 때마다 하나씩 올린다.
    /// </summary>
    /// <remarks>
    /// 아래 <see cref="OnParametersSetAsync"/> 는 <b>상태가 바뀔 때만</b> 실행
    /// 이력을 다시 읽는다. 돌고 있는 건은 그것으로 충분하지만(상태가 곧 바뀐다),
    /// <b>이미 끝난 건은 상태가 영영 그대로다</b> — 사람이 새로고침을 눌러도
    /// 부모가 받아 온 새 <see cref="Item"/> 의 상태가 같아서 속은 한 줄도 다시
    /// 읽히지 않았다. 처리 파일·결과문·로그는 상태와 따로 자라므로 그 길이
    /// 하나 있어야 한다.
    ///
    /// <para>
    /// <b>메서드를 내걸지 않고 값으로 받는 까닭</b>은 차례다. 부모가
    /// <c>@ref</c> 로 쥐고 직접 부르면 그 호출은 <b>새 <see cref="Item"/> 이
    /// 내려오기 전에</b> 닿아서, 차수(<c>_turns</c>)를 지난 건으로 짓는다.
    /// 값으로 받으면 새 건과 신호가 <b>같은 그림에 함께</b> 온다.
    /// </para>
    /// </remarks>
    [Parameter] public int ReloadTick { get; set; }

    /// <summary>
    /// 지금 로그인한 사람. <b>「내가 올린 건인가」를 가리는 데만 쓴다</b>
    /// (<see cref="IsMine"/>).
    /// </summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>로그인 아이디. 못 읽었으면 <c>null</c> 이다.</summary>
    private string? _me;

    /// <summary>
    /// 고를 수 있는 AI 의 코드값과 이름. <b>칩의 <c>title</c> 에만 쓴다</b>
    /// (<see cref="KindName"/>). 못 읽으면 빈 목록이고, 그때는 코드값이 그대로
    /// 이름 자리에 선다.
    /// </summary>
    private IReadOnlyList<AiModelOption> _kinds = [];

    /// <summary>지금 보고 있는 건. 이것이 바뀔 때만 받아 온 것을 버린다.</summary>
    private long? _key;

    /// <summary>마지막으로 본 상태. 바뀌면 실행 이력을 다시 읽는다.</summary>
    private string? _status;

    /// <summary>
    /// 마지막으로 따른 <see cref="ReloadTick"/>. 다르면 상태가 그대로여도
    /// 다시 읽는다.
    /// </summary>
    private int _tick;

    private int _tab;

    /// <summary>사람이 탭을 만졌나. <b>만진 뒤에는 우리가 옮기지 않는다.</b></summary>
    private bool _pinned;

    /// <summary>로그를 보고 있는 실행. 가장 최근 것이다.</summary>
    private AiTaskRunDto? _run;

    /// <summary>
    /// 이 건이 <b>몇 번 돌았나</b>(2026-10-12). 「지시와 결과」 탭 이름에 적는다.
    /// </summary>
    /// <remarks>
    /// 「이어서 지시」는 <b>새 작업을 만들지 않는다</b> — 같은 건에 실행이 하나
    /// 더 붙는다(<c>AiTaskService.ContinueAsync</c>). 다시 보내기도 마찬가지다.
    /// 그래서 이 판에 깔린 「지시」는 <b>마지막 회차의 지시문</b>이고, 그 앞에
    /// 몇 번이 더 있었는지는 글만 보아서는 알 수 없다 — 서버가 「지난 진행」을
    /// 본문 앞에 이어 붙여 놓아 한 덩어리로 읽힌다.
    /// <para>
    /// <b>세는 것은 실행 줄이다</b>(<c>ai_task_run</c>). 대기 중이라 아직 실행
    /// 줄이 없는 건은 0 이고, 그때는 적지 않는다.
    /// </para>
    /// </remarks>
    private int _runCount;

    /// <summary>
    /// <b>차수별로 가른 지시와 결과</b>(2026-10-15). 오래된 차수가 앞이다.
    /// </summary>
    /// <remarks>
    /// 한동안 이 탭에는 <b>지시 한 벌과 결과 한 벌</b>뿐이었다. 그런데 이어서
    /// 지시한 건에서 그 한 벌은 <b>마지막 차수의 것</b>이고(본문이 덮인다),
    /// 앞 차수에 무엇을 시켜 무슨 답을 받았는지는 서버가 지시문 앞에 덧대 놓은
    /// 요약으로만 남아 글 한복판에 묻혀 있었다 — 3차까지 간 건에서 1차의 답을
    /// 찾으려면 결과문을 처음부터 읽는 수밖에 없었다.
    /// <para>
    /// 짓는 법은 <see cref="AiTaskTurns.Build"/>. 실행 줄 하나가 한 차수고,
    /// 보냈는데 아직 안 집혀 간 차수가 끝에 하나 더 붙을 수 있다.
    /// </para>
    /// <para>
    /// <b>그리는 일은 <see cref="AiTaskAskTab"/> 이 한다</b>(2026-10-15) —
    /// 여기는 받아 와 넘겨 주기만 한다.
    /// </para>
    /// </remarks>
    private IReadOnlyList<AiTaskTurn> _turns = [];

    /// <summary>AI 의 마지막 답. 목록에는 없고 실행 줄에 있다.</summary>
    private string? _result;

    /// <summary>
    /// 그 답을 AI 가 다시 정리한 「처리 요약」. <b>서버가 적어 둔 것을 읽기만
    /// 한다</b> — 없을 수 있고, 없으면 요약 칸을 그리지 않는다.
    /// </summary>
    private AiRunSummary? _summary;

    /// <summary>
    /// 이 지시와 함께 올라온 파일들. <b>바이트는 없다</b> — 이름과 크기뿐이고,
    /// 그림은 셸 중계 주소로 그린다(<c>FileDownload.AiTaskUrlFor</c>).
    /// </summary>
    /// <remarks>
    /// <b>목록이 실어 주는 것은 개수뿐이다</b>(<c>AiTaskDto.FileCount</c>) —
    /// 카드 열 장을 그리는 조회가 이름까지 끌고 올 이유가 없다. 이름은 여는
    /// 자리에서 한 번 묻는다.
    /// </remarks>
    private IReadOnlyList<AiTaskFileDto> _files = [];

    /// <summary>
    /// 「처리 파일」 — <b>이번 작업이 만들고 고친 파일들.</b> 셋째 탭의 알맹이다.
    /// </summary>
    /// <remarks>
    /// <b><see cref="_files"/> 와 반대 방향이다.</b> 그쪽은 사람이 지시에 붙여
    /// 올려 보낸 것(들어간 것)이고 이쪽은 AI 가 저장소에 남긴 것(나온 것)이다.
    /// 둘이 한 판에 있으므로 이름을 섞지 않는다. 읽는 법은 <c>AiRunFiles.Parse</c>.
    /// <para>
    /// <b>그리는 일은 <see cref="AiTaskFilesTab"/> 이 한다</b>(2026-10-15) —
    /// 빈 탭에 적을 말도, 아래 한 줄도 그 부품이 짓는다.
    /// </para>
    /// </remarks>
    private IReadOnlyList<AiRunFile> _changed = [];

    /// <summary>
    /// 로그가 <b>모두 몇 줄인가.</b> 탭 이름에 적는다.
    /// </summary>
    /// <remarks>
    /// <b>받아 둔 줄만 세지 않는다.</b> 로그는 그 탭을 펴야 받아 오므로
    /// (<see cref="_logRead"/>), 받아 둔 것만 세면 건너오기 전에는 늘 0 이라
    /// 「줄 수를 미리 보여 준다」는 뜻이 통째로 사라진다. 서버가 실행 줄에
    /// 적어 둔 값(<c>log_line_count</c>)은 실행 이력과 함께 이미 와 있다.
    /// <para>
    /// 둘 중 <b>큰 쪽</b>을 쓴다. 들고 있는 줄에는 상한이 있어
    /// (<see cref="HoldMax"/>) 수천 줄짜리는 받아 둔 쪽이 늘 작고, 반대로
    /// 돌고 있는 건은 방금 받은 줄이 서버가 센 값보다 앞설 수 있다.
    /// </para>
    /// </remarks>
    private int LogCount => Math.Max(_lines.Count, _run?.LogLineCount ?? 0);

    private readonly List<AiLogLineDto> _lines = [];
    private int _lastSeq;

    /// <summary>로그를 한 번이라도 받았나. 로그 탭을 열 때 한 번만 받는다.</summary>
    private bool _logRead;

    /// <summary>돌고 있는 동안만 도는 타이머.</summary>
    private CancellationTokenSource? _poll;

    /// <summary>끝난 뒤 요약을 기다리며 돈 바퀴 수. <see cref="SummaryWaitRounds"/> 까지.</summary>
    private int _summaryWaits;

    /// <summary>
    /// 기다릴 만큼 기다렸나. <b>이때부터 「지금 만들기」를 보여 준다.</b>
    /// </summary>
    /// <remarks>
    /// 그 전에는 안 보여 준다 — 서버가 만드는 중일 수 있고, 그때 누르면
    /// 같은 일을 둘이 하게 된다(서버가 이미 적힌 것을 되읽어 주므로 값이
    /// 갈리지는 않지만, 모델 호출 한 번이 헛되이 는다).
    /// </remarks>
    private bool _summaryGaveUp;

    /// <summary>사람이 누른 「지금 만들기」가 도는 중인가.</summary>
    private bool _summaryBusy;

    /// <summary>
    /// <b>답을 준 실행</b>의 번호. 요약은 그 실행에 딸린 것이라 다시 만들 때도
    /// 이것을 준다 — 가장 최근 실행(<see cref="_run"/>)이 아니다. 둘이 갈리는
    /// 경우가 실제로 있다(다시 돌렸는데 이번에는 아무 말도 안 남긴 때).
    /// </summary>
    private long? _answeredKey;

    /// <summary>
    /// 처리 요약을 <b>펴 두었나</b>(2026-10-12). 처음에는 참이다.
    /// </summary>
    /// <remarks>
    /// 요약은 탭 줄 <b>위</b>에 있어 어느 탭을 펴든 그만큼을 늘 머리에서
    /// 먹는다. 한 일 여덟 줄에 확인할 것 넷이 붙은 건에서는 그것이 휴대폰
    /// 한 화면을 통째로 가려, 로그를 쫓는 사람이 이미 읽은 글을 매번 지나쳐
    /// 내려가야 했다.
    /// <para>
    /// <b>기억해 두지 않는다.</b> 다른 건을 열면 다시 펴진다(<see cref="Clear"/>) —
    /// 접어 둔 것은 그 건의 요약이 길었다는 사정이지 「나는 요약을 안 본다」가
    /// 아니고, 이 판을 여는 사람의 열에 아홉은 그 결론을 보러 온다.
    /// </para>
    /// </remarks>
    private bool _sumOpen = true;

    /// <summary>음성으로 읽어주기(TTS) 재생 중 여부.</summary>
    private bool _speaking;
    private IJSObjectReference? _speechJs;
    private DotNetObjectReference<AiTaskView>? _dotNetRef;

    /// <summary>
    /// <b>답은 왔는데 요약이 아직 없나.</b> 그러면 실행이 끝났어도 조금 더 본다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 요약은 <b>실행이 끝난 뒤에</b> 적힌다. 서버는 완료 보고를 기다리게 하지
    /// 않으려고 요약·알림을 떼어 뒤에서 돌리고(<c>AiRunService</c> 의
    /// <c>_ = SummarizeThenNotifyAsync(runKey)</c>), 그 안에서 AI 를 한 번 더
    /// 부른다 — 한 번에 길면 30초고, 빈손으로 오면 한 번 더 부른다.
    /// </para>
    /// <para>
    /// <b>알림을 껐어도 적힌다.</b> 요약을 만드는 일은 메일·앱푸시에서 떼어
    /// 놓았다(<c>AiRunSummaryWriter</c>) — 「메일로 받기」를 끄면 요약도 없던
    /// 때가 있었고, 그때 이 칸은 알림을 끈 사람에게만 영영 비어 있었다.
    /// </para>
    /// <para>
    /// 그사이 이 화면은 「끝났다」를 보고 따라가기를 멈춘다. 그러면 <b>끝나는
    /// 것을 지켜보고 있던 사람에게만 요약이 영영 안 보인다</b> — 나중에 다시
    /// 연 사람에게는 보이는데. 그래서 여기서 몇 바퀴를 더 돈다.
    /// </para>
    /// <para>
    /// <b>답이 없는 건은 기다리지 않는다.</b> 요약할 원문이 없으면 서버도
    /// 만들지 않으므로, 기다려 봐야 75초를 헛도는 일이다.
    /// </para>
    /// </remarks>
    private bool AwaitingSummary => _summary is null && _result is { Length: > 0 };

    /// <summary>
    /// <b>지금 서버가 만들고 있다고 볼 만한가.</b> 여기가 참인 동안만 따라간다 —
    /// 포기했거나 애초에 오래된 건이면 더 봐야 나올 것이 없다.
    /// </summary>
    private bool WaitingForSummary => AwaitingSummary && !_summaryGaveUp;

    /// <summary>
    /// 요약 자리에 적을 한 줄. <b>세 가지 사정이 다 다르다</b> — 기다리는 중 ·
    /// 지금 만드는 중 · 못 만들었음.
    /// </summary>
    private string SummaryWaitText => (_summaryBusy, _summaryGaveUp) switch
    {
        (true, _) => "처리 요약을 만드는 중입니다…",
        (_, true) => "처리 요약을 아직 만들지 못했습니다. "
            + "AI 가 붐비면 서버가 몇 분 뒤 다시 만들어 채웁니다.",
        _ => "처리 요약을 만드는 중입니다. 잠시 뒤에 이 자리에 나타납니다.",
    };

    /// <summary>
    /// 그릴 로그 줄. <b>꼬리만</b> 쓴다 — 위는 잘렸다는 사실을 첫 줄에 적는다.
    /// </summary>
    /// <remarks>
    /// <b>글상자와 비교 편집기가 이것 하나를 본다.</b> 따로 세면 둘을 오가는
    /// 사람이 다른 줄을 보게 되고, 번역 쪽은 그 어긋남이 <b>짝이 밀린 것</b>
    /// 으로 나타나 원인이 안 보인다.
    /// </remarks>
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

    /// <summary>
    /// 얼굴 옆에 적을 지시자 이름. <b>아직 못 읽었으면 로그인 아이디</b>다 —
    /// 이름이 올 때까지 그 자리를 비우면 머리띠의 칸 너비가 한 번 덜컥인다.
    /// </summary>
    private string Who(AiTaskDto t) => Faces.Get(t.CreId)?.Name ?? t.CreId ?? "-";

    /// <summary>
    /// 머리띠의 AI 칩에 달 전체 이름. <b>고르는 칸과 같은 글자</b>를 쓴다 —
    /// 공통코드(<c>AI_MODEL</c>)에서 읽은 <see cref="_kinds"/> 에서 집는다.
    /// </summary>
    /// <remarks>
    /// 못 찾으면 코드값을 그대로 적는다. 목록을 못 받았을 때도(그쪽은 실패해도
    /// 화면을 막지 않는다) 칩이 <b>이름 없는 색 조각</b>으로 남지 않는다 —
    /// 칩에는 글자가 한 자뿐이라 그 <c>title</c> 이 유일한 이름이다.
    /// </remarks>
    private string KindName(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "AI 를 알 수 없음";
        }

        return _kinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Text ?? kind;
    }

    /// <summary>
    /// 처음 열리는 탭. <b>상태가 고른다</b> — 위 머리말 참고.
    /// </summary>
    private static int DefaultTab(AiTaskDto t) => t.TaskStatus switch
    {
        // 돌고 있는 동안만 로그다. 끝났으면 「지시와 결과」 — 물음과 답이
        // 같은 판에 있으므로 대기 중인 건과 끝난 건을 가를 이유가 없다.
        "preparing" or "running" => TabLog,
        _ => TabAsk,
    };

    /// <summary>
    /// <b>로그인한 사람이 올린 건인가.</b> 남긴 말을 어느 경로로 주고받을지가
    /// 이 값으로 갈린다(위 머리말).
    /// </summary>
    /// <remarks>
    /// <b>아이디를 못 읽었으면 거짓이다.</b> 그때는 관리자 경로로 도는데,
    /// 그 길은 누구에게나 열려 있어 판이 죽지 않는다 — 읽음이 안 찍히는
    /// 것뿐이고 그것은 다음에 제 화면에서 펴면 찍힌다.
    /// </remarks>
    private bool IsMine(AiTaskDto t) =>
        _me is { Length: > 0 } me
        && string.Equals(me, t.CreId?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 로그인 아이디를 한 번 읽어 둔다. <b>첫 그림 전이다</b> — 그린 뒤에
    /// 바뀌면 남긴 말 부품이 이미 관리자 경로로 한 번 읽은 뒤가 된다
    /// (그 부품은 번호가 그대로면 다시 읽지 않는다).
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // **AuthState 를 보기 전에 읽는다.** 아래 이른 반환에 걸리면 칩의
        // 이름이 영영 코드값으로 남는다. 통에 담긴 값이라 두 번째부터는
        // 그 자리에서 돌아온다(`AiModelCodes`).
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

            // 지시자의 얼굴. **여기서도 물어야 한다** — 창으로 열렸을 때는
            // 「빠른 지시」 화면이 목록을 받으며 이미 채워 두었지만, 알림이나
            // 메일을 눌러 `AiTaskViewPage` 로 바로 들어오면 그 화면은 없다.
            // 이미 아는 아이디면 `EnsureAsync` 가 그 자리에서 돌아온다.
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

        // 같은 건이다. **사람이 새로고침을 눌렀으면** 상태를 보지 않고 다시
        // 읽는다 — 끝난 건은 상태가 영영 그대로라, 상태만 보고 있으면 눌러도
        // 아무 일이 일어나지 않는다(`ReloadTick` 머리말).
        //
        // **보던 탭은 그대로 둔다.** 다시 읽는 것이지 다른 건을 여는 것이
        // 아니므로, 로그를 보다 누른 사람을 지시 탭으로 데려가지 않는다.
        if (_tick != ReloadTick)
        {
            _tick = ReloadTick;
            _status = t.TaskStatus;

            // **포기한 것을 무른다.** 요약을 기다리다 포기한 뒤에 서버의 감시자가
            // 뒤늦게 적어 넣는 일이 있고(`AiSummaryCatchUp`), 그것을 보려고 누르는
            // 것이 이 단추다. 아래 읽기가 여전히 빈손이면 그 자리에서 도로 켜진다.
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

    /// <summary>
    /// 실행 이력을 읽는다. <b>결과문이 여기 있다</b> — 목록에는 없다.
    /// </summary>
    /// <remarks>
    /// 실행이 새로 생겼으면(다시 돌렸다) 들고 있던 로그를 버린다. 안 버리면
    /// <b>지난 실행의 줄 위에 이번 줄이 이어 붙어</b> 한 실행처럼 읽힌다.
    /// </remarks>
    /// <summary>
    /// 함께 올라온 파일 목록. <b>붙은 것이 있을 때만 묻는다.</b>
    /// </summary>
    /// <remarks>
    /// <b>따라가기(타이머)가 다시 읽지 않는다.</b> 첨부는 보낼 때 정해지고
    /// 그 뒤로 바뀌지 않는다 — 몇 초마다 다시 읽으면 왕복만 는다.
    /// <para>
    /// 못 읽어도 화면을 막지 않는다. 지시와 결과가 이 화면의 본문이고,
    /// 첨부는 곁들이는 것이다.
    /// </para>
    /// </remarks>
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

            // **요약은 답을 준 그 실행에서 집는다.** 따로 고르면 이번 실행의
            // 답 위에 **지난 실행의 요약**이 앉는 날이 온다 — 그 둘이 다른
            // 말을 하면 어느 쪽이 맞는지 화면만 보고는 알 수 없다.
            var answered = runs.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.ResultText));

            _run = newest;
            _runCount = runs.Count;
            _result = answered?.ResultText;
            _answeredKey = answered?.RunKey;
            _summary = AiRunSummary.Parse(answered?.SummaryText);

            // **처리 파일은 가장 최근 실행의 것이다.** 요약과 달리 「답을 준
            // 실행」에서 집지 않는다 — 아무 말 없이 파일만 고치고 끝나는 건이
            // 실제로 있고(`result_text` 가 빈 건), 그때 답을 준 지난 실행의
            // 파일 목록을 올려 두면 **이번에 고친 것이 아닌 파일이** 이번 건의
            // 처리 파일로 보인다.
            _changed = AiRunFiles.Parse(newest?.ChangedFiles, newest?.DiffStat);

            // **차수별로 가른다**(2026-10-15). 실행 줄 하나가 한 차수고, 그때
            // 간 지시문과 그 답이 그 줄에 함께 있다 — 짓는 법은 `AiTaskTurns`.
            _turns = Item is { } task ? AiTaskTurns.Build(task, runs) : [];

            // **끝난 지 오래된 건은 기다리는 시늉을 하지 않는다** — 위
            // <see cref="SummaryFresh"/> 참고. 서버가 준 시각은 UTC 라
            // UTC 끼리 잰다(<c>AiTaskWhen</c> 머리말 · <c>docs/utc-time.md</c>).
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

    /// <summary>
    /// <b>「지금 만들기」.</b> 서버에 요약을 한 번 더 시키고, 나온 것을 그 자리에 건다.
    /// </summary>
    /// <remarks>
    /// <b>만드는 것은 서버다.</b> 여기서 모델을 부르면 메일에 적힌 요약과 화면의
    /// 요약이 갈린다 — 서버가 만들어 <c>summary_text</c> 에 적고, 우리는 적힌 것을
    /// 받아 읽기만 한다. 그래서 여러 번 눌러도 요약은 하나다.
    ///
    /// <para>
    /// <b>못 만들어도 오류로 보여 주지 않는다.</b> AI 가 붐벼서 못 만든 것이고
    /// 잠시 뒤면 되는 일이라, 원래 적혀 있던 「아직 못 만들었다」로 되돌린다 —
    /// 서버의 감시자가 그사이에도 계속 다시 본다.
    /// </para>
    /// </remarks>
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

            // 들고 있는 줄에도 상한을 둔다. 수만 줄을 쥐고 있으면 회로가
            // 그것을 전부 실어 나른다.
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

    /// <summary>
    /// 돌고 있으면 따라간다. <b>상태는 부모가 주므로 여기서는 실행 이력과
    /// 로그만 본다.</b>
    /// </summary>
    /// <remarks>
    /// <b>끝난 뒤에도 요약이 안 왔으면 조금 더 본다</b> — 아래
    /// <see cref="AwaitingSummary"/> 참고.
    /// </remarks>
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

                    // 대기 중에는 실행 줄이 아직 없다. 생기는 순간을 놓치면
                    // **로그가 영영 빈 채로 남는다.** 끝났는데 요약이 아직
                    // 안 적혔을 때도 같은 자리를 다시 읽는다.
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
                        // 실행은 끝났다. **요약만 기다린다** — 서버가 그것을
                        // 뒤늦게 적으므로(위 `AwaitingSummary`) 몇 바퀴 더 본
                        // 뒤 포기한다.
                        if (!WaitingForSummary)
                        {
                            return;
                        }

                        if (++_summaryWaits > SummaryWaitRounds)
                        {
                            // **포기했다는 것을 화면에 남긴다.** 예전에는 말없이
                            // 멈춰서, 보는 사람에게는 「만드는 중」이 영영 이어지는
                            // 것과 구분이 안 됐다. 여기서부터 「지금 만들기」가 뜬다.
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

        // 다른 건을 열었다. **요약은 다시 펴 둔다** — 접어 둔 것은 앞 건의
        // 요약이 길었다는 사정이고, 새 건을 열자마자 결론이 접혀 있으면
        // 이 판을 여는 까닭이 통째로 숨는다.
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
            // 읽어주기는 **공용 부품이다.** AI쳇의 음성 모드가 같은 것을 쓴다 —
            // 브라우저 버그 대응(청크 나누기·GC 참조 유지·pause 와치독)이
            // 한 벌뿐이어야 한다.
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
