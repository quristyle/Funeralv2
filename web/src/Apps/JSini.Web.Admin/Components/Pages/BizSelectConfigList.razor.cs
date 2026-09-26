using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class BizSelectConfigList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private IReadOnlyList<BizSelectConfigDto> _all = [];
    private string? _keyword;

    private static readonly string[] Services = ["auth", "funeral", "helpdesk", "projmng", "file", "ai"];
    private static readonly string[] Methods = ["GET", "POST"];

    private IReadOnlyList<BizSelectConfigDto> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();
            return [.. _all.Where(c =>
                c.BizType.Contains(k, StringComparison.OrdinalIgnoreCase)
                || c.ApiUrl.Contains(k, StringComparison.OrdinalIgnoreCase)
                || (c.Remark?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetBizSelectConfigsAsync();
        return _all.Count;
    }, "등록된 메타데이터가 없습니다.", "메타데이터를 읽지 못했습니다");

    private static void FillNew(BizSelectConfigDto c)
    {
        c.ServiceCode = "auth";
        c.HttpMethod = "GET";

        // 대부분이 이 셋이다. 비워 두면 드롭다운이 빈 채로 뜨는데, 그 원인이
        // 화면에서 잘 안 보여서 기본값을 넣어 둔다.
        c.LabelField = "name";
        c.ValueField = "id";
        c.ResultPath = "result";
    }

    private Task SaveAsync((BizSelectConfigDto Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.BizType) || string.IsNullOrWhiteSpace(e.Item.ApiUrl))
        {
            throw new ApiException("타입과 주소는 반드시 넣어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(e.Item.LabelField) || string.IsNullOrWhiteSpace(e.Item.ValueField))
        {
            throw new ApiException("이름 칸과 값 칸이 없으면 드롭다운이 빈 채로 뜹니다.");
        }

        return e.IsNew
            ? Api.CreateBizSelectConfigAsync(e.Item)
            : Api.UpdateBizSelectConfigAsync(e.Item.Id, e.Item);
    }

    private Task DeleteAsync(BizSelectConfigDto c) => Api.DeleteBizSelectConfigAsync(c.Id);
}
