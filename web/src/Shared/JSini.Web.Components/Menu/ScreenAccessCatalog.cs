using System.Reflection;
using JSini.Web.Abstractions;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Menu;

/// <summary>
/// 화면 하나가 신고한 <b>권한의 출처</b>.
/// </summary>
/// <param name="PageType">라우터가 끼우는 화면 클래스. 판정의 열쇠다.</param>
/// <param name="Name">사람이 읽을 화면 이름 (<c>JSini.Web.Admin…AccountAppPage</c>).</param>
/// <param name="Template">첫 <c>@page</c> 템플릿. 진단 화면이 보여 준다.</param>
/// <param name="RouteKey"><see cref="RouteKeyAttribute"/> 로 신고한 열쇠. 없으면 <c>null</c>.</param>
/// <param name="Kind">권한이 어디서 오는가.</param>
/// <param name="InheritsFrom">물려받을 부모 메뉴의 열쇠. 없으면 <c>null</c>.</param>
/// <param name="Declared">
/// <see cref="ScreenAccessAttribute"/> 를 <b>직접 적었는가</b>. 거짓이면
/// 아래 기본값으로 정해진 것이다 — 진단 화면이 둘을 갈라 보여 준다.
/// </param>
/// <param name="Reason">왜 이렇게 두었는지 한 줄.</param>
public sealed record ScreenAccessRule(
    Type PageType,
    string Name,
    string Template,
    string? RouteKey,
    ScreenAccessKind Kind,
    string? InheritsFrom,
    bool Declared,
    string? Reason);

/// <summary>실려 있는 화면마다 권한의 출처를 모아 둔다. 기동 때 한 번 만들고 그 뒤로는 읽기만 한다 — RouteInventory 와 같은 자리다.
/// 주소가 아니라 화면 클래스로 찾는다 — 상세 화면의 주소에는 매개변수가 들어 있어 주소를 열쇠로 삼으면 템플릿을 여기서 다시 맞춰 봐야 하는데, 그 짝맞추기는 이미 라우터가 한 일이다. 두 벌이 되면 언젠가 갈리고 갈리는 쪽이 막아야 할 것을 여는 방향일 수 있다.
/// 포괄 라우트(_Pending)는 화면이 아니라 아직 화면이 없다는 안내라 기본이 열림이다 — 막으면 오타 주소가 전부 차단 기록으로 쌓여 진짜 막힌 줄이 묻힌다. 자료를 보여 주는 포괄 화면은 스스로 ScreenAccess(Menu) 를 적어 기본을 덮는다.</summary>
public sealed class ScreenAccessCatalog
{
    private readonly Dictionary<Type, ScreenAccessRule> _byType;

    private ScreenAccessCatalog(Dictionary<Type, ScreenAccessRule> byType)
    {
        _byType = byType;
        Rules = [.. byType.Values.OrderBy(r => r.Template, StringComparer.Ordinal)];
    }

    /// <summary>신고된 화면 전부. 진단 화면이 그대로 보여 준다.</summary>
    public IReadOnlyList<ScreenAccessRule> Rules { get; }

    /// <summary>
    /// 이 화면의 규칙. 라우트가 없는 컴포넌트면 <c>null</c> 이다.
    /// </summary>
    public ScreenAccessRule? For(Type? pageType) =>
        pageType is not null && _byType.TryGetValue(pageType, out var rule) ? rule : null;

    /// <summary>어셈블리를 훑어 <c>@page</c> 를 가진 화면의 신고를 모은다.</summary>
    public static ScreenAccessCatalog Build(params Assembly[] assemblies)
    {
        var byType = new Dictionary<Type, ScreenAccessRule>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var templates = type
                    .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                    .Cast<RouteAttribute>()
                    .Select(r => r.Template)
                    .ToList();

                if (templates.Count == 0)
                {
                    continue;
                }

                var declared = type
                    .GetCustomAttributes(typeof(ScreenAccessAttribute), inherit: false)
                    .Cast<ScreenAccessAttribute>()
                    .FirstOrDefault();

                var routeKey = type
                    .GetCustomAttributes(typeof(RouteKeyAttribute), inherit: false)
                    .Cast<RouteKeyAttribute>()
                    .FirstOrDefault()?.Key;

                // 포괄뿐인 화면은 「준비 중」 안내다. 신고가 없으면 열어 둔다(머리말).
                var catchAllOnly = templates.All(t => t.Contains("{*", StringComparison.Ordinal));

                var kind = declared?.Kind
                    ?? (catchAllOnly ? ScreenAccessKind.Open : ScreenAccessKind.Menu);

                byType[type] = new ScreenAccessRule(
                    PageType: type,
                    Name: type.FullName ?? type.Name,
                    Template: templates[0],
                    RouteKey: routeKey,
                    Kind: kind,
                    InheritsFrom: declared?.InheritsFrom,
                    Declared: declared is not null,
                    Reason: declared?.Reason ?? (catchAllOnly && declared is null
                        ? "포괄 라우트(「준비 중」 안내)라 보여 줄 자료가 없다."
                        : null));
            }
        }

        return new ScreenAccessCatalog(byType);
    }
}
