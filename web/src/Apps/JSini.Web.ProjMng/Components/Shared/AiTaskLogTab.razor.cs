using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「로그」 탭 한 장. 부르는 쪽의 <c>DxTabs</c> 안에 끼워 쓴다
/// (<see cref="AiTaskAskTab"/> 머리말).
/// </summary>
/// <remarks>
/// <b>이 탭만 자료가 글줄이다.</b> 그래서 다른 둘과 달리 <c>AiTaskDto</c> 를
/// 아예 안 받는다 — 실행기 로그가 아닌 줄을 세울 자리가 생겨도 그대로 쓴다.
/// </remarks>
public partial class AiTaskLogTab
{
    /// <summary>
    /// 그릴 줄. <b>꼬리만 넘어온다</b> — 자르는 일은 부르는 쪽에서 한다
    /// (<c>AiTaskView.LogLines</c> · 모바일 회로가 수천 줄을 실어 나르면
    /// 여는 것부터 느려진다).
    /// </summary>
    [Parameter] public IReadOnlyList<string> Lines { get; set; } = [];

    /// <summary>
    /// 로그가 <b>모두 몇 줄인가.</b> 탭 이름에 적는다.
    /// </summary>
    /// <remarks>
    /// <b><see cref="Lines"/> 를 세지 않는다.</b> 로그는 이 탭을 펴야 받아 오므로
    /// 받아 둔 것만 세면 건너오기 전에는 늘 0 이라 「줄 수를 미리 보여 준다」는
    /// 뜻이 통째로 사라진다. 서버가 실행 줄에 적어 둔 값(<c>log_line_count</c>)은
    /// 실행 이력과 함께 이미 와 있다.
    /// </remarks>
    [Parameter] public int Count { get; set; }

    /// <summary>
    /// 옮긴 글을 담아 둘 열쇠. <b>건 번호를 넣는다</b> — 옮긴 것은 화면 밖에
    /// 남으므로(<see cref="AiLogXlatStore"/>) 열쇠가 같으면 다른 지시를 열었을
    /// 때 앞 건의 번역이 그대로 보인다.
    /// </summary>
    [Parameter] public string? StateKey { get; set; }

    /// <summary>
    /// <b>지금 이 탭이 켜져 있나.</b> 부르는 쪽이 답한다 — 이 부품은 제가
    /// 몇 번째 탭인지 모르고, 앞에 몇 장이 끼워졌는지가 화면마다 다르다.
    /// </summary>
    [Parameter] public bool Active { get; set; }

    /// <summary>
    /// 그 건이 <b>지금 도는 중인가.</b> 줄이 하나도 없을 때 적을 말이 갈린다 —
    /// 「아직 안 찍혔다」와 「남은 것이 없다」는 다른 사정이다.
    /// </summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>
    /// 탭 이름을 <b>직접 적고 싶을 때.</b> 비우면 줄 수를 붙여 스스로 짓는다.
    /// </summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// 탭에 적을 이름. <b>「줄」을 붙인다</b> — 옆의 「처리 파일 (12)」가
    /// 맨숫자라, 여기도 맨숫자면 둘이 같은 종류로 읽힌다.
    /// </summary>
    private string TabText => Text ?? (Count == 0 ? "로그" : $"로그 ({Count}줄)");
}
