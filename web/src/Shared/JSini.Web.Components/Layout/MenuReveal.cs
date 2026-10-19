using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

/// <summary>「이 메뉴를 사이드바에서 보여 달라」는 부탁. 헤더의 브레드크럼이 하고 사이드바와 레이아웃이 받는다. 부품끼리 직접 말하지 않고 통을 하나 둔다 — 파라미터로 잇자면 레이아웃이 남의 화면 상태를 대신 들어야 한다.
/// 경로로 맞춘다: 브레드크럼은 거르기 전 원본에서, 사이드바는 거른 트리에서 메뉴를 찾는데 거르기가 새 기록을 만들어 같은 메뉴라도 인스턴스가 다르다. 찾지 못해도 아무것도 만들지 않는다 — 권한 때문에 트리에 없는 화면은 그냥 둔다.</summary>
public sealed class MenuReveal
{
    /// <summary>보여 달라는 메뉴의 경로(<see cref="MenuNode.Path"/>).</summary>
    public event Action<string>? Requested;

    /// <summary>사이드바에서 이 경로의 메뉴를 펼쳐 고르게 한다.</summary>
    public void Request(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Requested?.Invoke(path);
    }
}
