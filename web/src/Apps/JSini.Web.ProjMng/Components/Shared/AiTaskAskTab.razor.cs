using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「지시와 결과」 탭 한 장. 부르는 쪽의 DxTabs 안에 끼워 쓴다. 하는 일은 탭 이름 하나다 — 판의 알맹이와 펴 둔 차수는 AiTaskAskPane 이 들고 있다(DxTabPage 가 제 자식을 제가 그리지 않아 여기 두면 상태가 화면에 닿지 않는다). 자료는 받아 오지 않는다: 탭이 또 물으면 두 번 받아 어긋난다.</summary>
public partial class AiTaskAskTab
{
    /// <summary>보고 있는 건. 널이면 판만 비워 두고 탭은 선다 — 탭을 빼면 부르는 쪽이 숫자로 든 탭 번호가 그 찰나에만 틀린다.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>가장 최근 실행. 「로그 보기」 한 줄을 세울지가 이것으로 갈린다 — 한 번도 안 돈 건에는 펼 로그가 없다.</summary>
    [Parameter] public AiTaskRunDto? Run { get; set; }

    /// <summary>차수별로 가른 지시와 결과. 오래된 차수가 앞이다.</summary>
    [Parameter] public IReadOnlyList<AiTaskTurn> Turns { get; set; } = [];

    /// <summary>이 건이 몇 번 돌았나. 탭 이름에 적는다. Turns 를 세지 않는다 — 아직 안 집혀 간 차수가 끝에 붙어 한 번도 안 돈 건이 (1회)가 된다.</summary>
    [Parameter] public int RunCount { get; set; }

    /// <summary>이 지시와 함께 올라온 파일들. 첫 차수 아래에만 선다. 「처리 파일」과 반대 방향이다 — 그쪽은 AI 가 남긴 것이고 이쪽은 사람이 올린 것이다.</summary>
    [Parameter] public IReadOnlyList<AiTaskFileDto> Files { get; set; } = [];

    /// <summary>음성 단추를 어느 차수에 세울까 — 읽어 줄 요약이 딸린 실행의 번호다. 없으면 단추가 한 자리에도 안 선다. 차수마다 세우지 않는다: 요약은 한 실행에 딸린 것이라 다른 차수에서 누르면 엉뚱한 요약이 읽힌다.</summary>
    [Parameter] public long? SpeechRunKey { get; set; }

    /// <summary>음성으로 읽어주기(TTS)가 도는 중인가.</summary>
    [Parameter] public bool Speaking { get; set; }

    /// <summary>지금 이 탭이 켜져 있나. 부르는 쪽이 답한다 — 이 부품은 제가 몇 번째 탭인지 모른다(LogTab.Active 와 같은 까닭). 참이 기본이다: 안 주는 화면에서 번역이 조용히 안 도는 쪽보다 탭을 떠난 뒤 한 줄 더 도는 쪽이 낫다.</summary>
    [Parameter] public bool Active { get; set; } = true;

    /// <summary>음성 단추를 눌렀다. 읽는 일은 부르는 쪽이 한다.</summary>
    [Parameter] public EventCallback OnToggleSpeech { get; set; }

    /// <summary>로그인한 사람이 올린 건인가. 남긴 말을 어느 경로로 주고받을지가 이 값으로 갈린다.</summary>
    [Parameter] public bool Mine { get; set; }

    /// <summary>「로그 보기」를 눌렀다. 몇 번째 탭인지는 부르는 쪽이 안다 — 앞에 몇 장이 끼워졌는지가 화면마다 다르다.</summary>
    [Parameter] public EventCallback OnOpenLog { get; set; }

    /// <summary>탭 이름을 직접 적고 싶을 때. 비우면 회차를 붙여 스스로 짓는다.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>탭에 적을 이름. 두 번 이상 돈 건에만 회차를 적는다 — 한 번 돈 건의 (1회)는 아무것도 가르지 못한다. 「회」를 붙이는 것은 옆 탭이 맨숫자를 써서 셋이 같은 종류로 읽히지 않게 하려는 것이다.</summary>
    private string TabText => Text
        ?? (RunCount < 2 ? "지시와 결과" : $"지시와 결과 ({RunCount}회)");
}
