using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class QnaThread
{
    /// <summary>그릴 글.</summary>
    [Parameter, EditorRequired] public QnaPost Post { get; set; } = new();

    /// <summary>들여쓰기 깊이. 뿌리가 0.</summary>
    [Parameter] public int Depth { get; set; }

    /// <summary>글을 쓸 수 있는 사람인가. 목록 응답의 <c>canWrite</c>.</summary>
    [Parameter] public bool CanReply { get; set; }

    /// <summary>관리자인가. 목록 응답의 <c>canManage</c>.</summary>
    [Parameter] public bool CanManage { get; set; }

    [Parameter] public EventCallback<QnaPost> Reply { get; set; }
    [Parameter] public EventCallback<QnaPost> Edit { get; set; }
    [Parameter] public EventCallback<QnaPost> Delete { get; set; }
    [Parameter] public EventCallback<QnaPost> ToggleVisibility { get; set; }
}
