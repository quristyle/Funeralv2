using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace JSini.Web.Admin.Components.Pages;

public partial class RagDocuments
{
    [Inject] private RagLibraryClient Api { get; set; } = default!;

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

    protected override async Task OnInitializedAsync() => await ReloadAsync();

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
        var byId = flat.ToDictionary(f => f.Id, f => new RagFolderNode { Id = f.Id, Name = f.Name });
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
        await LoadTreeAsync();
        await LoadDocumentsAsync();
    }

    // ── 문서 ────────────────────────────────────────────────────

    /// <summary>한 번에 올릴 수 있는 파일 수. 브라우저가 묶어서 보낸다.</summary>
    private const int MaxFiles = 20;

    /// <summary>파일 하나의 상한. 넘으면 게이트웨이가 먼저 끊는다.</summary>
    private const long MaxBytes = 50L * 1024 * 1024;

    private async Task OnFilesAsync(InputFileChangeEventArgs e)
    {
        if (_categoryId is null) return;

        var picked = e.GetMultipleFiles(MaxFiles);
        var payload = new List<(Stream, string, string?)>();
        var streams = new List<Stream>();

        foreach (var f in picked)
        {
            if (f.Size > MaxBytes)
            {
                Say($"{f.Name} 은 너무 큽니다 (50MB 까지).", NoticeTone.Warning);
                continue;
            }

            var s = f.OpenReadStream(MaxBytes);
            streams.Add(s);
            payload.Add((s, f.Name, f.ContentType));
        }

        if (payload.Count == 0) return;

        _busy = true;
        try
        {
            var saved = await Api.UploadAsync(_categoryId, _folderId, payload);

            var skipped = saved.Where(x => x.Status == "skipped").ToArray();
            var msg = $"{saved.Count}건을 올렸습니다.";
            if (skipped.Length > 0)
            {
                msg += $" 그중 {skipped.Length}건은 읽을 수 없는 형식입니다.";
            }

            Say(msg, skipped.Length > 0 ? NoticeTone.Warning : NoticeTone.Info);
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            Say($"올리지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
            _busy = false;
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
        if (ok) await LoadDocumentsAsync();
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

            _categories = await Api.CategoriesAsync();
            await LoadDocumentsAsync();
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
