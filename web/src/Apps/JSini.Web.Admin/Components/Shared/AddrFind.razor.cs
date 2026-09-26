using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Components.Layout;

namespace JSini.Web.Admin.Components.Shared;

public partial class AddrFind
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>단추에 적는 글자.</summary>
    [Parameter] public string Text { get; set; } = "주소 찾기";

    /// <summary>
    /// 주소를 골랐을 때. 우편번호와 주소를 함께 준다.
    /// 고르지 않고 닫으면 부르지 않는다.
    /// </summary>
    [Parameter] public EventCallback<AddrPick> OnPicked { get; set; }

    private IJSObjectReference? _js;

    /// <summary>
    /// 지금 덮개가 떠 있는가. 떠 있는 동안 단추를 잠근다 —
    /// 연달아 누르면 덮개가 겹쳐 쌓이고, 뒤엣것을 닫아도 앞엣것이 남는다.
    /// </summary>
    private bool _opening;

    private async Task PickAsync()
    {
        if (_opening)
        {
            return;
        }

        _opening = true;

        try
        {
            _js ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/addr-find.js");

            var picked = await _js.InvokeAsync<AddrPick?>("pick");

            if (picked is not null)
            {
                await OnPicked.InvokeAsync(picked);
            }
        }
        catch (JSException)
        {
            // 스크립트를 못 받았거나 위젯이 죽었다. 주소는 손으로도 칠 수 있다.
            Say("주소 검색을 열지 못했습니다. 주소를 직접 입력해 주십시오.", NoticeTone.Warning);
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다(화면을 옮기는 순간 등). 띄울 곳이 없다.
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>골라 온 주소. JS 가 돌려주는 모양 그대로다.</summary>
    public sealed class AddrPick
    {
        public string ZipCode { get; set; } = string.Empty;

        /// <summary>도로명 또는 지번 주소. 참고 항목까지 붙어 있다.</summary>
        public string Address { get; set; } = string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        if (_js is null)
        {
            return;
        }

        try
        {
            await _js.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // 회로가 이미 끊겼다. 브라우저 쪽은 함께 사라진다.
        }
    }
}
