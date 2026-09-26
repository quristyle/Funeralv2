using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class McModelList
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private static readonly string[] PTypes = ["R", "S"];
    private static readonly string[] ParseTypes = ["number", "date"];

    private IReadOnlyList<McModel> _models = [];
    private IReadOnlyList<ParseItem> _items = [];

    private int? _modelId;
    private string? _modelName;

    private bool _editingModel;
    private McModel _model = new();

    private bool _editingItem;
    private ParseItem _item = new();

    /// <summary>편집 창의 키 칸. 자료는 숫자 목록이고 화면은 글자로 다룬다.</summary>
    private string? _keys;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _models = await Api.GetListAsync<McModel>("utils/mc-models");

        if (_modelId is not null)
        {
            await LoadItemsAsync();
        }

        return _models.Count;
    }, "등록된 모델이 없습니다.", "모델 목록을 읽지 못했습니다");

    private async Task PickAsync(McModel model)
    {
        _modelId = model.Id;
        _modelName = model.McName;
        await LoadItemsAsync();
    }

    /// <summary>
    /// 고른 모델의 해석 규칙. <b>목록 응답에는 규칙이 없다</b> — 모델 하나를
    /// 따로 읽어야 딸린 것이 함께 온다(<c>mc-models-full</c> 은 전부를 준다).
    /// </summary>
    private async Task LoadItemsAsync()
    {
        try
        {
            var full = await Api.GetListAsync<McModel>("utils/mc-models-full");
            _items = full.FirstOrDefault(m => m.Id == _modelId)?.ParseItems ?? [];
        }
        catch (ApiException ex)
        {
            _items = [];
            Say($"해석 규칙을 읽지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
    }

    // ── 모델 ────────────────────────────────────────────────

    private void StartNewModel()
    {
        _model = new McModel();
        _editingModel = true;
    }

    private void StartEditModel(McModel model)
    {
        _model = new McModel { Id = model.Id, McName = model.McName, StartKey = model.StartKey };
        _editingModel = true;
    }

    private async Task SaveModelAsync()
    {
        if (string.IsNullOrWhiteSpace(_model.McName))
        {
            Say("모델 이름을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var isNew = _model.Id == 0;
        var body = new { mcName = _model.McName, startKey = _model.StartKey ?? string.Empty };

        if (await RunAsync(
                () => isNew
                    ? Api.PostAsync("utils/mc-models", body)
                    : Api.PutAsync($"utils/mc-models/{_model.Id}", body),
                isNew ? "등록했습니다." : "저장했습니다.",
                isNew ? "등록하지 못했습니다" : "저장하지 못했습니다"))
        {
            _editingModel = false;
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 모델을 지운다. <b>딸린 해석 규칙과 보관 전문도 함께 사라진다</b> —
    /// 서버가 그렇게 지운다. 그래서 지우기 전에 무엇이 딸려 있는지 말해 준다.
    /// </summary>
    private async Task DeleteModelAsync(McModel model)
    {
        if (await RunAsync(() => Api.DeleteAsync($"utils/mc-models/{model.Id}"),
                $"{model.McName} 을(를) 지웠습니다. 딸린 규칙도 함께 지워졌습니다.",
                "지우지 못했습니다"))
        {
            if (_modelId == model.Id)
            {
                _modelId = null;
                _modelName = null;
                _items = [];
            }

            await ReloadAsync();
        }
    }

    // ── 해석 규칙 ───────────────────────────────────────────

    private void StartNewItem()
    {
        _item = new ParseItem { PTYPE = "R", BlocParseType = "number" };
        _keys = null;
        _editingItem = true;
    }

    private void StartEditItem(ParseItem item)
    {
        _item = new ParseItem
        {
            Id = item.Id,
            Desc = item.Desc,
            PTYPE = item.PTYPE,
            KeyIdx = item.KeyIdx,
            BlocParseLength = item.BlocParseLength,
            BlocParseType = item.BlocParseType,
        };

        _keys = item.Keys is { Count: > 0 } keys ? string.Join(',', keys) : null;
        _editingItem = true;
    }

    private async Task SaveItemAsync()
    {
        if (string.IsNullOrWhiteSpace(_item.Desc))
        {
            Say("설명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var isNew = _item.Id == 0;

        var body = new
        {
            desc = _item.Desc,
            ptype = _item.PTYPE ?? "R",
            keyIdx = _item.KeyIdx ?? 0,
            keys = _keys ?? string.Empty,
            blocParseType = _item.BlocParseType ?? "number",
            blocParseLength = _item.BlocParseLength ?? string.Empty,
        };

        if (await RunAsync(
                () => isNew
                    ? Api.PostAsync($"utils/mc-models/{_modelId}/parse-items", body)
                    : Api.PutAsync($"utils/parse-items/{_item.Id}", body),
                isNew ? "규칙을 넣었습니다." : "저장했습니다.",
                isNew ? "넣지 못했습니다" : "저장하지 못했습니다"))
        {
            _editingItem = false;
            await LoadItemsAsync();
        }
    }

    private async Task DeleteItemAsync(ParseItem item)
    {
        if (await RunAsync(() => Api.DeleteAsync($"utils/parse-items/{item.Id}"),
                "규칙을 지웠습니다.", "지우지 못했습니다"))
        {
            await LoadItemsAsync();
        }
    }
}
