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
    /// <summary>안내 줄에 띄울 문구. 없으면 <c>null</c>.</summary>
    protected string? Notice { get; private set; }

    /// <summary>안내의 성격.</summary>
    protected NoticeTone Tone { get; private set; } = NoticeTone.Info;

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
        Notice = null;
        Tone = NoticeTone.Info;

        try
        {
            var count = await load();

            if (count == 0)
            {
                Notice = emptyMessage;
            }
        }
        catch (ApiException ex)
        {
            // 화면을 통째로 죽이지 않는다. 조건을 바꿔 다시 시도할 수 있어야 한다.
            Notice = $"{failMessage} — {ex.Message}";
            Tone = NoticeTone.Error;
        }
        finally
        {
            Loading = false;
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
        Notice = null;
        Tone = NoticeTone.Info;

        try
        {
            await action();
            Notice = okMessage;
            return true;
        }
        catch (ApiException ex)
        {
            Notice = $"{failMessage} — {ex.Message}";
            Tone = NoticeTone.Error;
            return false;
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>화면이 직접 안내를 띄울 때.</summary>
    protected void Say(string? text, NoticeTone tone = NoticeTone.Info)
    {
        Notice = text;
        Tone = tone;
    }
}
