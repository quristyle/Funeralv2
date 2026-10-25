using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Components.Layout;

namespace JSini.Web.Components.Data;

public partial class FilePicker
{
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>몇 개까지. 1 이면 하나만 고르게 하고, 새로 고르면 앞엣것을 갈아 끼운다.</summary>
    [Parameter] public int MaxFiles { get; set; } = 5;

    /// <summary>한 개 최대 크기(바이트). DevExpress 가 넘는 파일에 표시를 붙여 막는다.
    /// <b>0 이하면 크기를 따지지 않는다</b> — Takeout mbox 처럼 GB 단위가 들어오는 자리가 있다.</summary>
    [Parameter] public long MaxBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>모두 합쳐 최대 크기(바이트). 0 이면 총량을 따지지 않는다. 메일 첨부 때문에 생겼다 — 서버는 총량 15MB 로 막는데 화면은 한 개 크기만 보고 있어, 5개 × 15MB 를 다 받아 올린 뒤에야 거절됐다. 먼저 말해 주려고 둔 상한이 먼저 말해 주지 못한 셈이다.</summary>
    [Parameter] public long MaxTotalBytes { get; set; }

    /// <summary>받을 형식. 쉼표로 여럿(image/* · video/mp4,video/webm). 비우면 아무거나.</summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>칸 아래 적을 안내. 무엇을 올리는 자리인지 화면마다 다르다.</summary>
    [Parameter] public string? Hint { get; set; }

    /// <summary>그 안내 줄을 그릴 것인가. 끄는 자리는 고르개가 아이콘 하나로 줄어 다른 줄에 끼어 있을 때다 — 한 줄이 통째로 줄을 풀어 버린다. 끈 쪽은 같은 말을 아이콘의 title 로 달아 자리를 옮긴다.</summary>
    [Parameter] public bool ShowHint { get; set; } = true;

    /// <summary>고른 것이 바뀔 때마다. 화면은 이 목록을 들고 있다가 저장할 때 읽는다.</summary>
    [Parameter] public EventCallback<IReadOnlyList<PickedFile>> FilesChanged { get; set; }

    /// <summary>바이트를 받는 동안의 진행(받은 만큼, 전체). <b>안 매면 아무 알림도 안 낸다</b> —
    /// 회로를 타는 알림이라 쓰는 자리에서만 켠다. DevExpress 도 막대를 그리지만 남은 시간이 없고,
    /// 제 몫이 끝나면 「Uploaded」로 바뀌어 <b>뒤에 남은 일을 끝난 것처럼 보이게</b> 한다.</summary>
    [Parameter] public EventCallback<(long Received, long Total)> Progress { get; set; }

    /// <summary>브라우저에서 고른 파일 이름. 바이트를 받기 전의 선택 상태도 화면에 알린다.</summary>
    [Parameter] public EventCallback<IReadOnlyList<string>> SelectionChanged { get; set; }

    /// <summary>아이콘 단추에 걸 설명. 글자가 없으니 이것이 이름 노릇을 한다.</summary>
    [Parameter] public string SelectTitle { get; set; } = "파일 고르기";

    private DxFileInput? _input;

    /// <summary>고르기 단추의 아이디. ExternalSelectButtonCssSelector 가 이것으로 우리 단추를 찾는다. 부품마다 달라야 한다 — 한 화면에 첨부 칸이 둘이면 아이디를 글자로 박아 둔 둘째 칸의 단추가 첫째 칸의 고르개를 연다.</summary>
    private readonly string _selectId = $"jsini-pick-{Guid.NewGuid():N}";

    /// <summary>받아 둔 파일. 열쇠는 DevExpress 가 파일마다 붙이는 Guid 다 — 이름으로 열쇠를 삼으면 같은 이름을 두 번 고를 때 한쪽을 뺀 것이 엉뚱한 임시 파일을 지운다.</summary>
    private readonly Dictionary<string, PickedFile> _byGuid = [];

    /// <summary>진행을 알리는 간격. 더 잦으면 회로 왕복만 늘고, 더 뜸하면 막대가 뚝뚝 끊겨 보인다.</summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(400);

    /// <summary>진행을 셀 때 한 번에 읽는 크기.</summary>
    private const int ProgressChunkBytes = 256 * 1024;

    /// <summary>이 부품이 만든 임시 폴더. 사라질 때 통째로 지운다.</summary>
    private string? _tempDir;

    /// <summary>총량을 넘겨 우리가 빼는 중인가. RemoveFiles 가 OnSelectedFilesChangedAsync 를 다시 부르므로 막지 않으면 자기가 부른 것을 또 처리하며 맴돈다.</summary>
    private bool _trimming;

    /// <summary>지금 들고 있는 것. 고른 순서를 지킨다.</summary>
    private IReadOnlyList<PickedFile> Files => [.. _byGuid.Values];

    /// <summary>크기를 따지지 않는가.</summary>
    private bool Unlimited => MaxBytes <= 0;

    /// <summary>DevExpress 의 MaxFileSize 는 int 다(약 2.1GB). 우리 상한이 그보다 크면 여기서 자른다 — 넘기면 OverflowException 이 아니라 조용히 음수가 되므로 막아 둔다.
    /// <b>0 은 DevExpress 에서 「제한 없음」이다</b> — 2.1GB 로 못박으면 그보다 큰 mbox 가 고르기 단계에서 거절된다.</summary>
    private int DxMaxFileSize => Unlimited ? 0 : (int)Math.Min(MaxBytes, int.MaxValue);

    /// <summary>스트림을 열 때 넘기는 상한. 제한이 없으면 막지 않는다 — 0 을 그대로 넘기면 첫 바이트에서 터진다.</summary>
    private long ReadLimit => Unlimited ? long.MaxValue : MaxBytes;

    private List<string>? AcceptedTypes => string.IsNullOrWhiteSpace(Accept)
        ? null
        : [.. Accept.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private string HintText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Hint))
            {
                return Hint;
            }

            var total = MaxTotalBytes > 0 ? $" · 모두 합쳐 {Human(MaxTotalBytes)}" : string.Empty;
            var one = Unlimited ? "크기 제한 없음" : $"한 개 {Human(MaxBytes)} 까지";
            return $"최대 {MaxFiles}개 · {one}{total}";
        }
    }

    /// <summary>고른 것이 바뀌었다. 빼기와 총량을 여기서 처리한다. 이 이벤트는 파일이 서버로 넘어오기 전에 온다 — 총량을 여기서 따져야 넘치는 파일을 받기 전에 돌려보낼 수 있다.</summary>
    private async Task OnSelectedFilesChangedAsync(IEnumerable<UploadFileInfo> selected)
    {
        if (_trimming)
        {
            return;
        }

        var list = selected.ToList();

        // ── 빠진 것의 임시 파일을 지운다 ──────────────────────
        var alive = list.Select(f => f.Guid).ToHashSet(StringComparer.Ordinal);

        foreach (var gone in _byGuid.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            Delete(_byGuid[gone].TempPath);
            _byGuid.Remove(gone);
        }

        var removedGuids = new HashSet<string>(StringComparer.Ordinal);

        // ── 총량 ──────────────────────────────────────────────
        if (MaxTotalBytes > 0)
        {
            long running = 0;
            var over = new List<UploadFileInfo>();

            foreach (var file in list)
            {
                var size = (long)file.Size;

                // 먼저 고른 것을 남기고 뒤엣것을 뺀다. 반대로 하면 방금 고른
                // 파일 때문에 이미 붙여 둔 것이 사라진다.
                if (running + size > MaxTotalBytes)
                {
                    over.Add(file);
                    continue;
                }

                running += size;
            }

            if (over.Count > 0)
            {
                _trimming = true;
                try
                {
                    _input?.RemoveFiles(over);
                }
                catch (Exception)
                {
                    // 목록에서 못 빼도 우리 쪽 셈은 이미 맞다. 여기서 던지면
                    // 이벤트 처리기 밖으로 나가 **회로가 통째로 끊긴다** —
                    // 첨부 하나 때문에 화면 전체가 멎는 쪽이 훨씬 나쁘다.
                }
                finally
                {
                    _trimming = false;
                }

                foreach (var file in over)
                {
                    removedGuids.Add(file.Guid);
                    _byGuid.Remove(file.Guid);
                }

                Toasts.Show(
                    over.Count == 1
                        ? $"{over[0].Name} 을(를) 더하면 모두 합쳐 {Human(MaxTotalBytes)} 를 넘어 뺐습니다."
                        : $"모두 합쳐 {Human(MaxTotalBytes)} 를 넘어 {over.Count}개를 뺐습니다.",
                    NoticeTone.Warning);
            }
        }

        await SelectionChanged.InvokeAsync(
            [.. list.Where(file => !removedGuids.Contains(file.Guid)).Select(file => file.Name)]);
        await FilesChanged.InvokeAsync(Files);
    }

    /// <summary>파일이 서버로 넘어온다. 임시 파일로 받아 둔다. 올릴 주소가 없다 — DxFileInput 은 바이트를 우리 회로로 넘겨 줄 뿐이고 그 다음은 전부 서버 코드다.</summary>
    private async Task OnFilesUploadingAsync(FilesUploadingEventArgs e)
    {
        foreach (var file in e.Files)
        {
            // 총량을 넘겨 방금 빼낸 것이 여기까지 올 수 있다. 다시 확인한다.
            if (_byGuid.ContainsKey(file.Guid))
            {
                continue;
            }

            var ct = file.CancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                _byGuid[file.Guid] = await ReceiveAsync(file, ct);
            }
            catch (OperationCanceledException)
            {
                // 사람이 취소 단추를 눌렀다. DevExpress 가 목록에서 표시한다.
            }
            catch (Exception ex)
            {
                // 브라우저가 파일을 못 읽는 경우(고른 뒤 원본을 지웠다 등)가 있다.
                // 화면 전체를 죽이지 않고 이 자리에서 말한다.
                Toasts.Show($"{file.Name} 을(를) 받지 못했습니다 — {ex.Message}", NoticeTone.Warning);
            }
        }

        await SelectionChanged.InvokeAsync([.. Files.Select(file => file.Name)]);
        await FilesChanged.InvokeAsync(Files);
    }

    /// <summary>파일 하나를 임시 파일로 받는다.</summary>
    private async Task<PickedFile> ReceiveAsync(IFileInputSelectedFile file, CancellationToken ct)
    {
        _tempDir ??= Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "jsini-upload", Guid.NewGuid().ToString("N"))).FullName;

        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));

        await using var source = file.OpenReadStream(ReadLimit, ct);
        await using var target = File.Create(path);

        if (!Progress.HasDelegate)
        {
            // 진행을 안 쓰는 자리는 DevExpress 의 막대에 맡긴다.
            await source.CopyToAsync(target, ct);
        }
        else
        {
            var total = (long)file.Size;
            var buffer = new byte[ProgressChunkBytes];
            long received = 0;
            var lastSaid = DateTime.UtcNow;

            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                received += n;

                // 조각마다 알리면 500MB 에서 수천 번 회로를 왕복한다. 시간으로 조인다.
                var now = DateTime.UtcNow;
                if (now - lastSaid < ProgressInterval && received < total) continue;

                lastSaid = now;
                await Progress.InvokeAsync((received, total));
            }

            await Progress.InvokeAsync((received, total));
        }

        return new PickedFile(
            file.Name,
            string.IsNullOrWhiteSpace(file.Type) ? "application/octet-stream" : file.Type,
            (long)file.Size,
            path);
    }

    /// <summary>고른 것을 모두 버린다. 저장이 끝난 뒤 화면이 부른다.</summary>
    public async Task ClearAsync()
    {
        foreach (var file in _byGuid.Values)
        {
            Delete(file.TempPath);
        }

        _byGuid.Clear();

        // 목록도 비운다. 안 비우면 저장이 끝났는데 첨부가 붙어 있는 것처럼 보이고,
        // 다음에 저장하면 같은 파일이 또 올라간다.
        _trimming = true;
        try
        {
            _input?.RemoveAllFiles();
        }
        catch (Exception)
        {
            // 위와 같은 이유 — 목록 비우기에 실패해도 회로는 살려 둔다.
        }
        finally
        {
            _trimming = false;
        }

        await SelectionChanged.InvokeAsync([]);
        await FilesChanged.InvokeAsync(Files);
    }

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 임시 파일이다. 못 지워도 업무에 영향이 없다.
        }
    }

    /// <summary>사람이 읽는 크기.</summary>
    private static string Human(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:0.#} GB",
        >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };

    /// <summary>임시 파일을 지운다. ClearAsync 를 부르지 않는다 — 그쪽은 자식의 목록을 비우고 FilesChanged 를 부르는데 사라지는 중에는 둘 다 위험하다(던지면 회로가 끊긴다). 여기서 할 일은 디스크를 치우는 것 하나뿐이다.</summary>
    public ValueTask DisposeAsync()
    {
        foreach (var file in _byGuid.Values)
        {
            Delete(file.TempPath);
        }

        _byGuid.Clear();

        try
        {
            if (_tempDir is not null && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        return ValueTask.CompletedTask;
    }
}
