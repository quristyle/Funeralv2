using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class CommSch
{
    /// <summary>조회 조건들. <c>CommSchItem</c> 을 넣는다.</summary>
    [Parameter] public RenderFragment? Fields { get; set; }

    /// <summary>
    /// 이 화면만의 단추(등록·일괄처리 따위). 오른쪽 묶음의 <b>맨 앞</b>에 선다.
    /// </summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>조회. 주지 않으면 조회 단추가 생기지 않는다.</summary>
    [Parameter] public EventCallback OnSearch { get; set; }

    /// <summary>조건을 비우고 다시 읽는다. 주지 않으면 초기화 단추가 생기지 않는다.</summary>
    [Parameter] public EventCallback OnReset { get; set; }

    /// <summary>조회 단추의 글자. 「검색」으로 부르는 화면이 있어 열어 둔다.</summary>
    [Parameter] public string SearchText { get; set; } = "조회";

    /// <summary>
    /// 조회 중인가. 켜면 <b>조회 단추가 잠긴다.</b>
    ///
    /// <para>
    /// 오래 걸리는 조회가 있는 화면(대상 DB 를 네 번 물어보는 ERD 같은)에서,
    /// 눌린 것인지 알 수 없으면 사람은 다시 누른다. 표가 있는 화면은 표가
    /// 스스로 「불러오는 중」을 보여 주지만 그렇지 않은 화면도 있다.
    /// </para>
    /// </summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>
    /// 라벨을 칸 <b>위</b>에 얹는다. 기본은 왼쪽에 붙이는 것이다.
    ///
    /// <para>
    /// 라벨이 길거나 조건이 예닐곱 개라 한 줄에 안 들어가는 화면에서 쓴다.
    /// 이 판의 칸 <b>전부</b>가 함께 돌아간다.
    /// </para>
    /// </summary>
    [Parameter] public bool Stacked { get; set; }

    /// <summary>
    /// 접었을 때 머리줄에 적을 <b>지금 고른 조건</b>(「전체 · 개발팀 · 9 월」).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>접기 자체는 이 값과 무관하다</b> — 조건이 있는 판은 좁은 화면에서
    /// 모두 접힌다. 안 주면 머리줄에 「조회 조건」만 남아, 지금 무엇으로
    /// 걸러 본 목록인지 펴 보아야 알 수 있다. <b>빠뜨리면 테스트가 잡는다</b>
    /// (<c>CommSchFoldTests</c>).
    /// </para>
    /// <para>
    /// 글자는 <see cref="SchSummary"/> 로 잇는다 — 가운뎃점으로 잇고, 빈 조각은
    /// 버리고, 안 고른 칸은 「전체」라 부른다. 화면마다 손으로 이으면 갈라진다.
    /// </para>
    /// <para>
    /// 값을 <b>화면이 만든다.</b> 여기서 <c>Fields</c> 를 훑어 만들 수는
    /// 없다 — 자식은 <c>RenderFragment</c> 라 그 안의 값을 읽을 길이 없고,
    /// 읽을 수 있다 해도 「전체」처럼 <b>고르지 않은 것을 무엇이라 부를지</b>
    /// 는 화면마다 다르다.
    /// </para>
    /// </remarks>
    [Parameter] public string? MobileSummary { get; set; }

    /// <summary>
    /// 처음에 접어 둘 것인가. 기본은 접는 것이다.
    ///
    /// <para>
    /// 화면을 열자마자 보고 싶은 것은 <b>목록</b>이지 조건이 아니다. 조건을
    /// 손대야 하는 사람은 한 번 누르면 된다.
    /// </para>
    /// </summary>
    [Parameter] public bool FoldedByDefault { get; set; } = true;

    /// <summary>
    /// 좁은 화면에서 접을 것인가. 기본은 접는 것이다.
    ///
    /// <para>
    /// 조건이 하나뿐이어서 접는 줄이 오히려 자리를 먹는 화면에서 끈다.
    /// <b>조건이 즉시 반영되는 화면</b>(고르는 순간 목록이 바뀌는)도 여기에
    /// 든다 — 접혀 있으면 그 고르개를 쓰려고 매번 펴야 한다.
    /// </para>
    /// </summary>
    [Parameter] public bool Foldable { get; set; } = true;

    /// <summary>
    /// 접는 줄을 그리는가. <see cref="Fields"/> 가 없으면 거짓이다 —
    /// 단추만 있는 조작줄을 접으면 「등록」이 「조회 조건」 뒤로 숨는다.
    /// </summary>
    private bool CanFold => Foldable && Fields is not null;

    /// <summary>
    /// 지금 접혀 있는가. 접는 줄이 없으면 늘 거짓이다 — 그 판을 접어 두면
    /// 조건을 펼 방법이 사라진다.
    /// </summary>
    private bool Folded => CanFold && _folded;

    private bool _folded;

    /// <summary>
    /// 첫 그림에서 한 번만 접는다.
    /// </summary>
    /// <remarks>
    /// <c>OnParametersSet</c> 에서 <see cref="FoldedByDefault"/> 를 그대로
    /// 넣으면, 화면이 조건을 바꿀 때마다 <b>사람이 펴 둔 판이 다시 접힌다.</b>
    /// </remarks>
    protected override void OnInitialized() => _folded = FoldedByDefault;

    private void ToggleFold() => _folded = !_folded;

    /// <summary>
    /// 조회하고 <b>판을 접는다</b>.
    /// </summary>
    /// <remarks>
    /// 좁은 화면에서 조건을 고치고 조회를 누른 사람이 다음에 볼 것은 결과다.
    /// 판이 펴진 채로 남으면 그 결과가 화면 밖에 있다. 데스크톱에서는 접힘이
    /// CSS 에 걸리지 않으므로 아무 일도 일어나지 않는다.
    /// </remarks>
    private Task SearchAsync()
    {
        if (CanFold)
        {
            _folded = true;
        }

        return OnSearch.InvokeAsync();
    }
}
