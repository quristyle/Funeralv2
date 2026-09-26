using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class BinaryParser
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private string? _raw;
    private DataTable _rows = JsonTable.Empty;

    private IReadOnlyList<McModel> _models = [];
    private IReadOnlyList<BinarySample> _samples = [];

    private int? _modelId;
    private int? _sampleId;

    private bool _savingSample;
    private string? _sampleTitle;

    protected override Task OnInitializedAsync() => LoadAsync(async () =>
    {
        _models = await Api.GetListAsync<McModel>("utils/mc-models");
        return _models.Count;
    }, "등록된 설비 모델이 없습니다.", "설비 모델을 읽지 못했습니다");

    /// <summary>모델을 고르면 그 모델에 보관된 전문을 읽는다.</summary>
    private async Task PickModelAsync(int? modelId)
    {
        _modelId = modelId;
        _sampleId = null;
        _samples = [];

        if (modelId is null)
        {
            return;
        }

        try
        {
            _samples = await Api.GetListAsync<BinarySample>($"utils/mc-models/{modelId}/samples");
        }
        catch (ApiException ex)
        {
            // 보관함을 못 읽는다고 파서를 못 쓰게 하지 않는다.
            Say($"보관한 전문을 읽지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
    }

    /// <summary>
    /// 보관한 전문을 원문 칸에 붙인다. <b>목록 응답에는 내용이 빠져 있어</b>
    /// 한 건을 따로 읽는다.
    /// </summary>
    private Task LoadSampleAsync() => LoadAsync(async () =>
    {
        var sample = await Api.GetAsync<BinarySample>($"utils/samples/{_sampleId}");

        _raw = sample?.Content;
        _sampleTitle = sample?.Title;
        _rows = JsonTable.Empty;

        return _raw is { Length: > 0 } ? 1 : 0;
    }, "그 전문에는 내용이 없습니다.", "전문을 불러오지 못했습니다");

    private void StartSaveSample()
    {
        if (string.IsNullOrWhiteSpace(_raw))
        {
            Say("보관할 원문이 없습니다.", NoticeTone.Warning);
            return;
        }

        _savingSample = true;
    }

    /// <summary>
    /// 보관한다.
    ///
    /// <para>
    /// 불러온 전문이 있고 이름을 그대로 두면 <b>덮어쓴다</b>. 이름을 바꾸면
    /// 새로 만든다 — 원본도 그렇게 갈랐다(덮어쓸 때는 확인을 물었다).
    /// </para>
    /// </summary>
    private async Task SaveSampleAsync()
    {
        if (string.IsNullOrWhiteSpace(_sampleTitle))
        {
            Say("전문 이름을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var loaded = _samples.FirstOrDefault(s => s.Id == _sampleId);
        var overwrite = loaded is not null &&
            string.Equals(loaded.Title, _sampleTitle, StringComparison.Ordinal);

        var body = new { content = _raw, title = _sampleTitle };

        if (await RunAsync(
                () => overwrite
                    ? Api.PutAsync($"utils/samples/{_sampleId}", body)
                    : Api.PostAsync($"utils/mc-models/{_modelId}/samples", body),
                overwrite ? "덮어썼습니다." : "보관했습니다.",
                "보관하지 못했습니다"))
        {
            _savingSample = false;
            await PickModelAsync(_modelId);
        }
    }

    private Task ParseAsync()
    {
        if (string.IsNullOrWhiteSpace(_raw))
        {
            Say("풀 원문을 넣으십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var json = await Api.PostAsync<JsonElement>("utils/parse-binary", new { content = _raw });
            _rows = JsonTable.From(json);
            return _rows.Rows.Count;
        }, "푼 결과가 없습니다. 원문 모양을 확인하십시오.", "풀지 못했습니다");
    }
}
