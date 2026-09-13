using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

/// <summary>
/// 목록을 읽어 표로 보여 주는 화면의 뼈대.
///
/// [화면마다 같은 열 줄을 적지 않으려고 만든 것이다]
///
/// 이관해야 할 화면이 백 개가 넘는데, 그 대부분이 똑같은 모양이다 —
/// 조건을 받아 게이트웨이를 부르고, 성공하면 표를 그리고, 비었으면 "없습니다"
/// 를 띄우고, 실패하면 이유를 남긴다.
///
/// 그 열 줄을 화면마다 손으로 적으면 반드시 갈라진다. 실제로 갈라지는 곳은
/// 늘 <b>실패 처리</b>다 — 어떤 화면은 예외를 그대로 올려 포털을 하얗게 만들고,
/// 어떤 화면은 조용히 빈 표를 보여 준다. 뒤엣것이 더 나쁘다. 사용자는
/// "자료가 없다" 고 읽고, 실제로는 서버가 죽어 있다.
///
/// [예외를 삼키되 흔적은 남긴다]
///
/// <see cref="ApiException"/> 만 잡는다. 그건 "서버가 이렇게 답했다" 는 뜻이라
/// 화면에 옮길 말이 있다. 그 밖의 예외(널 참조 등)는 우리 잘못이므로 그대로
/// 올려 보낸다 — 삼키면 못 고친다.
/// </summary>
public abstract class DataPage : ComponentBase
{
    /// <summary>
    /// 지금 조회해도 되는가 — <b>회로가 붙어 있는가</b>.
    /// 프리렌더 중에는 거짓이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [화면마다 조회가 두 번 나가고 있었다]
    /// </para>
    ///
    /// <para>
    /// 포털은 프리렌더를 켜 두었다(<c>RenderMode.InteractiveServer</c> 의 기본값).
    /// 그러면 첫 진입·F5 마다 화면이 <b>두 번</b> 만들어진다 — 정적 SSR 로 한 번,
    /// 회로가 붙고 또 한 번. 수명 주기가 통째로 두 번 도니까 조회도 두 번 나갔다.
    /// </para>
    ///
    /// <para>
    /// 게이트웨이·서비스·DB 를 두 벌 태우고, 사용자는 자료가 <b>떴다 사라졌다
    /// 다시 뜨는 것</b>을 본다. 화면이 161개라 한 곳에서 막는다.
    /// </para>
    ///
    /// <para>
    /// [프리렌더를 끄지 않은 이유]
    /// </para>
    ///
    /// <para>
    /// <c>prerender: false</c> 로 두면 조회는 한 번이 되지만 <b>회로가 붙을
    /// 때까지 화면이 하얗다</b> — 껍데기도 안 그려진다. 여기서 조회만 건너뛰면
    /// 레이아웃 · 조건줄 · 빈 표가 즉시 그려지고 자료만 나중에 온다.
    /// 사용자가 보는 것은 「조회 중인 화면」이고, 그것이 맞는 그림이다.
    /// </para>
    ///
    /// <para>
    /// [<c>Loading</c> 을 켠 채로 돌아가는 것이 요점이다]
    /// </para>
    ///
    /// <para>
    /// 끄고 돌아가면 프리렌더된 표가 <b>「조회 결과가 없습니다」</b>를 띄운다 —
    /// 아직 묻지도 않았는데. 켠 채로 두면 표가 조회 중으로 그려지고, 회로가
    /// 붙어 실제 조회가 끝날 때 그대로 자료로 바뀐다.
    /// </para>
    ///
    /// <para>
    /// [정적 SSR 전용 화면에 쓰면 안 된다]
    /// </para>
    ///
    /// <para>
    /// 회로가 없는 화면이 <c>DataPage</c> 를 상속하면 <b>영원히 조회 중</b>이 된다.
    /// 지금 <c>[ExcludeFromInteractiveRouting]</c> 을 단 것은 셋이고
    /// (<c>App</c> · <c>Login</c> · <c>NoticeAutoPopup</c>) 아무것도
    /// <c>DataPage</c> 를 상속하지 않는다. 그런 화면을 만들 일이 생기면
    /// 조회를 <c>DataPage</c> 에 맡기지 않는다.
    /// </para>
    /// </remarks>
    private bool CanLoad => RendererInfo.IsInteractive;

    /// <summary>
    /// 옮기는 동안의 표시. 이 화면의 <b>첫 조회가 끝날 때까지</b> 덮어 둔다.
    ///
    /// <para>
    /// 화면이 이것을 직접 만질 일은 없다 — <see cref="LoadAsync"/> 가 잡고
    /// 놓는다. 같은 화면에서 「조회」를 누른 것은 세지 않는다(옮기는 중이
    /// 아니므로). 자세한 것은 <see cref="PageTransition"/> 머리말에 있다.
    /// </para>
    /// </summary>
    [Inject] private PageTransition Transition { get; set; } = default!;

    /// <summary>
    /// 동작의 결과를 알리는 곳. <b>화면이 직접 부르지 않는다</b> —
    /// <see cref="LoadAsync"/> · <see cref="RunAsync"/> · <see cref="Say"/> 가
    /// 대신 부른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [화면 위쪽 안내 줄에서 옮겨 왔다]
    /// </para>
    ///
    /// <para>
    /// 예전에는 여기 <c>Notice</c> · <c>Tone</c> 두 값이 있었고 화면 132개가
    /// <c>&lt;PageNotice Text="@Notice" Tone="@Tone" /&gt;</c> 한 줄을 똑같이
    /// 들고 그것을 그렸다. 옮긴 이유 셋은 <c>Toasts</c> 머리말에 있다.
    /// </para>
    ///
    /// <para>
    /// <b>그 두 값을 남겨 두지 않았다.</b> 남기면 어떤 화면은 토스트로,
    /// 어떤 화면은 안내 줄로 같은 말을 하게 되고 — 화면이 백 개가 넘으면
    /// 그 갈라짐이 되돌릴 수 없다. 지워 두면 옛 줄을 든 화면은
    /// <b>빌드가 그 자리에서 막는다.</b>
    /// </para>
    ///
    /// <para>
    /// 사라지면 안 되는 안내(「왼쪽에서 메뉴를 고르십시오」 따위)는 여전히
    /// <c>PageNotice</c> 다. 그쪽은 동작의 결과가 아니라
    /// <b>화면의 상태</b>라 화면이 자기 값으로 직접 그린다.
    /// </para>
    /// </remarks>
    [Inject] protected Toasts Toasts { get; set; } = default!;

    /// <summary>조회 중인가. 표의 <c>Loading</c> 에 그대로 넘긴다.</summary>
    protected bool Loading { get; private set; }

    /// <summary>
    /// 조회를 감싼다. 성공·빈 결과·실패를 한 곳에서 처리한다.
    /// </summary>
    /// <param name="load">
    /// 실제 조회. 돌려주는 값은 <b>결과 건수</b>다 — 0 이면 "없습니다" 를 띄운다.
    /// 건수를 셀 수 없는 화면(단건 조회 등)은 <see cref="LoadOneAsync"/> 를 쓴다.
    /// </param>
    /// <param name="emptyMessage">결과가 없을 때 띄울 문구.</param>
    /// <param name="failMessage">
    /// 실패했을 때 띄울 문구의 앞부분. 서버가 준 이유가 뒤에 붙는다.
    /// </param>
    protected async Task LoadAsync(
        Func<Task<int>> load,
        string emptyMessage = "조회 결과가 없습니다.",
        string failMessage = "조회하지 못했습니다")
    {
        Loading = true;

        // **첫 `await` 앞이어야 한다.** 뒤로 밀면 그 사이에 레이아웃의 첫 그림이
        // 끝나 버려, 조회가 시작되기도 전에 표시가 걷힌다.
        Transition.Claim();

        // **프리렌더에서는 조회하지 않는다.** `Loading` 을 켠 채로 돌아간다 —
        // 이유는 CanLoad 머리말에 있다. 잡은 것은 여기서 놓는다 — 안 놓으면
        // 회로가 붙기 전까지 표시가 걷히지 않는다.
        if (!CanLoad)
        {
            Transition.Release();
            return;
        }

        // **여기서 한 번 그린다.** 바로 아래가 첫 `await` 다 — 그리지 않으면
        // Blazor 는 이 처리기가 **끝난 뒤에야** 다시 그리고, 그때는 이미
        // `Loading` 이 꺼져 있다. 「불러오는 중」 표시가 영영 안 보이는 이유가
        // 그것이었다(ERD 처럼 몇 초 걸리는 화면에서 사람이 단추를 또 누른다).
        StateHasChanged();

        try
        {
            var count = await load();

            if (count == 0)
            {
                Toasts.Show(emptyMessage);
            }
        }
        catch (ApiException ex)
        {
            // 화면을 통째로 죽이지 않는다. 조건을 바꿔 다시 시도할 수 있어야 한다.
            Toasts.Show($"{failMessage} — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            Loading = false;
            Transition.Release();
        }
    }

    /// <summary>
    /// 단건 조회용. 결과가 <c>null</c> 이면 "없습니다" 를 띄운다.
    /// </summary>
    protected Task LoadOneAsync<T>(
        Func<Task<T?>> load,
        Action<T?> assign,
        string emptyMessage = "자료를 찾지 못했습니다.",
        string failMessage = "조회하지 못했습니다")
        where T : class
        => LoadAsync(async () =>
        {
            var value = await load();
            assign(value);
            return value is null ? 0 : 1;
        }, emptyMessage, failMessage);

    /// <summary>
    /// 저장·삭제처럼 <b>바꾸는</b> 동작을 감싼다.
    ///
    /// 조회와 갈라 둔 이유는 성공했을 때 할 말이 다르기 때문이다 — 조회는
    /// 성공하면 아무 말도 하지 않는 것이 맞고(표가 곧 결과다), 바꾸는 동작은
    /// 성공했다고 알려 줘야 한다. 화면에 아무 변화가 없는 저장도 있어서다.
    /// </summary>
    /// <param name="action">실제 동작</param>
    /// <param name="okMessage">성공했을 때 띄울 문구</param>
    /// <param name="failMessage">실패했을 때 띄울 문구의 앞부분</param>
    /// <returns>성공했으면 <c>true</c>. 부르는 쪽이 목록을 다시 읽을지 정한다.</returns>
    protected async Task<bool> RunAsync(
        Func<Task> action,
        string okMessage = "처리했습니다.",
        string failMessage = "처리하지 못했습니다")
    {
        Loading = true;

        try
        {
            await action();
            Toasts.Show(okMessage);
            return true;
        }
        catch (ApiException ex)
        {
            Toasts.Show($"{failMessage} — {ex.Message}", NoticeTone.Error);
            return false;
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>
    /// 화면이 직접 한마디 할 때. 「일정 이름을 넣으십시오」처럼 <b>서버까지
    /// 가지 않고 막은 것</b>이 대부분이다.
    ///
    /// <para>
    /// 사라지면 안 되는 안내는 이것으로 띄우지 않는다 —
    /// <c>PageNotice</c> 를 화면에 직접 둔다.
    /// </para>
    /// </summary>
    protected void Say(string? text, NoticeTone tone = NoticeTone.Info) =>
        Toasts.Show(text, tone);
}
