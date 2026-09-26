using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestEdit
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    /// <summary>주소의 요청 키.</summary>
    [Parameter] public string Id { get; set; } = string.Empty;

    private static readonly Dictionary<string, string> CommentCaptions = new(StringComparer.Ordinal)
    {
        ["authorName"] = "작성자",
        ["content"] = "내용",
        ["createdAt"] = "작성일",
    };

    private JsonElement? _request;
    private DataTable _comments = JsonTable.Empty;

    private string? _status;

    /// <summary>서버가 쓰는 상태 값.</summary>
    private static readonly string[] Statuses = ["OPEN", "IN_PROGRESS", "DONE", "CLOSED"];

    /// <summary>
    /// 주소가 바뀌면 다시 읽는다.
    ///
    /// <c>OnInitializedAsync</c> 가 아니라 <c>OnParametersSetAsync</c> 인 이유는,
    /// 같은 화면에서 다른 요청으로 이동할 때 Blazor 가 컴포넌트를 다시 만들지
    /// 않기 때문이다 — 초기화에만 걸어 두면 주소만 바뀌고 내용이 그대로다.
    /// </summary>
    protected override Task OnParametersSetAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 본문과 이력을 나란히. 서로 기다릴 이유가 없다.
        var request = Api.GetAsync<JsonElement>($"requests/{Id}");
        var comments = Api.GetAsync<JsonElement>($"requests/{Id}/comments");

        await Task.WhenAll(request, comments);

        _request = request.Result;
        _comments = JsonTable.From(comments.Result);

        return _request is null ? 0 : 1;
    }, "그런 요청을 찾지 못했습니다.", "요청을 읽지 못했습니다");

    /// <summary>
    /// 응답에서 칸 하나를 글자로 꺼낸다.
    ///
    /// DTO 를 두지 않은 이유는 <c>JsonTable</c> 주석과 같다 — 이 응답의 칸이
    /// 백엔드 사정으로 늘고 줄어서, 박아 두면 새 칸이 조용히 사라진다.
    /// </summary>
    private async Task SaveStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(_status))
        {
            Say("바꿀 상태를 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.PutAsync($"requests/{Id}", new { status = _status }),
                "상태를 바꿨습니다.", "상태를 바꾸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private string Text(string name) => Value(name) ?? "-";

    private string RequesterName() =>
        Value("requesterName")
        ?? NestedValue("customer", "userName")
        ?? "-";

    private string? NestedValue(string parentName, string name) =>
        _request is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(parentName, out var parent)
        && parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;

    /// <summary>
    /// 본문을 화면에 넣을 수 있는 HTML 로 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>칸 이름은 <c>description</c> 이다.</b> 여기서 오래 <c>content</c> 를 읽고
    /// 있었는데 서버 응답에 그런 칸이 없어서 본문 자리가 늘 <c>-</c> 였다
    /// (서버는 <c>ImprovementRequest.Description</c> 을 camelCase 로 내려준다).
    /// 옛 이름도 함께 본다 — 서버가 칸을 늘리는 일이 있고, 그때 둘 중 하나만
    /// 보고 있으면 본문이 조용히 사라진다.
    /// </para>
    /// <para>
    /// <c>MarkupString</c> 은 <b>거른 다음에만</b> 쓴다. 본문을 쓰는 사람은
    /// 고객이므로 그 글이 그대로 실행되면 이 포털을 보는 담당자 쪽에서 터진다.
    /// 거르는 규칙은 공지 본문과 같은 한 벌을 쓴다(<c>NoticeHtml</c>) — 거기에
    /// <c>/api/file/…</c> 주소를 셸 중계 경로로 옮기는 일까지 들어 있어,
    /// 본문에 박힌 그림이 포털(:5557)에서도 그대로 보인다.
    /// </para>
    /// </remarks>
    private MarkupString Body()
    {
        var html = Value("description") ?? Value("content");
        return new MarkupString(NoticeHtml.Sanitize(html));
    }

    /// <summary>응답에서 칸 하나를 꺼낸다. 없으면 <c>null</c>.</summary>
    private string? Value(string name) =>
        _request is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;
}
