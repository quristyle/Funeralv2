using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「작성」 탭 한 장 — 앞으로 보낼 지시를 적는다. 보는 탭 셋과 반대 방향이고, 자료·저장은 부르는 쪽 것이다.</summary>
public partial class AiTaskWriteTab
{
    /// <summary>고르는 칸 한 줄. 공통코드에 없는 값들이라 부품이 들고 받는다.</summary>
    public sealed record PickOption(string Value, string Text);

    /// <summary>적고 있는 건. 널이면 판만 비우고 탭은 선다(탭 번호가 밀리지 않게). 이 객체가 곧 입력칸이라 @bind 가 여기에 바로 쓴다.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>고칠 수 있나. 도는 중에는 거짓이다 — 실행기는 옛 본문을 들고 떠났으므로 고치면 화면과 도는 글이 달라진다.</summary>
    [Parameter] public bool CanEdit { get; set; }

    /// <summary>저장·요청이 오가는 중. <b>단추를 두 번 누르지 못하게 한다.</b></summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>고를 수 있는 대상(저장소). <b>쓰는 것만 넘긴다.</b></summary>
    [Parameter] public IReadOnlyList<AiTargetDto> Targets { get; set; } = [];

    /// <summary>고른 대상이 허용한 AI 만 걸러 넘긴다(걸러는 부르는 쪽 몫).</summary>
    [Parameter] public IReadOnlyList<AiModelOption> Runners { get; set; } = [];

    /// <summary>AI 의 한도 줄들을 집어 주는 길. 안 주면 배지를 안 그린다. 이름만 보이면 다 쓴 CLI 를 고르게 되는데, 그 실패는 몇십 분 뒤에야 드러난다.</summary>
    [Parameter] public Func<string?, IReadOnlyCollection<AiUsageSnapshot>>? RunnerUsage { get; set; }

    /// <summary>한도를 한 번이라도 받아 봤나. 거짓이면 「확인 중」으로 적힌다.</summary>
    [Parameter] public bool UsageSeen { get; set; } = true;

    /// <summary>고른 대상이 「저장소에 올리기」를 허용하는가.</summary>
    [Parameter] public bool AllowPush { get; set; }

    /// <summary>그 체크칸에 적을 글. 대상이 안 받으면 그 사실을 적는다.</summary>
    [Parameter] public string PushLabel { get; set; } = "저장소에 올리기 (운영 배포)";

    /// <summary>대상을 바꿨다. AI 와 올리기를 그 대상에 맞추는 일은 부르는 쪽이 한다.</summary>
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

    /// <summary>임시본이 있을 때만 「임시본 버리기」가 선다 — 버릴 것이 없는데 단추만 서면 고장으로 읽힌다.</summary>
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

    /// <summary>탭 이름. 옆의 셋과 달리 숫자를 안 붙인다 — 셀 것이 없다.</summary>
    private string TabText => Text ?? "작성";

    /// <summary>끝난 뒤 알림을 보낼 때. 부품이 들고 있다 — 쓰는 자리가 이 칸뿐이다.</summary>
    private static readonly PickOption[] NotifyWhens =
    [
        new("always", "끝나면 언제나"), new("on_success", "성공했을 때만"),
        new("on_failure", "실패했을 때만"),
    ];
}
