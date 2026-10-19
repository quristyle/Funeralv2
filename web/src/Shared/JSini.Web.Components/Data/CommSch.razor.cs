using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class CommSch
{
    /// <summary>추가 CSS 클래스.</summary>
    [Parameter] public string? CssClass { get; set; }

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

    /// <summary>조회 단추를 새로고침 아이콘 하나로 그린다. SearchText 는 풍선 도움말로 간다. 조건이 바뀌는 즉시 다시 읽는 화면에서 쓴다 — 글자 단추가 서 있으면 눌러야 반영되는 것으로 읽힌다.</summary>
    [Parameter] public bool ReloadIcon { get; set; }

    // 단추가 새로고침 아이콘 하나뿐이면 좁은 화면에서도 조건과 한 줄에 세운다(app.css `commsch--inline`).
    private bool Inline => ReloadIcon && Actions is null && !OnReset.HasDelegate && !CanFold;

    /// <summary>조회 중인가. 켜면 조회 단추가 잠긴다. 오래 걸리는 조회(대상 DB 를 여러 번 묻는 ERD 등)에서 눌렸는지 알 수 없으면 사람은 다시 누른다.</summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>라벨을 칸 위에 얹는다. 기본은 왼쪽에 붙이는 것이다. 라벨이 길거나 조건이 예닐곱 개라 한 줄에 안 들어가는 화면에서 쓰며, 판의 칸 전부가 함께 돌아간다.</summary>
    [Parameter] public bool Stacked { get; set; }

    /// <summary>접었을 때 머리줄에 적을 지금 고른 조건(「전체 · 개발팀 · 9 월」). 접기 자체는 이 값과 무관하다 — 안 주면 머리줄에 「조회 조건」만 남는다. 빠뜨리면 CommSchFoldTests 가 잡는다. 글자는 SchSummary 로 잇는다. 값은 화면이 만든다 — 자식은 RenderFragment 라 읽을 길이 없고, 고르지 않은 것을 무엇이라 부를지는 화면마다 다르다.</summary>
    [Parameter] public string? MobileSummary { get; set; }

    /// <summary>처음에 접어 둘 것인가. 기본은 접는 것이다 — 화면을 열자마자 보고 싶은 것은 목록이다.</summary>
    [Parameter] public bool FoldedByDefault { get; set; } = true;

    /// <summary>좁은 화면에서 접을 것인가. 기본은 접는 것이다. 조건이 하나뿐이어서 접는 줄이 자리를 먹는 화면과, 고르는 순간 목록이 바뀌는 화면은 끈다 — 접혀 있으면 고르개를 쓰려고 매번 펴야 한다.</summary>
    [Parameter] public bool Foldable { get; set; } = true;

    /// <summary>접는 줄을 그리는가. Fields 가 없으면 거짓이다 — 단추만 있는 조작줄을 접으면 등록이 조건 뒤로 숨는다.</summary>
    private bool CanFold => Foldable && Fields is not null;

    /// <summary>지금 접혀 있는가. 접는 줄이 없으면 늘 거짓이다 — 그 판을 접어 두면 조건을 펼 방법이 사라진다.</summary>
    private bool Folded => CanFold && _folded;

    private bool _folded;

    /// <summary>첫 그림에서 한 번만 접는다. OnParametersSet 에서 FoldedByDefault 를 그대로 넣으면 화면이 조건을 바꿀 때마다 사람이 펴 둔 판이 다시 접힌다.</summary>
    protected override void OnInitialized() => _folded = FoldedByDefault;

    private void ToggleFold() => _folded = !_folded;

    /// <summary>조회하고 판을 접는다. 좁은 화면에서 조건을 고치고 조회를 누른 사람이 다음에 볼 것은 결과라서다. 데스크톱에서는 접힘이 CSS 에 걸리지 않아 아무 일도 일어나지 않는다.</summary>
    private Task SearchAsync()
    {
        if (CanFold)
        {
            _folded = true;
        }

        return OnSearch.InvokeAsync();
    }
}
