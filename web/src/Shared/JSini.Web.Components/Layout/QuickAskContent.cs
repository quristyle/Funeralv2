namespace JSini.Web.Components.Layout;

/// <summary>
/// 「빠른 지시」 서랍이 그릴 알맹이가 <b>무엇인가</b>. 업무 모듈이 채워 넣는다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 타입을 들고 다니나 — 그냥 부품을 쓰면 되지 않나]
/// </para>
///
/// <para>
/// 그 알맹이(<c>AiAskPanel</c>)는 프로젝트관리 모듈 안에 있고,
/// <b>Blazor Common 은 업무 모듈을 참조할 수 없다</b>(의존 규칙 4번,
/// <c>DependencyRuleTests</c> 가 막는다). 참조가 생기는 순간 공용 레이아웃이
/// 업무 하나에 묶여서, 그 모듈을 빼면 셸이 빌드조차 안 된다.
/// </para>
///
/// <para>
/// 그래서 방향을 뒤집는다 — <b>모듈이 자기 부품을 등록하고</b>
/// (<c>ProjMngModule.ConfigureServices</c>), 서랍은 그것을
/// <see cref="Microsoft.AspNetCore.Components.DynamicComponent"/> 로 그린다.
/// 컴파일 시점에는 이름조차 모른다. 셸이 모듈을 훑어 찾는 것
/// (<c>IPortalModule</c>)과 같은 구도다.
/// </para>
///
/// <para>
/// [등록이 없으면 서랍도 없다]
/// </para>
///
/// <para>
/// 이 서비스가 아예 등록되지 않을 수 있다 — 그 모듈을 뺀 셸이 그렇다.
/// 그때 헤더의 단추는 <b>예전처럼 화면으로 옮겨 간다</b>. 빈 서랍을 열어
/// 보여 주지 않는다.
/// </para>
/// </remarks>
/// <param name="panelType">
/// 그릴 Blazor 부품의 타입. 매개변수 없이 그려지므로
/// <c>[Parameter]</c> 를 요구하면 안 된다.
/// </param>
public sealed class QuickAskContent(Type panelType)
{
    /// <summary>서랍이 그릴 부품.</summary>
    public Type PanelType { get; } = panelType;
}
