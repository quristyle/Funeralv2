using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiTaskViewPage
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private PortalTabs Tabs { get; set; } = default!;

    /// <summary>주소에 실려 온 지시 번호.</summary>
    [Parameter] public long TaskKey { get; set; }

    private AiTaskDto? _item;
    private bool _isPhone;

    /// <summary>「다시 읽기」를 누른 횟수. 안쪽(AiTaskView)에 그대로 넘기며, 값이 달라지면 상태가 같아도 다시 읽으라는 뜻이다.</summary>
    private int _tick;

    /// <summary>지금 그리고 있는 건. 주소가 바뀌었는지 보는 값이다.</summary>
    private long _shown;

    /// <summary>돌고 있는 동안만 도는 타이머.</summary>
    private CancellationTokenSource? _poll;

    private string Href => $"/projmng/ai/task/{TaskKey}";

    /// <summary>탭과 머리에 적을 이름. 제목이 오기 전에는 번호다.</summary>
    private string TabTitle =>
        _item?.Title is { Length: > 0 } title ? title : $"지시 #{TaskKey}";

    /// <summary>「다시 읽기」 단추의 꾸밈. 읽는 동안 돈다 — 단추가 꺼지기만 하면 누른 것이 먹었는지 알 수 없다.</summary>
    private string ReloadCss =>
        Loading ? "pm-taskview__reload is-busy" : "pm-taskview__reload";

    protected override async Task OnParametersSetAsync()
    {
        if (_shown == TaskKey)
        {
            return;
        }

        _shown = TaskKey;
        _item = null;

        StopPoll();

        // 받아 오기 전에 먼저 연다 — 탭이 늦게 서면 화면은 바뀌었는데 탭 줄은 직전 화면을 켠 채로 남는다.
        Tabs.Open(Href, TabTitle, standalone: true);

        await LoadAsync();
    }

    /// <summary>한 건을 받아 온다. 못 찾으면 그대로 두고, 화면이 「찾지 못했습니다」로 말한다.</summary>
    private async Task LoadAsync()
    {
        await LoadOneAsync(
            async () => await Api.GetAsync(TaskKey),
            item => _item = item,
            failMessage: "지시를 읽지 못했습니다");

        Tabs.Open(Href, TabTitle, standalone: true);

        Follow();
    }

    /// <summary>머리줄의 「다시 읽기」. 지금 건을 서버에서 처음부터 받아 화면 전체를 새것으로 갈아 끼운다 — 따라가기가 끝난 뒤의 변화(요약이 뒤늦게 적힘, 차수 추가)를 메운다. 신호(_tick)는 받아 온 뒤 같은 그림에 실어 올려야 안쪽이 지난 건으로 짓지 않는다.</summary>
    private async Task ReloadAsync()
    {
        await LoadAsync();

        _tick++;

        StateHasChanged();
    }

    /// <summary>단추가 무엇인가 했다. 상태가 바뀌었을 테니 다시 읽는다.</summary>
    private async Task OnChangedAsync(AiTaskDto task)
    {
        _item = task;

        await LoadAsync();

        StateHasChanged();
    }

    /// <summary>작성중인 건을 지웠거나 사용자 확인을 마쳤다. 이 탭을 닫고 「빠른 지시」로 돌아간다 — 이 주소가 가리킬 것이 없어졌다.</summary>
    private void OnClosedAsync(AiTaskDto _) => ClosePage();

    private void OnPhoneChanged(bool isPhone)
    {
        _isPhone = isPhone;
        StateHasChanged();
    }

    /// <summary>화면을 닫고 「빠른 지시」로 돌아간다.</summary>
    private void ClosePage()
    {
        StopPoll();

        Tabs.Close(Href);
        Navigation.NavigateTo("/projmng/ai/ask");
    }

    /// <summary>돌고 있으면 따라간다. 상태를 넘겨 줄 부모가 없으므로 이 화면이 직접 본다 — 안쪽은 실행 이력과 로그만 본다.</summary>
    private void Follow()
    {
        if (_item?.IsBusy != true)
        {
            StopPoll();
            return;
        }

        if (_poll is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _poll = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    // 「빠른 지시」와 같은 5초다. 로그는 안쪽이 3초마다 따로 따라가므로 여기서 더 자주 물을 이유가 없다.
                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

                    var fresh = await Api.GetAsync(TaskKey, cts.Token);

                    if (cts.IsCancellationRequested || fresh is null)
                    {
                        return;
                    }

                    _item = fresh;

                    await InvokeAsync(StateHasChanged);

                    if (!fresh.IsBusy)
                    {
                        StopPoll();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 화면을 떠났다. 정상이다.
            }
            catch
            {
                // 한 바퀴 실패했다고 화면을 깨지 않는다. 다음 바퀴에 다시 본다.
            }
        }, cts.Token);
    }

    private void StopPoll()
    {
        var cts = _poll;
        _poll = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 이미 치웠다.
        }
    }

    /// <summary><b>화면을 떠날 때 반드시 끈다.</b> 안 끄면 회로마다 타이머가 쌓인다.</summary>
    public void Dispose() => StopPoll();
}
