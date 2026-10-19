using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「로그」 탭 한 장. 부르는 쪽의 DxTabs 안에 끼워 쓴다. 이 탭만 자료가 글줄이라 AiTaskDto 를 받지 않는다.</summary>
public partial class AiTaskLogTab
{
    /// <summary>그릴 줄. 꼬리만 넘어온다 — 자르는 일은 부르는 쪽이 한다(수천 줄을 실어 나르면 여는 것부터 느려진다).</summary>
    [Parameter] public IReadOnlyList<string> Lines { get; set; } = [];

    /// <summary>로그가 모두 몇 줄인가. 탭 이름에 적는다 — Lines 를 세지 않는다: 로그는 이 탭을 펴야 받아 오므로 받아 둔 것만 세면 늘 0 이다. 실행 이력의 log_line_count 를 쓴다.</summary>
    [Parameter] public int Count { get; set; }

    /// <summary>옮긴 글을 담아 둘 열쇠. 건 번호를 넣는다 — 옮긴 것은 화면 밖에 남으므로 열쇠가 같으면 다른 지시를 열었을 때 앞 건의 번역이 보인다.</summary>
    [Parameter] public string? StateKey { get; set; }

    /// <summary>지금 이 탭이 켜져 있나. 부르는 쪽이 답한다 — 이 부품은 제가 몇 번째 탭인지 모른다.</summary>
    [Parameter] public bool Active { get; set; }

    /// <summary>그 건이 지금 도는 중인가. 줄이 하나도 없을 때 적을 말이 갈린다 — 아직 안 찍혔다와 남은 것이 없다는 다르다.</summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>탭 이름을 직접 적고 싶을 때. 비우면 줄 수를 붙여 스스로 짓는다.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>탭에 적을 이름. 「줄」을 붙인다 — 옆의 「처리 파일 (12)」가 맨숫자라 여기도 맨숫자면 같은 종류로 읽힌다.</summary>
    private string TabText => Text ?? (Count == 0 ? "로그" : $"로그 ({Count}줄)");
}
