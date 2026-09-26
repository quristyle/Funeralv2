using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class _Smoke
{
    private string? _sql = "select cm_cd, cm_nm\nfrom projmng.dev_comm\nwhere cm_type = 'X';";
    private void SetFromOutside() => _sql = "SELECT 1";

    private string? _userCode;
    private BizOption? _userItem;

    private DateTime _anchor = DateTime.Today;
    private DateRangePreset _preset = DateRangePreset.Month;
    private DateRange? _range;

    private DiagramViewer _diagram = default!;
    private string? _savedJson;

    private Task DrawSampleAsync() => _diagram.LoadAsync(new ErdModel
    {
        Entities =
        [
            new ErdEntity { Id = "dev_comm", Name = "dev_comm", Desc = "공통코드", X = 40, Y = 40 },
            new ErdEntity { Id = "dev_proj", Name = "dev_proj", Desc = "프로젝트" },
        ],
        Relations = [new ErdRelation { From = "dev_proj", To = "dev_comm", Label = "코드 참조" }],
    });

    private async Task SaveDiagramAsync()
    {
        var model = await _diagram.SaveAsync();
        _savedJson = model.ToJson();
    }
}
