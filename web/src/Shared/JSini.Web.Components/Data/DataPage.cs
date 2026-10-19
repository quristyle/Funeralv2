using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

/// <summary>목록을 읽어 표로 보여 주는 화면의 뼈대. 조회·빈 결과·실패를 한 곳에서 처리한다 — 화면마다 손으로 적으면 갈라지는 곳은 실패 처리다(예외를 올려 하얗게 만들거나 빈 표를 보여 서버가 죽은 것을 자료 없음으로 읽게 한다).
/// ApiException 만 잡는다 — 서버가 답한 것이라 옮길 말이 있다. 그 밖의 예외는 우리 잘못이라 그대로 올린다.</summary>
public abstract class DataPage : BasePage
{
    /// <summary>지금 조회해도 되는가 — 회로가 붙어 있는가. 프리렌더 중에는 거짓이다.
    /// 포털은 프리렌더를 켜 두어 화면이 두 번 만들어지므로 조회도 두 번 나가던 것을 여기서 막는다. 조회를 건너뛰되 Loading 은 켠 채 돌려, 프리렌더 표가 「없습니다」를 띄우지 않고 회로가 붙어 자료가 오면 그대로 바뀌게 한다. 회로 없는 화면이 DataPage 를 상속하면 영원히 조회 중이 된다.</summary>
    private bool CanLoad => RendererInfo.IsInteractive;

    /// <summary>옮기는 동안의 표시. 이 화면의 첫 조회가 끝날 때까지 덮어 둔다 — LoadAsync 가 잡고 놓는다. 같은 화면의 「조회」는 세지 않는다(PageTransition 머리말).</summary>
    [Inject] private PageTransition Transition { get; set; } = default!;

    /// <summary>동작의 결과를 알리는 곳. 화면이 직접 부르지 않고 LoadAsync · RunAsync · Say 가 부른다 — 예전의 Notice·Tone 두 값은 지웠다. 두면 어떤 화면은 토스트로, 어떤 화면은 안내 줄로 같은 말을 하게 되고, 지우면 옛 줄을 든 화면은 빌드가 막는다. 사라지면 안 되는 상태 안내는 여전히 PageNotice 로 화면이 그린다.</summary>
    [Inject] protected Toasts Toasts { get; set; } = default!;

    /// <summary>조회 중인가. 표의 <c>Loading</c> 에 그대로 넘긴다.</summary>
    protected bool Loading { get; private set; }

    /// <summary>조회를 감싼다. 성공·빈 결과·실패를 한 곳에서 처리한다.</summary>
    /// <param name="load">실제 조회. 돌려주는 값은 결과 건수다 — 0 이면 「없습니다」. 건수를 셀 수 없는 단건 조회는 LoadOneAsync 를 쓴다.</param>
    /// <param name="emptyMessage">결과가 없을 때 띄울 문구.</param>
    /// <param name="failMessage">실패했을 때 띄울 문구의 앞부분. 서버가 준 이유가 뒤에 붙는다.</param>
    protected async Task LoadAsync(
        Func<Task<int>> load,
        string emptyMessage = "조회 결과가 없습니다.",
        string failMessage = "조회하지 못했습니다")
    {
        Loading = true;

        // 첫 await 앞이어야 한다 — 뒤로 밀면 조회가 시작되기도 전에 레이아웃의 첫 그림이 끝나 표시가 먼저 걷힌다.
        Transition.Claim();

        // 프리렌더에서는 조회하지 않고 Loading 을 켠 채로 돌아간다(CanLoad 머리말). 잡은 것은 여기서 놓는다 — 안 놓으면 회로가 붙기 전까지 표시가 안 걷힌다.
        if (!CanLoad)
        {
            Transition.Release();
            return;
        }

        // 여기서 한 번 그린다 — 바로 아래가 첫 await 라 그리지 않으면 Blazor 는 이 처리기가 끝난 뒤에야 다시 그리고 「불러오는 중」이 영영 안 보인다(몇 초 걸리는 화면에서 사람이 단추를 또 누른다).
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

    /// <summary>단건 조회용. 결과가 null 이면 「없습니다」를 띄운다.</summary>
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

    /// <summary>저장·삭제처럼 바꾸는 동작을 감싼다. 조회와 갈라 둔 이유는 성공했을 때 할 말이 다르기 때문이다 — 조회는 표가 곧 결과라 말이 없어도 되고, 바꾸는 동작은 화면에 변화가 없어도 성공을 알려야 한다.</summary>
    /// <param name="action">실제 동작</param>
    /// <param name="okMessage">성공했을 때 띄울 문구</param>
    /// <param name="failMessage">실패했을 때 띄울 문구의 앞부분</param>
    /// <returns>성공했으면 true. 부르는 쪽이 목록을 다시 읽을지 정한다.</returns>
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

    /// <summary>화면이 직접 한마디 할 때. 서버까지 가지 않고 막은 것(「일정 이름을 넣으십시오」)이 대부분이다. 사라지면 안 되는 안내는 이것으로 띄우지 않고 PageNotice 를 화면에 둔다.</summary>
    protected void Say(string? text, NoticeTone tone = NoticeTone.Info) =>
        Toasts.Show(text, tone);
}
