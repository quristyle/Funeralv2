using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class FaqListPage
{
    [Inject] private HelpApi Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.Or(_category));

    private string? _keyword;
    private string? _category;

    private IReadOnlyList<Faq> _items = [];
    private IReadOnlyList<string> _categories = [];

    /// <summary>서버가 「이 사람은 고칠 수 있다」고 한 경우에만 참.</summary>
    private bool _canManage;

    private bool _editing;
    private bool _isNew;
    private Faq _edit = new();

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var list = await Api.GetFaqListAsync(_category, _keyword);

        _items = list.Items;
        _categories = list.Categories;
        _canManage = list.CanManage;

        return _items.Count;
    }, "등록된 질문이 없습니다.", "F.A.Q 를 읽지 못했습니다");

    private void StartNew()
    {
        _isNew = true;
        _edit = new Faq
        {
            Category = _category,
            Status = 1,

            // 마지막 뒤에 붙인다. 0 으로 두면 새 질문이 맨 앞에 끼어든다.
            OrderNo = _items.Count == 0 ? 1 : _items.Max(f => f.OrderNo) + 1,
        };
        _editing = true;
    }

    /// <summary>
    /// 편집 창에 <b>복사본</b>을 띄운다.
    ///
    /// 원본을 그대로 묶으면 저장을 취소해도 화면에는 이미 바뀐 값이 남는다 —
    /// 화면과 서버가 어긋난 채로 보이고, 새로 고치기 전에는 알 수 없다.
    /// </summary>
    private void StartEdit(Faq faq)
    {
        _isNew = false;
        _edit = new Faq
        {
            Id = faq.Id,
            Question = faq.Question,
            Answer = faq.Answer,
            Category = faq.Category,
            OrderNo = faq.OrderNo,
            Status = faq.Status,
        };
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_edit.Question))
        {
            Say("질문을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var body = new
        {
            question = _edit.Question,
            answer = _edit.Answer,
            category = _edit.Category,
            orderNo = _edit.OrderNo,
            status = _edit.Status,
        };

        var saved = await RunAsync(
            () => _isNew ? Api.CreateFaqAsync(body) : Api.UpdateFaqAsync(_edit.Id, body),
            _isNew ? "등록했습니다." : "저장했습니다.",
            _isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (!saved)
        {
            // 창을 닫지 않는다. 닫으면 쓴 내용이 사라진다.
            return;
        }

        _editing = false;
        await ReloadAsync();
    }

    private async Task DeleteAsync(Faq faq)
    {
        if (await RunAsync(() => Api.DeleteFaqAsync(faq.Id), "지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
