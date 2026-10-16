using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace JSini.Web.Admin.Components.Pages;

public partial class RagDocuments
{
    [Inject] private RagLibraryClient Api { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private IReadOnlyList<RagCategoryDto> _categories = [];
    private IReadOnlyList<RagDocumentDto> _documents = [];
    private List<RagFolderNode> _tree = [];

    private string? _categoryId;
    private long? _folderId;

    /// <summary>지금 고른 분류의 이름. 트리에서 어디에 있는지 보여 준다.</summary>
    private string _folderLabel = "분류 미지정";

    private bool _busy;

    /// <summary>
    /// 「정말 지웁니까」를 묻는 창. <b>지우기는 전부 이것을 거친다</b> —
    /// 카테고리를 지우면 분류·문서·디스크의 파일까지 함께 사라지고
    /// 되돌릴 방법이 없다.
    /// </summary>
    private ConfirmDialog? _confirm;

    // ── 끌어다 놓기 ─────────────────────────────────────────────

    /// <summary>파일을 떨굴 자리. JS 가 여기에 귀를 단다.</summary>
    private ElementReference _dropZone;

    /// <summary>숨겨 둔 파일 고르개. 떨군 파일을 JS 가 여기에 꽂는다.</summary>
    private InputFile? _fileInput;

    private IJSObjectReference? _drop;

    /// <summary>떨구기 귀를 달아 두었나. 판이 사라지면 거짓으로 되돌린다.</summary>
    private bool _wired;

    /// <summary>지금 끌고 있는 분류. 놓는 자리를 정하는 데 쓴다.</summary>
    private long? _dragFolder;

    /// <summary>지금 끌고 있는 문서. 분류를 끄는 것과 **떨구는 자리가 같아서** 갈라 둔다.</summary>
    private long? _dragDoc;

    /// <summary>끌고 있는 것이 지나가는 자리. 선을 그어 보여 준다.</summary>
    private (long Id, bool Before)? _dragOver;

    // 새 카테고리 만들기
    private bool _newCategoryVisible;
    private string _newCategoryId = string.Empty;
    private string _newCategoryName = string.Empty;

    // 새 분류 만들기
    private bool _newFolderVisible;
    private string _newFolderName = string.Empty;
    private long? _newFolderParent;

    private RagCategoryDto? Current
        => _categories.FirstOrDefault(c => c.Id == _categoryId);

    /// <summary>분류에 안 들어간 문서 수. 전체에서 분류에 든 것을 뺀 값이다.</summary>
    /// <remarks>
    /// 서버가 따로 세어 주지 않는다 — 분류별 수를 이미 보내므로 빼면 나오고,
    /// 칸을 하나 더 두면 둘이 어긋나는 날이 온다.
    /// </remarks>
    private int LooseCount
    {
        get
        {
            var total = Current?.DocTotal ?? 0;
            return Math.Max(0, total - Sum(_tree));

            static int Sum(List<RagFolderNode> ns)
                => ns.Sum(n => n.DocCount + Sum(n.Children));
        }
    }

    /// <summary>분류에 안 들어간 문서 중 아직 색인에 안 든 수.</summary>
    private int LoosePending
        => Math.Max(0, (Current?.DocPending ?? 0) - _tree.Sum(n => n.PendingTotal));

    /// <summary>
    /// 머리와 트리의 숫자를 다시 센다.
    /// </summary>
    /// <remarks>
    /// <b>문서를 올리거나 지운 뒤에는 반드시 부른다.</b> 목록만 다시 읽으면
    /// 아래에는 올린 파일이 보이는데 머리는 0 으로 남아, 올라가지 않은 것처럼
    /// 읽힌다 — 실제로 그렇게 보였다.
    /// </remarks>
    private async Task RefreshCountsAsync()
    {
        _categories = await Api.CategoriesAsync();
        await LoadTreeAsync();
    }

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    /// <summary>
    /// 떨구기 귀를 단다. <b>회로가 붙은 뒤에만</b> 한다 — 프리렌더에는
    /// 브라우저가 없어 JS 를 부를 수 없다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive) return;

        // **`firstRender` 에만 달면 영영 안 달린다.** 떨굴 판은 카테고리가
        // 있어야 서는데(`@if`), 첫 그림에는 아직 못 읽었다 — `OnInitializedAsync`
        // 가 기다리는 사이에 빈 쪽 가지가 먼저 그려진다. 그래서 **판이 설 때**
        // 단다. 실제로 그래서 끌어다 놓기가 통째로 죽어 있었다.
        var standing = _categoryId is not null && _fileInput?.Element is { } el;
        if (!standing)
        {
            // 판이 사라졌다(카테고리를 다 지웠다). 다음에 서면 다시 단다 —
            // 그때는 DOM 요소가 아예 다른 것이라 옛 귀가 아무 데도 안 붙어 있다.
            _wired = false;
            return;
        }

        if (_wired) return;

        try
        {
            _drop ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/rag-drop.js");
            await _drop.InvokeVoidAsync("wire", _dropZone, _fileInput!.Element);
            _wired = true;
        }
        catch (JSException)
        {
            // 떨구기가 안 될 뿐이다. 단추로 고르는 길은 그대로 있다.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_drop is not null)
        {
            try
            {
                await _drop.InvokeVoidAsync("unwire", _dropZone);
                await _drop.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // 회로가 이미 끊겼다. 귀도 함께 사라졌다.
            }
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _categories = await Api.CategoriesAsync();

        if (_categoryId is null || _categories.All(c => c.Id != _categoryId))
        {
            _categoryId = _categories.FirstOrDefault()?.Id;
            _folderId = null;
            _folderLabel = "분류 미지정";
        }

        await LoadTreeAsync();
        await LoadDocumentsAsync();
        return _categories.Count;
    });

    private async Task LoadTreeAsync()
    {
        if (_categoryId is null) { _tree = []; return; }

        var flat = await Api.FoldersAsync(_categoryId);

        // 평평한 목록을 부모-자식으로 엮는다. 분류는 깊어야 서너 단계라
        // 재귀로 한 번 훑으면 끝난다.
        var byId = flat.ToDictionary(
            f => f.Id,
            f => new RagFolderNode
            {
                Id = f.Id, Name = f.Name, DocCount = f.DocCount, DocPending = f.DocPending,
            });
        var roots = new List<RagFolderNode>();

        foreach (var f in flat)
        {
            var node = byId[f.Id];
            if (f.ParentId is { } p && byId.TryGetValue(p, out var parent)) parent.Children.Add(node);
            else roots.Add(node);
        }

        _tree = roots;
    }

    private async Task LoadDocumentsAsync()
    {
        _documents = _categoryId is null ? [] : await Api.DocumentsAsync(_categoryId, _folderId);
    }

    // ── 고르기 ──────────────────────────────────────────────────

    private async Task PickCategoryAsync(string id)
    {
        _categoryId = id;
        _folderId = null;
        _folderLabel = "분류 미지정";
        await LoadTreeAsync();
        await LoadDocumentsAsync();
    }

    private async Task PickFolderAsync(long? id, string label)
    {
        _folderId = id;
        _folderLabel = label;
        await LoadDocumentsAsync();
    }

    // ── 카테고리 ────────────────────────────────────────────────

    private void OpenNewCategory()
    {
        _newCategoryId = string.Empty;
        _newCategoryName = string.Empty;
        _newCategoryVisible = true;
    }

    private async Task SaveCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCategoryId))
        {
            Say("열쇠를 적어 주세요.", NoticeTone.Warning);
            return;
        }

        var ok = await RunAsync(async () =>
        {
            await Api.CreateCategoryAsync(_newCategoryId, _newCategoryName, null);
        }, "카테고리를 만들었습니다.");

        if (!ok) return;

        _newCategoryVisible = false;
        _categoryId = _newCategoryId.Trim().ToLowerInvariant();
        await ReloadAsync();
    }

    private async Task DeleteCategoryAsync()
    {
        if (_categoryId is null || _confirm is null) return;

        // **무엇이 사라지는지 숫자로 적는다.** "정말 지웁니까" 만으로는
        // 카테고리 하나가 문서 수십 건을 데려간다는 것을 알 수 없다.
        var docs = Current?.DocCount ?? 0;
        if (!await _confirm.AskAsync(
                $"「{Current?.Name ?? _categoryId}」 카테고리를 지웁니다.\n"
                + $"분류와 문서 {docs}건, 디스크의 파일까지 함께 사라집니다. 되돌릴 수 없습니다.",
                "카테고리 지우기"))
        {
            return;
        }

        var ok = await RunAsync(
            async () => await Api.DeleteCategoryAsync(_categoryId),
            "카테고리를 지웠습니다.");

        if (!ok) return;

        _categoryId = null;
        await ReloadAsync();
    }

    // ── 분류 ────────────────────────────────────────────────────

    private void OpenNewFolder(long? parentId)
    {
        _newFolderParent = parentId;
        _newFolderName = string.Empty;
        _newFolderVisible = true;
    }

    private async Task SaveFolderAsync()
    {
        if (_categoryId is null || string.IsNullOrWhiteSpace(_newFolderName))
        {
            Say("분류 이름을 적어 주세요.", NoticeTone.Warning);
            return;
        }

        var ok = await RunAsync(
            async () => await Api.CreateFolderAsync(_categoryId, _newFolderParent, _newFolderName),
            "분류를 만들었습니다.");

        if (!ok) return;

        _newFolderVisible = false;
        await LoadTreeAsync();
    }

    private async Task DeleteFolderAsync(long id)
    {
        if (_confirm is null) return;

        var name = Find(_tree, id)?.Name ?? "이 분류";
        if (!await _confirm.AskAsync(
                $"「{name}」 분류를 지웁니다.\n"
                + "아래 분류도 함께 사라집니다. 문서는 지워지지 않고 분류 미지정으로 올라갑니다.",
                "분류 지우기"))
        {
            return;
        }

        var ok = await RunAsync(
            async () => await Api.DeleteFolderAsync(id),
            "분류를 지웠습니다. 안에 있던 문서는 분류 미지정으로 올라갑니다.");

        if (!ok) return;

        if (_folderId == id) { _folderId = null; _folderLabel = "분류 미지정"; }
        await LoadDocumentsAsync();
        await RefreshCountsAsync();
    }

    // ── 분류 끌어 옮기기 ────────────────────────────────────────

    /// <summary>
    /// 끌어 놓은 자리로 분류를 옮긴다.
    /// </summary>
    /// <param name="targetId">놓은 분류. null 이면 최상위.</param>
    /// <param name="before">
    /// 참이면 그 분류 <b>앞에</b> 끼우고(형제가 된다), 거짓이면 그 <b>아래로</b>
    /// 넣는다(자식이 된다). 줄의 위쪽 가장자리냐 가운데냐로 갈린다.
    /// </param>
    /// <remarks>
    /// <b>색인은 건드리지 않는다.</b> 색인은 카테고리 단위라 분류를 옮겨도
    /// 내용이 그대로다 — 다시 색인하라고 하지 않는다.
    /// </remarks>
    private async Task DropFolderAsync(long? targetId, bool before)
    {
        // 문서를 끌고 있으면 그쪽 일이다. **줄 사이의 틈(before)에 떨군 것은
        // 버린다** — 그 틈은 「형제로, 그 앞에」라는 뜻이라 문서에는 말이 안 된다.
        if (_dragDoc is { } doc)
        {
            _dragDoc = null;
            _dragOver = null;
            if (!before) await MoveDocumentAsync(doc, targetId);
            return;
        }

        var moving = _dragFolder;
        _dragFolder = null;
        _dragOver = null;

        if (moving is not { } id || _categoryId is null) return;
        if (targetId == id) return;

        long? parent;
        long? beforeId;

        if (targetId is null)
        {
            parent = null;      // 최상위 맨 뒤
            beforeId = null;
        }
        else if (before)
        {
            parent = Find(_tree, targetId.Value) is { } _ ? ParentOf(_tree, targetId.Value) : null;
            beforeId = targetId;
        }
        else
        {
            parent = targetId;  // 그 아래 자식으로
            beforeId = null;
        }

        var ok = await RunAsync(
            async () => await Api.MoveFolderAsync(id, parent, beforeId),
            "분류를 옮겼습니다.");

        if (ok) await LoadTreeAsync();
    }

    private void DragOver(long id, bool before)
    {
        // 문서는 분류 **안으로**만 들어간다. 틈에는 표시를 안 한다.
        if (_dragDoc is not null) { if (!before) _dragOver = (id, false); return; }

        if (_dragFolder is null || _dragFolder == id) return;
        _dragOver = (id, before);
    }

    /// <summary>트리에서 그 분류의 부모를 찾는다. 형제로 끼울 때 쓴다.</summary>
    private static long? ParentOf(IEnumerable<RagFolderNode> nodes, long id, long? parent = null)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return parent;
            if (ParentOf(n.Children, id, n.Id) is { } hit) return hit;
        }

        return null;
    }

    // ── 문서 ────────────────────────────────────────────────────

    /// <summary>한 번에 올릴 수 있는 파일 수. 브라우저가 묶어서 보낸다.</summary>
    private const int MaxFiles = 20;

    /// <summary>파일 하나의 상한. 넘으면 게이트웨이가 먼저 끊는다.</summary>
    private const long MaxBytes = 50L * 1024 * 1024;

    /// <summary>
    /// 지금 올리고 있는 것들. <b>끝나도 지우지 않는다</b> — 건너뛴 파일과 그
    /// 까닭을 사람이 읽을 시간이 있어야 한다. 다음에 올릴 때 비운다.
    /// </summary>
    private readonly List<RagUploadItem> _uploads = [];

    /// <summary>
    /// 진행을 화면에 반영한 마지막 시각. <b>버퍼마다 다시 그리면 회로가 넘친다</b> —
    /// 큰 파일 하나에 수천 번이 된다.
    /// </summary>
    private DateTime _lastTick = DateTime.MinValue;

    private async Task OnFilesAsync(InputFileChangeEventArgs e)
    {
        if (_categoryId is null) return;

        // 올리는 중에 또 떨구는 일이 있다. 조용히 무시하면 **떨궜는데 아무
        // 일도 안 일어난 것처럼** 보여서 사람이 같은 동작을 되풀이한다.
        if (_busy)
        {
            Say("아직 올리는 중입니다. 끝나면 다시 놓아 주세요.", NoticeTone.Warning);
            return;
        }

        var picked = e.GetMultipleFiles(MaxFiles);

        _uploads.Clear();
        foreach (var f in picked)
        {
            _uploads.Add(new RagUploadItem
            {
                FileName = f.Name,
                TotalBytes = f.Size,
                State = f.Size > MaxBytes ? "failed" : "waiting",
                Error = f.Size > MaxBytes ? $"너무 큽니다 ({Bytes(f.Size)}). 50MB 까지." : null,
            });
        }

        _busy = true;

        // **이름과 진행바를 먼저 그린다.** `StateHasChanged` 만으로는 이 처리기가
        // 다음에 기다릴 때까지 화면이 안 바뀌는데, 그 "다음" 이 곧 파일 읽기다 —
        // 큰 파일이면 목록이 한참 뒤에야 나타난다. 한 번 양보해 그리고 시작한다.
        StateHasChanged();
        await Task.Yield();

        try
        {
            // **한 건씩 보낸다.** 묶어 보내면 어디까지 갔는지 알 수 없어
            // 큰 파일에서 화면이 멎은 것처럼 보인다. 하나가 실패해도
            // 나머지는 계속 간다.
            for (var i = 0; i < picked.Count && i < _uploads.Count; i++)
            {
                var file = picked[i];
                var item = _uploads[i];
                if (item.State == "failed") continue;

                item.State = "sending";
                _lastTick = DateTime.MinValue;
                await InvokeAsync(StateHasChanged);

                try
                {
                    await using var stream = file.OpenReadStream(MaxBytes);

                    var saved = await Api.UploadOneAsync(
                        _categoryId, _folderId, stream, file.Name, file.ContentType,
                        sent =>
                        {
                            item.SentBytes = sent;

                            var now = DateTime.UtcNow;
                            if ((now - _lastTick).TotalMilliseconds < 80) return;
                            _lastTick = now;
                            _ = InvokeAsync(StateHasChanged);
                        });

                    item.SentBytes = item.TotalBytes;
                    item.State = "done";
                    item.ServerStatus = saved?.Status;
                    item.Error = saved?.Error;
                }
                catch (Exception ex)
                {
                    item.State = "failed";
                    item.Error = ex.Message;
                }

                await InvokeAsync(StateHasChanged);
            }

            await LoadDocumentsAsync();
            await RefreshCountsAsync();

            var done = _uploads.Count(x => x.State == "done");
            var bad = _uploads.Count(x => x.State == "failed");
            var skipped = _uploads.Count(x => x.ServerStatus == "skipped");

            var msg = $"{done}건을 올렸습니다.";
            if (skipped > 0) msg += $" 그중 {skipped}건은 읽을 수 없는 형식입니다.";
            if (bad > 0) msg += $" {bad}건은 올리지 못했습니다.";

            Say(msg, bad > 0 || skipped > 0 ? NoticeTone.Warning : NoticeTone.Info);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>올리기 목록을 지운다. 다 읽고 치우는 단추다.</summary>
    private void ClearUploads() => _uploads.Clear();

    /// <summary>문서 한 건을 다른 분류로 옮긴다.</summary>
    /// <remarks>
    /// <b>색인을 다시 만들라고 하지 않는다.</b> 분류는 사람이 보기 좋으라고
    /// 있는 것이고 검색은 카테고리 전체에서 한다 — 토막에도 디스크의 자리에도
    /// 분류가 적혀 있지 않다. 분류 자체를 끌어 옮길 때와 같은 까닭이다.
    /// </remarks>
    private async Task MoveDocumentAsync(long docId, long? folderId)
    {
        if (folderId == _folderId) return;   // 보고 있는 그 분류에 떨궜다

        var name = _documents.FirstOrDefault(d => d.Id == docId)?.FileName ?? "문서";
        var to = folderId is { } f ? Find(_tree, f)?.Name ?? "분류" : "분류 미지정";

        var ok = await RunAsync(
            async () => await Api.MoveDocumentAsync(docId, folderId),
            $"「{name}」 을 「{to}」 로 옮겼습니다.");

        if (!ok) return;

        await LoadDocumentsAsync();
        await RefreshCountsAsync();
    }

    /// <summary>고르개가 돌려준 값을 분류 번호로 읽는다. 빈 글자는 분류 미지정.</summary>
    private static long? ParseFolder(object? v)
        => long.TryParse(v?.ToString(), out var n) ? n : null;

    /// <summary>고르개에 세울 분류 목록. 깊이만큼 들여 적는다.</summary>
    /// <remarks>
    /// 끌어 옮기기만 두면 **화면 어디에도 안 보이는 몸짓**이 유일한 길이 된다 —
    /// 키보드로도, 좁은 화면에서도 옮길 수 없다.
    /// </remarks>
    private List<(long? Id, string Label)> FlatFolders()
    {
        var list = new List<(long?, string)> { (null, "분류 미지정") };
        Walk(_tree, 0);
        return list;

        void Walk(List<RagFolderNode> ns, int depth)
        {
            foreach (var n in ns)
            {
                list.Add((n.Id, new string('\u00a0', depth * 3) + n.Name));
                Walk(n.Children, depth + 1);
            }
        }
    }

    private async Task DeleteDocumentAsync(long id)
    {
        if (_confirm is null) return;

        var name = _documents.FirstOrDefault(d => d.Id == id)?.FileName ?? "이 문서";
        if (!await _confirm.AskAsync(
                $"「{name}」 을 지웁니다. 디스크의 파일도 함께 사라집니다.", "문서 지우기"))
        {
            return;
        }

        var ok = await RunAsync(async () => await Api.DeleteDocumentAsync(id), "문서를 지웠습니다.");
        if (!ok) return;

        await LoadDocumentsAsync();
        await RefreshCountsAsync();
    }

    /// <summary>트리에서 분류 하나를 찾는다. 묻는 창에 이름을 적으려고 쓴다.</summary>
    private static RagFolderNode? Find(IEnumerable<RagFolderNode> nodes, long id)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return n;
            if (Find(n.Children, id) is { } hit) return hit;
        }

        return null;
    }

    // ── 색인 ────────────────────────────────────────────────────

    /// <summary>
    /// 카테고리를 통째로 다시 색인한다.
    /// </summary>
    /// <remarks>
    /// <b>올리기만 해서는 찾아지지 않는다.</b> 문서를 넣고 이 단추를 눌러야
    /// 검색에 들어간다 — 올릴 때마다 자동으로 돌리면 열 개를 올릴 때 열 번
    /// 도는데, 한 번에 몇 분씩 걸린다(임베딩이 CPU 로 돈다).
    /// </remarks>
    private async Task ReindexAsync()
    {
        if (_categoryId is null) return;

        _busy = true;
        try
        {
            var r = await Api.ReindexAsync(_categoryId);
            Say(r is null
                ? "색인을 만들지 못했습니다."
                : $"색인을 다시 만들었습니다. 토막 {r.Chunks}개.",
                r is null ? NoticeTone.Error : NoticeTone.Info);

            await LoadDocumentsAsync();
            await RefreshCountsAsync();
        }
        catch (Exception ex)
        {
            Say($"색인을 만들지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    // ── 보여 주기 ───────────────────────────────────────────────

    private static string StatusLabel(string s) => s switch
    {
        "indexed" => "색인됨",
        "pending" => "색인 대기",
        "skipped" => "건너뜀",
        "failed" => "실패",
        _ => s,
    };

    private static string StatusBadge(string s) => s switch
    {
        "indexed" => "jsini-badge--on",
        "pending" => "jsini-badge--warn",
        "failed" => "jsini-badge--err",
        _ => "jsini-badge--off",
    };

    private static string Bytes(long b)
    {
        if (b <= 0) return "-";
        double v = b;
        string[] u = ["B", "KB", "MB", "GB"];
        var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v >= 100 ? $"{v:0} {u[i]}" : $"{v:0.0} {u[i]}";
    }
}
