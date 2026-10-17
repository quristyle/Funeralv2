using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「작성」 탭 한 장 — <b>앞으로 보낼 지시를 적는 자리.</b>
/// 부르는 쪽의 <c>DxTabs</c> 안에 끼워 쓴다(<see cref="AiTaskAskTab"/> 머리말).
/// </summary>
/// <remarks>
/// <b>보는 탭 셋과 반대 방향이다.</b> 저쪽 셋(<see cref="AiTaskAskTab"/> ·
/// <see cref="AiTaskFilesTab"/> · <see cref="AiTaskLogTab"/>)은 이미 간 것을
/// 보여 주고, 이쪽은 아직 안 간 것을 적는다.
/// <para>
/// <b>자료도 저장도 부르는 쪽 것이다.</b> 대상 목록 · AI 목록 · 한도 배지는
/// 받아서 그리기만 하고, 단추는 눌렸다는 사실만 올려 보낸다 — 탭이 제 손으로
/// 또 물으면 같은 값을 두 번 받아 오면서 둘이 어긋날 자리만 생긴다.
/// <b>다만 고친 값은 곧장 <see cref="Item"/> 에 들어간다</b>(머리말).
/// </para>
/// </remarks>
public partial class AiTaskWriteTab
{
    /// <summary>고르는 칸 한 줄. 공통코드에 없는 값들이라 부품이 들고 받는다.</summary>
    public sealed record PickOption(string Value, string Text);

    /// <summary>
    /// 적고 있는 건. <b>널이면 판만 비워 두고 탭은 선다</b> — 까닭은
    /// <see cref="AiTaskAskTab.Item"/> 과 같다(탭 번호가 밀린다).
    /// </summary>
    /// <remarks>
    /// <b>이 객체가 곧 입력칸이다.</b> 칸들이 <c>@bind</c> 로 여기에 바로 쓰므로,
    /// 부르는 쪽은 저가 넘긴 그 객체를 그대로 저장하면 된다. 표의 행과 갈라 둔
    /// 복사본을 넘기는 것이 보통이다(<c>AiTaskList._edit</c>) — 저장하지 않고
    /// 다른 건으로 옮기면 목록에 반쯤 고친 값이 남기 때문이다.
    /// </remarks>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>
    /// 지금 고칠 수 있나. <b>도는 중에는 거짓이다</b> — 고쳐도 실행기는 이미
    /// 옛 본문을 들고 떠났으므로, 고치게 두면 화면의 글과 도는 글이 달라진다.
    /// </summary>
    [Parameter] public bool CanEdit { get; set; }

    /// <summary>저장·요청이 오가는 중. <b>단추를 두 번 누르지 못하게 한다.</b></summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>고를 수 있는 대상(저장소). <b>쓰는 것만 넘긴다.</b></summary>
    [Parameter] public IReadOnlyList<AiTargetDto> Targets { get; set; } = [];

    /// <summary>
    /// 고를 수 있는 AI. <b>고른 대상이 허용한 것만</b> 걸러서 넘긴다 —
    /// 거르는 일은 대상의 <c>RunnerKinds</c> 를 아는 부르는 쪽 몫이다.
    /// </summary>
    [Parameter] public IReadOnlyList<AiModelOption> Runners { get; set; } = [];

    /// <summary>
    /// 그 AI 의 한도 줄들을 집어 주는 길. <b>안 주면 배지를 안 그린다.</b>
    /// 고르개(<see cref="AiRunnerPick"/>)에 그대로 넘긴다.
    /// </summary>
    /// <remarks>
    /// <b>이름만 보여 주면 다 쓴 CLI 를 고르게 된다.</b> 그리고 그것은 실행기가
    /// 집어가 실패로 끝난 뒤에야 드러난다 — 이 화면의 일은 몇십 분씩 걸려
    /// 그때 사람은 이미 자리를 뜬 뒤다.
    /// </remarks>
    [Parameter] public Func<string?, IReadOnlyCollection<AiUsageSnapshot>>? RunnerUsage { get; set; }

    /// <summary>한도를 한 번이라도 받아 봤나. 거짓이면 「확인 중」으로 적힌다.</summary>
    [Parameter] public bool UsageSeen { get; set; } = true;

    /// <summary>고른 대상이 「저장소에 올리기」를 허용하는가.</summary>
    [Parameter] public bool AllowPush { get; set; }

    /// <summary>그 체크칸에 적을 글. 대상이 안 받으면 그 사실을 적는다.</summary>
    [Parameter] public string PushLabel { get; set; } = "저장소에 올리기 (운영 배포)";

    /// <summary>
    /// 대상을 바꿨다. <b>AI 와 「올리기」를 그 대상에 맞추는 일은 부르는 쪽이
    /// 한다</b> — 못 쓰는 AI 가 남으면 칸은 비어 보이는데 값은 옛것이 실린다.
    /// </summary>
    [Parameter] public EventCallback<long?> TargetChanged { get; set; }

    /// <summary>AI 도우미가 고른 제목(<see cref="AiTaskWriteAssist"/>).</summary>
    [Parameter] public EventCallback<string> TitleChosen { get; set; }

    /// <summary>AI 도우미가 간추린 조각(마크다운). 본문 맨 위에 얹는다.</summary>
    [Parameter] public EventCallback<string> SummaryReady { get; set; }

    /// <summary>「요청」을 누를 수 있나.</summary>
    [Parameter] public bool CanRequest { get; set; }

    /// <summary>「취소」를 누를 수 있나.</summary>
    [Parameter] public bool CanCancel { get; set; }

    /// <summary>「이어서 지시」를 누를 수 있나.</summary>
    [Parameter] public bool CanContinue { get; set; }

    /// <summary>
    /// 적어 둔 임시본이 있나. <b>있을 때만 「임시본 버리기」가 선다</b> —
    /// 버릴 것이 없는데 단추만 서 있으면 눌러 본 사람이 고장으로 읽는다.
    /// </summary>
    [Parameter] public bool HasDraft { get; set; }

    /// <summary>「저장」을 눌렀다.</summary>
    [Parameter] public EventCallback OnSave { get; set; }

    /// <summary>「요청」을 눌렀다.</summary>
    [Parameter] public EventCallback OnRequest { get; set; }

    /// <summary>「취소」를 눌렀다.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    /// <summary>「이어서 지시」를 눌렀다.</summary>
    [Parameter] public EventCallback OnContinue { get; set; }

    /// <summary>「임시본 버리기」를 눌렀다.</summary>
    [Parameter] public EventCallback OnDiscardDraft { get; set; }

    /// <summary>「삭제」를 눌렀다.</summary>
    [Parameter] public EventCallback OnDelete { get; set; }

    /// <summary>탭 이름을 <b>직접 적고 싶을 때.</b> 비우면 「작성」이다.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>고른 것이 없을 때 그 자리에 적을 말.</summary>
    [Parameter] public string EmptyText { get; set; } = "왼쪽 목록에서 작업을 고르거나 새 작업을 만드십시오.";

    /// <summary>
    /// 탭에 적을 이름. <b>옆의 셋과 달리 숫자를 안 붙인다</b> — 저 셋은
    /// 쌓인 것을 보여 주는 자리라 개수가 건너올지를 정하지만, 이 탭에는
    /// 셀 것이 없다.
    /// </summary>
    private string TabText => Text ?? "작성";

    /// <summary>
    /// 「보낼 때」 — 끝난 뒤 알림을 어느 때에 보낼까.
    /// </summary>
    /// <remarks>
    /// <b>부품이 들고 있다.</b> 공통코드에 없는 값이고 쓰는 자리가 이 칸
    /// 하나뿐이라, 받아 오게 두면 쓰려는 화면마다 같은 세 줄을 다시 적게 된다.
    /// </remarks>
    private static readonly PickOption[] NotifyWhens =
    [
        new("always", "끝나면 언제나"), new("on_success", "성공했을 때만"),
        new("on_failure", "실패했을 때만"),
    ];
}
