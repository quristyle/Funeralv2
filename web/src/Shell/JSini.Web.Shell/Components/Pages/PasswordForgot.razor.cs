using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.Shell.Security;

namespace JSini.Web.Shell.Components.Pages;

public partial class PasswordForgot
{
    [Inject] private AccountClient Accounts { get; set; } = default!;

    private string? _loginId;
    private string? _email;

    /// <summary>보냈는가. 참이면 폼 대신 안내만 보인다.</summary>
    private bool _sent;

    /// <summary>보내는 중인가. 두 번 누르면 앞 링크가 죽으므로 막는다.</summary>
    private bool _busy;

    /// <summary>보내는 중이거나 아직 회로가 안 붙었으면 단추를 잠근다.</summary>
    private bool Blocked => _busy || !RendererInfo.IsInteractive;

    /// <summary>화면에 남겨 둘 안내. 토스트를 안 쓰는 이유는 머리말에 있다.</summary>
    private string? _notice;

    private NoticeTone _tone = NoticeTone.Info;

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_loginId) || string.IsNullOrWhiteSpace(_email))
        {
            _notice = "아이디와 이메일을 모두 넣으십시오.";
            _tone = NoticeTone.Warning;
            return;
        }

        _notice = null;
        _tone = NoticeTone.Info;
        _busy = true;

        // 서버가 언제나 성공으로 답하므로 여기서 실패가 오는 것은
        // 게이트웨이에 닿지 못했을 때(서버가 꺼져 있음)뿐이다.
        try
        {
            await Accounts.RequestPasswordResetAsync(_loginId!.Trim(), _email!.Trim());
            _sent = true;
        }
        catch (ApiException ex)
        {
            _notice = $"요청을 보내지 못했습니다 — {ex.Message}";
            _tone = NoticeTone.Error;
        }
        finally
        {
            _busy = false;
        }
    }
}
