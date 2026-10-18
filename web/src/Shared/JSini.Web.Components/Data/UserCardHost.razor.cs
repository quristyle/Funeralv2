using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Data;

public partial class UserCardHost : IAsyncDisposable
{
    [Inject] private UserCard Cards { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>휴대폰이면 가운데 창, 아니면 말풍선. 레이아웃이 재는 값 그대로다.</summary>
    [Parameter] public bool IsPhone { get; set; }

    private const string ModulePath = "./_content/JSini.Web.Components/js/user-card.js";

    private IJSObjectReference? _module;
    private ElementReference _card;

    /// <summary>지금 그리고 있는 요청. 바뀌면 자료를 다시 읽고 자리를 다시 잰다.</summary>
    private UserCardRequest? _shown;
    private UserCardRequest? _placed;
    private UserCardInfo? _info;
    private bool _loading;

    private string? _noteTo;

    protected override void OnInitialized() => Cards.Changed += OnCardsChanged;

    private void OnCardsChanged() => _ = InvokeAsync(ShowAsync);

    private async Task ShowAsync()
    {
        var req = Cards.Current;
        if (ReferenceEquals(req, _shown))
        {
            return;
        }

        _shown = req;
        _info = null;
        _loading = req is not null;
        StateHasChanged();

        if (req is null)
        {
            return;
        }

        var info = await Cards.LoadAsync(req.LoginId);

        // 기다리는 사이 다른 사람의 카드로 바뀌었으면 버린다.
        if (!ReferenceEquals(req, Cards.Current))
        {
            return;
        }

        _info = info;
        _loading = false;
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var req = Cards.Current;
        if (ReferenceEquals(req, _placed))
        {
            return;
        }

        _placed = req;

        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", ModulePath);

            if (req is null)
            {
                await _module.InvokeVoidAsync("release");
            }
            else
            {
                await _module.InvokeVoidAsync("place", _card, req.AnchorId);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private string NameOf(UserCardRequest req) =>
        _info?.Name ?? req.Name ?? Faces.NameOf(req.LoginId) ?? req.LoginId;

    /// <summary>아바타가 이미 그리던 사진을 먼저 쓴다 — 카드가 뜨는 순간 얼굴이 비지 않게.</summary>
    private string? PhotoOf(UserCardRequest req) =>
        req.Photo ?? _info?.Photo ?? Faces.PhotoOf(req.LoginId);

    private string? Email => _info?.Email;

    private string? Phone => _info?.Phone;

    /// <summary>전화 주소에는 숫자와 + 만 남긴다. 「010-1234-5678」의 줄표는 기기마다 다르게 읽힌다.</summary>
    private static string TelHref(string phone) =>
        "tel:" + new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

    /// <summary>주소에 섞인 공백·쉼표가 받는 사람을 둘로 가르지 않게 하나만 남긴다.</summary>
    private static string MailHref(string email) =>
        "mailto:" + email.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)[0];

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            Cards.Close();
        }
    }

    private void WriteNote(string loginId)
    {
        Cards.Close();
        _noteTo = loginId;
    }

    public async ValueTask DisposeAsync()
    {
        Cards.Changed -= OnCardsChanged;

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("release");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
