using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using JSini.Web.Http;
using JSini.Web.Components.Layout;

namespace JSini.Web.Components.Data;

public partial class ImageGroup
{
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private FileGroupClient Files { get; set; } = default!;

    /// <summary>
    /// 이 묶음의 그룹 아이디. <b>첫 장을 올리면 서버가 발급해 여기로 돌아온다</b> —
    /// 화면은 그 값을 업무 자료에 저장해야 한다.
    /// </summary>
    [Parameter] public string? GroupId { get; set; }

    [Parameter] public EventCallback<string?> GroupIdChanged { get; set; }

    /// <summary>FileServer 의 업무 구분. 저장 폴더 이름이 된다 (예: <c>funeralv2/building</c>).</summary>
    [Parameter, EditorRequired] public string BizType { get; set; } = string.Empty;

    /// <summary>몇 장까지. 원본(Vue)의 <c>limit</c> 자리다.</summary>
    [Parameter] public int Limit { get; set; } = 10;

    /// <summary>한 장 최대 크기(바이트). 넘으면 그 장만 빼고 이유를 말한다.</summary>
    [Parameter] public long MaxBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>대표 사진 고르기를 보일지. 여러 장 중 하나를 앞세우는 자리에서만 켠다.</summary>
    [Parameter] public bool ShowRepresentative { get; set; }

    /// <summary>칸 위에 적을 안내. 무엇에 쓰이는 사진인지 화면마다 다르다.</summary>
    [Parameter] public string? Hint { get; set; }

    private IReadOnlyList<GroupFile> _files = [];

    /// <summary>마지막으로 읽어 온 그룹. 바뀔 때만 다시 읽는다.</summary>
    private string? _loadedGroupId;

    private bool _busy;

    private CancellationTokenSource? _cts;

    protected override async Task OnParametersSetAsync()
    {
        // 부모가 다시 그릴 때마다 읽으면 편집 창을 여는 동안 몇 번씩 나간다.
        if (string.Equals(_loadedGroupId, GroupId, StringComparison.Ordinal))
        {
            return;
        }

        _loadedGroupId = GroupId;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _files = await Files.ListAsync(GroupId);
        }
        catch (ApiException ex)
        {
            // 사진을 못 읽는다고 편집 창을 죽이지 않는다.
            _files = [];
            Toasts.Show($"사진을 읽지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
    }

    private async Task UploadAsync(InputFileChangeEventArgs e)
    {
        var room = Limit - _files.Count;

        if (room <= 0)
        {
            Toasts.Show($"{Limit}장까지만 올릴 수 있습니다.", NoticeTone.Warning);
            return;
        }

        // 상한을 넉넉히 주고 우리가 세어 자른다 — GetMultipleFiles 는 넘기면
        // 던지기만 하고 「몇 장까지인지」를 말해 주지 못한다.
        var picked = e.GetMultipleFiles(Limit + 10);

        if (picked.Count > room)
        {
            Toasts.Show($"{Limit}장까지라 {picked.Count - room}장은 빼고 올립니다.", NoticeTone.Warning);
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _busy = true;

        // 스트림은 올린 뒤에 닫는다. 여기서 미리 닫으면 0바이트가 올라간다.
        var streams = new List<Stream>();

        try
        {
            var payload = new List<(Stream Content, string FileName, string? ContentType)>();

            foreach (var file in picked.Take(room))
            {
                if (file.Size > MaxBytes)
                {
                    Toasts.Show($"{file.Name} 은(는) {Human(MaxBytes)} 를 넘어 뺐습니다.", NoticeTone.Warning);
                    continue;
                }

                // 사진이라 통째로 들고 있어도 된다(한 장 상한이 20MB 다).
                // 영상처럼 수백 MB 를 다루는 자리는 이 부품을 쓰지 않는다.
                var buffer = new MemoryStream();
                await using (var source = file.OpenReadStream(MaxBytes, _cts.Token))
                {
                    await source.CopyToAsync(buffer, _cts.Token);
                }

                buffer.Position = 0;
                streams.Add(buffer);
                payload.Add((buffer, file.Name, file.ContentType));
            }

            if (payload.Count == 0)
            {
                return;
            }

            var result = await Files.UploadAsync(GroupId, payload, BizType, _cts.Token);

            // 첫 장이면 그룹이 방금 생겼다. 부모가 저장할 수 있게 알린다.
            if (!string.IsNullOrWhiteSpace(result.GroupId) &&
                !string.Equals(result.GroupId, GroupId, StringComparison.Ordinal))
            {
                GroupId = result.GroupId;
                _loadedGroupId = result.GroupId;
                await GroupIdChanged.InvokeAsync(result.GroupId);
            }

            await ReloadAsync();
        }
        catch (OperationCanceledException)
        {
            Toasts.Show("올리기를 멈췄습니다.", NoticeTone.Warning);
        }
        catch (Exception ex)
        {
            Toasts.Show($"사진을 올리지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }

            _busy = false;
        }
    }

    private async Task DeleteAsync(GroupFile file)
    {        _busy = true;

        try
        {
            await Files.DeleteAsync(file.Id);
            await ReloadAsync();
        }
        catch (ApiException ex)
        {
            Toasts.Show($"사진을 지우지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SetRepresentativeAsync(GroupFile file)
    {
        if (string.IsNullOrWhiteSpace(GroupId))
        {
            return;
        }        _busy = true;

        try
        {
            await Files.SetRepresentativeAsync(GroupId, file.Id);
            await ReloadAsync();
        }
        catch (ApiException ex)
        {
            Toasts.Show($"대표 사진을 바꾸지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
        finally
        {
            _busy = false;
        }
    }

    private static string Human(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024}MB" : $"{bytes / 1024}KB";

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
