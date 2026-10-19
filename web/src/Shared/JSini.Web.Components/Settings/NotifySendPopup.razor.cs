using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Layout;

namespace JSini.Web.Components.Settings;

public partial class NotifySendPopup
{
    [Inject] private NotifySender Sender { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;

    /// <summary>창이 열려 있는가. <c>@@bind-Visible</c> 로 묶는다.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <inheritdoc cref="Visible"/>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>받는 사람들. 비어 있으면 보내기가 잠긴다.</summary>
    [Parameter] public IReadOnlyList<NotifyRecipient> Recipients { get; set; } = [];

    /// <summary>창 머리에 쓸 글자.</summary>
    [Parameter] public string HeaderText { get; set; } = "알림 보내기";

    /// <summary>창을 열 때 제목 칸에 채워 둘 글. 사람이 고쳐 쓸 수 있다.</summary>
    [Parameter] public string? DefaultSubject { get; set; }

    /// <summary>창을 열 때 내용 칸에 채워 둘 글.</summary>
    [Parameter] public string? DefaultBody { get; set; }

    /// <summary>푸시를 눌렀을 때 열 화면. 비우면 서비스워커가 첫 화면을 연다. 메일에는 실리지 않는다 — 내부 주소를 넣으면 회사 밖에서 열었을 때 닿지 않는 링크가 된다.</summary>
    [Parameter] public string? LinkUrl { get; set; }

    /// <summary>보내고 난 뒤. 목록을 다시 읽거나 고른 것을 푸는 화면이 받는다. 막힌 경우에도 온다 — 무엇이 막혔는지는 인자 안에 있다.</summary>
    [Parameter] public EventCallback<NotifySendOutcome> OnSent { get; set; }

    private bool _push = true;
    private bool _email;

    private string? _subject;
    private string? _body;

    private bool _sending;

    /// <summary>창이 열린 것을 알아채려고 들고 있는 값.</summary>
    private bool _wasVisible;

    /// <summary>실제로 보낼 사람들. 상한을 넘으면 앞에서 자른다.</summary>
    private IReadOnlyList<NotifyRecipient> Targets =>
        Recipients.Count <= NotifySender.MaxRecipients
            ? Recipients
            : [.. Recipients.Take(NotifySender.MaxRecipients)];

    /// <summary>상한에 걸려 잘린 사람 수. <b>조용히 빼지 않는다.</b></summary>
    private int Cut => Math.Max(0, Recipients.Count - NotifySender.MaxRecipients);

    /// <summary>보낼 수 있는가 — 사람 · 길 · 제목 셋이 있어야 한다. 제목을 요구하는 것은 서버가 제목 없이는 푸시를 막기 때문이다. 여기서 잠가 두면 왕복 없이 알 수 있다.</summary>
    private bool CanSend =>
        !_sending
        && Targets.Count > 0
        && (_push || _email)
        && !string.IsNullOrWhiteSpace(_subject);

    /// <summary>창이 열릴 때 칸을 첫 문구로 되돌린다. 닫을 때 비우지 않는 까닭은 보내기가 막혀 닫았다 다시 여는 사람의 글을 잃지 않게 하려서다.</summary>
    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible)
        {
            _subject = DefaultSubject;
            _body = DefaultBody;
        }

        _wasVisible = Visible;
    }

    private Task OnVisibleChangedAsync(bool visible)
    {
        Visible = visible;
        return VisibleChanged.InvokeAsync(visible);
    }

    /// <summary>사진이 없을 때 동그라미에 넣을 글자 한 자.</summary>
    /// <summary>고른 길로 보낸다. 한 길이 막혀도 다른 길은 간다 — 둘을 한 try 로 묶으면 푸시가 던졌을 때 메일이 아예 시도되지 않는데 둘은 서로 다른 서비스 경로라 함께 죽을 이유가 없다.</summary>
    private async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        _sending = true;

        // **첫 `await` 앞에서 한 번 그린다.** 안 그리면 보내는 동안 단추가
        // 눌린 채로 남아, 몇 초 걸리는 발송에서 사람이 한 번 더 누른다 —
        // 그러면 같은 알림이 두 통 간다(`DataPage.LoadAsync` 와 같은 자리).
        StateHasChanged();

        var ids = Targets.Select(r => r.LoginId).ToArray();
        var subject = _subject!.Trim();
        var body = (_body ?? string.Empty).Trim();

        PushSendResultDto? push = null;
        string? pushError = null;
        var emailSent = false;
        string? emailError = null;

        try
        {
            if (_push)
            {
                try
                {
                    // 아이콘은 **보낸 사람**의 얼굴이다. 받은 쪽이 먼저 묻는 것이
                    // 「누가 보냈나」라서다.
                    push = await Sender.SendPushAsync(
                        ids, subject, body, LinkUrl, Me.Username);
                }
                catch (ApiException ex)
                {
                    pushError = ex.Message;
                }
            }

            if (_email)
            {
                try
                {
                    // 메일은 제목만으로는 읽을 것이 없어서 내용이 비었으면 제목을 본문으로 삼는다. 틀은 여기서 짜지 않는다 — 평문을 넘기면 서버가 회사 메일 꼴을 입힌다(화면마다 HTML 을 짜면 틀이 복제된다). 이름을 싣는 것은 푸시 아이콘과 같은 자리다 — 보내는 계정은 하나라 메일함에는 늘 JSini 포털로 뜨므로 누가 보냈는지를 이름으로 준다.
                    await Sender.SendEmailAsync(
                        ids, subject, body.Length > 0 ? body : subject,
                        senderName: Me.DisplayName);

                    emailSent = true;
                }
                catch (ApiException ex)
                {
                    emailError = ex.Message;
                }
            }
        }
        finally
        {
            _sending = false;
        }

        var outcome = new NotifySendOutcome
        {
            PushTried = _push,
            Push = push,
            PushError = pushError,
            EmailTried = _email,
            EmailSent = emailSent,
            EmailError = emailError,
        };

        Toasts.Show(Describe(outcome), outcome.HasError ? NoticeTone.Warning : NoticeTone.Info);

        // **막혀도 닫는다.** 까닭은 토스트에 있고, 창을 열어 둔 채로 두면
        // 사람은 같은 글을 한 번 더 보낸다.
        await OnVisibleChangedAsync(false);
        await OnSent.InvokeAsync(outcome);
    }

    /// <summary>결과를 사람이 읽는 한 줄로 만든다. 푸시가 0 대 0 이면 보냈습니다라고 말하지 않는다 — 서버가 센 까닭(구독 없음 · 본인이 껐음)을 그대로 옮긴다.</summary>
    private static string Describe(NotifySendOutcome outcome)
    {
        var parts = new List<string>();

        if (outcome.PushTried)
        {
            if (outcome.PushError is { } error)
            {
                parts.Add($"푸시를 보내지 못했습니다 — {error}");
            }
            else if (outcome.Push is { Sent: > 0 } sent)
            {
                parts.Add($"푸시 {sent.Sent}대 기기로 보냈습니다.");
            }
            else
            {
                var why = new List<string>();
                var none = outcome.Push?.OwnersWithoutSubscription ?? 0;
                var off = outcome.Push?.OptedOut ?? 0;

                if (none > 0)
                {
                    why.Add($"알림을 등록한 기기가 없는 사람 {none}명");
                }

                if (off > 0)
                {
                    why.Add($"푸시를 꺼 둔 사람 {off}명");
                }

                parts.Add(why.Count > 0
                    ? $"푸시는 가지 않았습니다 ({string.Join(" · ", why)})."
                    : $"푸시는 가지 않았습니다 ({outcome.Push?.Message ?? "받을 기기가 없습니다"}).");
            }
        }

        if (outcome.EmailTried)
        {
            parts.Add(outcome.EmailSent
                ? "메일을 보냈습니다."
                : $"메일을 보내지 못했습니다 — {outcome.EmailError ?? "알 수 없는 까닭"}");
        }

        return parts.Count == 0 ? "보낸 것이 없습니다." : string.Join(" ", parts);
    }
}
