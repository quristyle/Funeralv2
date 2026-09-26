using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

/// <summary>
/// <c>CommGrd</c> 의 오른쪽 클릭 창에 화면이 보태는 항목 하나.
///
/// <para>
/// <b>그리는 것이 없다.</b> <c>CommGrd</c> 의 <c>ContextMenuItems</c> 안에 적으면
/// 표에 자기를 알리고, 표가 창을 열 때 그 목록으로 항목을 세운다. 그래서
/// 권한으로 가리는 것도 단추 때와 똑같이 <c>PermissionView</c> 로 감싸면 된다 —
/// 안 그려진 항목은 알리지도 않으므로 창에 안 뜬다.
/// </para>
///
/// <code>
/// &lt;ContextMenuItems&gt;
///     &lt;PermissionView Action="MenuAction.Create"&gt;
///         &lt;CommMenuItem Text="사람 넣기" IconCssClass="jsini-icon-plus" Click="@OpenAddAsync" /&gt;
///     &lt;/PermissionView&gt;
/// &lt;/ContextMenuItems&gt;
/// </code>
///
/// <para>
/// 한동안 이런 동작은 표 아래 띠(<c>FooterLeft</c>)에 단추로 놓였다. 표의 기본
/// 동작(등록·다시 읽기·엑셀)이 오른쪽 클릭으로 옮겨 가자 <b>같은 표의 동작이
/// 두 자리로 갈렸다</b> — 다시 읽기는 창에, 사람 넣기는 띠에. 이 부품이 그것을
/// 한 자리로 모은다.
/// </para>
/// </summary>
public sealed class CommMenuItem : ComponentBase, IDisposable
{
    [CascadingParameter] private ICommMenuHost? Host { get; set; }

    /// <summary>창에 보일 글자.</summary>
    [Parameter, EditorRequired] public string Text { get; set; } = "";

    /// <summary>글자 앞 아이콘(<c>jsini-icon-*</c>). 비워도 된다.</summary>
    [Parameter] public string? IconCssClass { get; set; }

    /// <summary>
    /// 누를 수 있는가. 끄면 회색으로 남는다 — <b>없애지 않는다.</b> 조건이 안
    /// 맞아 못 누르는 것과 그런 동작이 없는 것은 다르다.
    /// </summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>앞에 구분선을 긋는다.</summary>
    [Parameter] public bool BeginGroup { get; set; }

    /// <summary>눌렀을 때.</summary>
    [Parameter] public EventCallback Click { get; set; }

    protected override void OnInitialized()
    {
        if (Host is null)
        {
            throw new InvalidOperationException(
                "CommMenuItem 은 CommGrd 의 ContextMenuItems 안에만 둘 수 있습니다.");
        }

        Host.Add(this);
    }

    public void Dispose() => Host?.Remove(this);
}

/// <summary>
/// <see cref="CommMenuItem"/> 을 받는 쪽. <c>CommGrd</c> 가 제네릭이라
/// 항목이 그 타입을 알 수 없어서 이 창구로 이어 준다.
/// </summary>
public interface ICommMenuHost
{
    void Add(CommMenuItem item);

    void Remove(CommMenuItem item);
}
