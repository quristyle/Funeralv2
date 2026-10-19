namespace JSini.Web.Components.Layout;

/// <summary>「빠른 지시」 서랍이 그릴 알맹이가 무엇인가. 업무 모듈이 채운다. Blazor Common 은 업무 모듈을 참조할 수 없어(의존 규칙 4번) 알맹이 부품을 직접 쓰면 공용 레이아웃이 업무 하나에 묶인다. 그래서 방향을 뒤집어 모듈이 자기 부품을 등록하고(ConfigureServices) 서랍은 DynamicComponent 로 그린다 — 셸이 모듈을 훑는 IPortalModule 과 같은 구도다.
/// 등록이 없으면 서랍도 없다 — 그 모듈을 뺀 셸에서 헤더 단추는 예전처럼 화면으로 옮겨 가고 빈 서랍을 열어 보여 주지 않는다.</summary>
    /// <param name="panelType">그릴 Blazor 부품의 타입. 매개변수 없이 그려지므로 Parameter 를 요구하면 안 된다.</param>
public sealed class QuickAskContent(Type panelType)
{
    /// <summary>서랍이 그릴 부품.</summary>
    public Type PanelType { get; } = panelType;
}
