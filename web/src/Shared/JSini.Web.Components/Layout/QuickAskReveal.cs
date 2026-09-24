using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 「빠른 지시」 서랍을 <b>바깥에서</b> 여닫는 손잡이.
/// </summary>
/// <remarks>
/// <para>
/// 단추는 헤더 도구 묶음(<c>HeaderTools</c>) 안에 있고 판은
/// <c>QuickAskDrawer</c> 다. 둘은 형제도 부모 자식도 아니라 파라미터로 이을
/// 수 없다 — 테마 서랍(<c>ThemeDrawer</c>)·메뉴 펼치기(<c>MenuReveal</c>)와
/// 같은 이유로 사이에 통을 하나 둔다.
/// </para>
///
/// <para>
/// [여닫힘을 판이 아니라 여기가 들고 있다]
/// </para>
///
/// <para>
/// 단추가 <b>토글</b>이라 그렇다. 판이 상태를 들고 있으면 단추는 지금 열려
/// 있는지를 몰라서 「열기」밖에 못 한다 — 한 번 연 서랍을 같은 단추로 닫을
/// 수 없게 된다. 테마 서랍은 항목을 고르면 닫히는 물건이라 그쪽은 열기·닫기
/// 둘로 충분했다.
/// </para>
///
/// <para>scoped 다. 한 사람이 열었다고 모두의 서랍이 열리면 안 된다.</para>
/// </remarks>
/// <param name="services">
/// 알맹이가 등록됐는지 보려고 받는다. <see cref="QuickAskContent"/> 는
/// <b>없을 수 있어서</b> 생성자로 못 받는다 — 그 이유는 그쪽 머리말에.
/// </param>
public sealed class QuickAskReveal(IServiceProvider services)
{
    /// <summary>
    /// 서랍이 <b>지금 펴져 있는지를 알맹이에게 흘려 주는 이름</b>
    /// (<see cref="Microsoft.AspNetCore.Components.CascadingValue{TValue}"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 값은 <c>bool?</c> 다 — <b>서랍 안이 아니면 아예 안 내려간다.</b> 같은
    /// 알맹이가 화면(<c>/projmng/ai/ask</c>)으로도 뜨는데, 그쪽에서 이 값을
    /// <c>false</c> 로 받으면 「안 보이는 중」으로 오해해 목록 따라가기를
    /// 통째로 멈춘다. 못 받았으면(<c>null</c>) 언제나 보이는 자리다.
    /// </para>
    /// <para>
    /// 알맹이(<c>AiAskPanel</c>)는 이 값이 거짓→참으로 갈리는 순간 목록을
    /// 다시 읽는다. <b>서랍은 닫아도 알맹이를 안 지우므로</b>(그쪽 머리말)
    /// 안 그러면 처음 열었을 때의 카드가 그대로 앉아 있다.
    /// </para>
    /// <para>
    /// 파라미터로 못 넘기는 이유는 <see cref="QuickAskContent"/> 와 같다 —
    /// 서랍은 알맹이의 타입조차 컴파일 시점에 모른다.
    /// </para>
    /// </remarks>
    public const string OpenCascade = "QuickAskOpen";

    /// <summary>지금 펴져 있나.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>여닫힘이 갈렸다. 판과 단추가 함께 듣는다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 서랍에 그릴 부품. 업무 모듈이 등록하지 않았으면 <c>null</c> 이다.
    /// </summary>
    public Type? PanelType => services.GetService<QuickAskContent>()?.PanelType;

    /// <summary>
    /// 서랍으로 열 수 있나. <b>거짓이면 단추는 화면으로 옮겨 간다.</b>
    /// </summary>
    public bool CanDock => PanelType is not null;

    /// <summary>여닫는다.</summary>
    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }

    /// <summary>닫는다. 이미 닫혀 있으면 아무 일도 하지 않는다.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Changed?.Invoke();
    }
}
