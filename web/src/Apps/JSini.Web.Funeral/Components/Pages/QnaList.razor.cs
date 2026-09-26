using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class QnaList
{
    [Inject] private HelpApi Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.NameOf(FilterOptions, o => o.Value, o => o.Text, _filter));

    private string? _keyword;
    private string? _filter;

    private IReadOnlyList<QnaPost> _items = [];
    private int _total;

    private bool _canWrite;
    private bool _canManage;

    private bool _editing;

    /// <summary>고치고 있는 글. 새 글이면 <c>null</c>.</summary>
    private QnaPost? _target;

    /// <summary>답글을 다는 대상. 새 질문이면 <c>null</c>.</summary>
    private QnaPost? _replyTo;

    private string? _title;
    private string? _content;
    private bool _isPublic = true;

    private string EditTitle => _replyTo is not null ? "답글" : _target is not null ? "글 수정" : "질문 등록";

    private static readonly SchOption[] FilterOptions =
    [
        new("mine", "내 글"),
        new("unanswered", "답변 대기"),
        new("answered", "답변 완료"),
    ];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var list = await Api.GetQnaListAsync(_filter, _keyword, pageSize: 50);

        _items = list.Items;
        _total = list.Total;
        _canWrite = list.CanWrite;
        _canManage = list.CanManage;

        // **잘렸으면 반드시 말한다.** 「전부다」로 읽고 넘어가면 없는 것을
        // 찾게 된다. 조회의 **결과**라 안내 줄이 아니라 토스트로 나간다.
        if (_total > _items.Count)
        {
            Say($"전체 {_total}건 중 {_items.Count}건입니다. 검색으로 좁히십시오.");
        }

        return _items.Count;
    }, "등록된 글이 없습니다.", "Q&A 를 읽지 못했습니다");

    private void StartAsk()
    {
        _target = null;
        _replyTo = null;
        _title = null;
        _content = null;
        _isPublic = true;
        _editing = true;
    }

    private void StartReply(QnaPost post)
    {
        _target = null;
        _replyTo = post;
        _title = null;
        _content = null;

        // 답글은 대상의 공개 여부를 따라간다. 비공개 질문에 공개 답글이 붙으면
        // 질문 없이 답만 보이게 된다.
        _isPublic = post.IsPublic;
        _editing = true;
    }

    private void StartEdit(QnaPost post)
    {
        _target = post;
        _replyTo = null;
        _title = post.Title;
        _content = post.Content;
        _isPublic = post.IsPublic;
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_content))
        {
            Say("내용을 적으십시오.", NoticeTone.Warning);
            return;
        }

        bool saved;

        if (_target is not null)
        {
            saved = await RunAsync(
                () => Api.UpdateQnaPostAsync(_target.Id, new
                {
                    title = _title,
                    content = _content,
                    isPublic = _isPublic,
                }),
                "저장했습니다.", "저장하지 못했습니다");
        }
        else
        {
            // 답글이면 parentId 만 보낸다. 뿌리는 서버가 찾는다 — 화면이
            // 계산해 보내면 깊은 답글에서 어긋나 스레드가 갈라진다.
            saved = await RunAsync(
                () => Api.CreateQnaPostAsync(new
                {
                    parentId = _replyTo?.Id,
                    title = _replyTo is null ? _title : null,
                    content = _content,
                    isPublic = _isPublic,
                }),
                _replyTo is null ? "등록했습니다." : "답글을 달았습니다.",
                "등록하지 못했습니다");
        }

        if (!saved)
        {
            // 창을 닫지 않는다. 닫으면 쓴 내용이 사라진다.
            return;
        }

        _editing = false;
        await ReloadAsync();
    }

    private async Task DeleteAsync(QnaPost post)
    {
        if (await RunAsync(() => Api.DeleteQnaPostAsync(post.Id),
                           "지웠습니다. 답글도 함께 지워집니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 공개 여부를 뒤집는다.
    ///
    /// 뿌리 글이면 답글까지 함께 바꾼다. 질문만 비공개로 돌리면 답글이 남아
    /// 무엇에 대한 답인지 모르는 글이 떠 있게 된다.
    /// </summary>
    private async Task ToggleVisibilityAsync(QnaPost post)
    {
        var next = !post.IsPublic;

        if (await RunAsync(
                () => Api.SetQnaVisibilityAsync(post.Id, next, includeReplies: post.Depth == 0),
                next ? "공개로 바꿨습니다." : "비공개로 바꿨습니다.",
                "공개 여부를 바꾸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>답글 대상 안내에 쓸 한 줄. 태그를 걷어내고 앞부분만.</summary>
    private static string PlainHead(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")).Trim();

        return text.Length <= 60 ? text : text[..60] + "…";
    }
}
