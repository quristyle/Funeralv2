using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「지시와 결과」 탭 한 장. <b>부르는 쪽의 <c>DxTabs</c> 안에 끼워 쓴다</b> —
/// 셋이든 넷이든 다섯이든 골라 담는다(머리말).
/// </summary>
/// <remarks>
/// <b>이 부품이 하는 일은 탭 이름 하나다.</b> 판의 알맹이와 「어느 차수를 펴
/// 두었나」는 <see cref="AiTaskAskPane"/> 가 들고 있다 — <c>DxTabPage</c> 가
/// 제 자식 내용을 제가 그리지 않아서, 여기 두면 상태가 화면에 닿지 않는다
/// (2026-10-16 · 그 부품 머리말).
/// <para>
/// <b>자료는 받아 오지 않는다.</b> 실행 이력도 로그도 이 판을 가진 화면이 읽어
/// 넘겨 준다 — 탭이 제 손으로 또 물으면 같은 값을 두 번 받아 오면서 둘이
/// 어긋날 자리만 생긴다(<see cref="AiTaskView"/> 머리말).
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
}
