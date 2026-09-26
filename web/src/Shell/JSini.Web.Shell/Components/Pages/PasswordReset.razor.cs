using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.Shell.Security;

namespace JSini.Web.Shell.Components.Pages;

public partial class PasswordReset
{
    [Inject] private AccountClient Accounts { get; set; } = default!;

    /// <summary>메일 링크가 실어 온 토큰. 없으면 폼을 그리지 않는다.</summary>
    [SupplyParameterFromQuery] private string? Token { get; set; }

    private string? _new;
    private string? _confirm;

    /// <summary>다시 정했는가. 참이면 로그인 화면으로 가는 길만 보인다.</summary>
    private bool _done;

    /// <summary>
    /// 링크가 죽었는가(시간이 지났거나 이미 썼다). 참이면 폼 대신
    /// 「다시 요청」 길만 준다 — 같은 링크로는 무엇을 넣어도 안 된다.
    /// </summary>
    private bool _dead;

    /// <summary>보내는 중인가. 두 번 눌러 토큰을 태우지 않게 막는다.</summary>
    private bool _busy;

    /// <summary>
    /// 지금 단추를 잠가야 하는가 — 보내는 중이거나 <b>아직 회로가 안 붙었다</b>.
    /// 뒤엣것이 왜 필요한지는 폼 위의 주석에 있다.
    /// </summary>
    private bool Blocked => _busy || !RendererInfo.IsInteractive;

    /// <summary>
    /// 화면에 남겨 둘 안내. <b>토스트를 쓰지 않는 이유는 머리말에 있다.</b>
    /// </summary>
    private string? _notice;

    private NoticeTone _tone = NoticeTone.Info;

    /// <summary>제목 밑 한 줄. 상태마다 할 말이 다르다.</summary>
    private string Sub =>
        string.IsNullOrWhiteSpace(Token) ? "받으신 메일의 링크로 들어와 주십시오."
        : _done ? "다 됐습니다."
        : "새로 쓰실 비밀번호를 정해 주십시오.";

    private async Task SubmitAsync()
    {
        if (string.IsNullOrEmpty(_new))
        {
            Warn("새 비밀번호를 넣으십시오.");
            return;
        }

        if (!string.Equals(_new, _confirm, StringComparison.Ordinal))
        {
            Warn("새 비밀번호와 확인이 다릅니다.");
            return;
        }

        _notice = null;
        _tone = NoticeTone.Info;
        _busy = true;

        // 실패 이유는 서버가 구분해 준다(시간 지남 · 이미 씀 · 지금 것과 같음).
        // 그 문구를 그대로 띄운다 — 다시 요청해야 하는지, 다른 값을 넣어야
        // 하는지가 갈리기 때문이다.
        try
        {
            await Accounts.ResetPasswordAsync(Token!, _new!);

            _done = true;
            _new = null;
            _confirm = null;
        }
        catch (ApiException ex)
        {
            _notice = ex.Message;
            _tone = NoticeTone.Error;

            // 링크가 죽은 것이면 폼을 치운다. 어느 코드가 죽은 것인지는
            // `PasswordResetEndpoints` 가 정한다.
            _dead = ex.Code is "EXPIRED" or "USED";
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>서버까지 가지 않고 막은 것. 화면에 남겨 둔다.</summary>
    private void Warn(string text)
    {
        _notice = text;
        _tone = NoticeTone.Warning;
    }
}
