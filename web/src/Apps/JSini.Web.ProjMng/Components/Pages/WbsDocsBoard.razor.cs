using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsDocsBoard : IAsyncDisposable
{
    [Inject] private WbsDocsClient Api { get; set; } = default!;
    [Inject] private WbsDevUserClient Prefs { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private const string SeenPref = "docs.seen";
    private const int MaxUploadFiles = 30;
    private const long MaxUploadBytes = 8L * 1024 * 1024;

    /// <summary>이보다 긴 글은 붙인 문서가 있을 때 접어 둔다 — 문서 한 벌을 통째로 붙여 넣은 쪽(1.7MB)이 있어, 그대로 두면 붙인 문서까지 수만 px 를 내려야 한다.</summary>
    private const int LongContent = 6000;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    private string? _projectName;
    private string? _projectCode;

    private IReadOnlyList<WbsDocDto> _docs = [];
    private IReadOnlyList<WbsDocDto> _trash = [];
    private Dictionary<int, List<WbsDocFileDto>> _files = [];
    private int? _selectedId;

    private bool _editing;
    private string? _draftTitle;
    private string? _draftContent;
    private bool _expanded;

    private bool _newOpen;
    private string? _newTitle;

    private bool _showTrash;
    private WbsDocDto? _undo;
    private CancellationTokenSource? _undoTimer;

    /// <summary>쪽마다 보던 탭. 쪽을 오갈 때 보던 문서를 기억한다.</summary>
    private readonly Dictionary<int, int> _tabOf = [];

    private bool _uploadOpen;
    private IReadOnlyList<PickedFile> _picked = [];
    private FilePicker? _picker;

    private bool _linkOpen;
    private string? _linkUrl;
    private string? _linkName;

    private bool _busy;

    private ConfirmDialog? _confirm;

    private ElementReference _tocEl;
    private IJSObjectReference? _module;
    private IJSObjectReference? _tocDrag;
    private DotNetObjectReference<WbsDocsBoard>? _self;
    private bool _attaching;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private WbsDocDto? Current => _docs.FirstOrDefault(d => d.Id == _selectedId);

    private IReadOnlyList<WbsDocFileDto> FilesOf(int docId) =>
        _files.TryGetValue(docId, out var list) ? list : [];

    private IReadOnlyList<WbsDocFileDto> CurrentFiles => Current is { } d ? FilesOf(d.Id) : [];

    /// <summary>보고 있는 문서. 기억해 둔 탭이 사라졌으면 첫 장으로 돌아간다.</summary>
    private WbsDocFileDto? CurrentFile
    {
        get
        {
            var list = CurrentFiles;
            if (list.Count == 0 || Current is not { } d) return null;

            return _tabOf.TryGetValue(d.Id, out var want)
                ? list.FirstOrDefault(f => f.Id == want) ?? list[0]
                : list[0];
        }
    }

    /// <summary>탭 이름. 이름이 같은 장이 둘 이상이면 올린 때를 덧붙인다 — 고친 파일을 같은 이름으로 다시 올리는 일이 흔해, 글자까지 같으면 고를 수가 없다.</summary>
    private Dictionary<int, string> TabLabels
    {
        get
        {
            var list = CurrentFiles;
            var dup = list.GroupBy(f => f.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            return list.ToDictionary(
                f => f.Id,
                f => dup.Contains(f.Name) ? $"{f.Name} ({f.UploadedAt ?? $"#{f.Id}"})" : f.Name ?? $"#{f.Id}");
        }
    }

    private bool CanClip => CurrentFiles.Count > 0 && (Current?.Content?.Length ?? 0) > LongContent;

    private bool Clipped => CanClip && !_expanded;

    private string FrameSrc(WbsDocFileDto file) =>
        file.IsLink ? file.Url! : FileDownload.WbsDocUrlFor(ProjectRid ?? 0, file.DocId, file.Id);

    /// <summary>고치던 것이 남아 있나. 제목과 본문을 둘 다 본다 — 제목만 고치고 옮기는 일이 잦다.</summary>
    private bool IsDirty =>
        _editing && Current is { } doc
        && (doc.Title != _draftTitle || (doc.Content ?? "") != (_draftContent ?? ""));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 목차는 분할판(DxSplitter) 안에 있어 첫 그림에 아직 안 섰을 수 있다 — 판이 선 그림에서 건다.
        if (_tocDrag is not null || _attaching || _tocEl.Context is null) return;
        _attaching = true;

        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/wbs-docs.js");
            _self ??= DotNetObjectReference.Create(this);
            _tocDrag = await _module.InvokeAsync<IJSObjectReference>("attachToc", _tocEl, _self);
        }
        catch (JSDisconnectedException)
        {
        }
        finally
        {
            _attaching = false;
        }
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _docs = [];
            _trash = [];
            _files = [];
            _selectedId = null;
            _seen = null;
            _base = null;
            return 0;
        }

        await ReloadAsync(rid);
        await LoadSeenAsync(rid);
        MarkSeen(_selectedId);

        return _docs.Count;
    }, "문서가 없습니다.", "문서를 읽지 못했습니다");

    /// <summary>쪽 · 붙인 문서 · 휴지통을 한 번에 다시 읽는다. 보던 쪽이 남아 있으면 그대로 둔다 — 저장할 때마다 첫 쪽으로 튕기면 고친 결과를 볼 수 없다.</summary>
    private async Task ReloadAsync(int rid)
    {
        var docs = Api.ListAsync(rid);
        var files = Api.FilesAsync(rid);
        var trash = Api.TrashAsync(rid);

        _docs = await docs;
        _files = (await files).GroupBy(f => f.DocId).ToDictionary(g => g.Key, g => g.ToList());

        try
        {
            _trash = await trash;
        }
        catch (ApiException)
        {
            // 휴지통을 못 읽어도 본 화면은 그대로 쓴다.
            _trash = [];
        }

        if (!_docs.Any(d => d.Id == _selectedId))
        {
            _selectedId = _docs.FirstOrDefault()?.Id;
        }
    }

    /// <summary>다른 쪽으로 옮긴다. 고치던 것이 있으면 먼저 묻는다 — 안 물으면 쓰던 글이 말없이 사라진다.</summary>
    private async Task SelectAsync(int id)
    {
        if (id == _selectedId) return;

        if (IsDirty && _confirm is not null)
        {
            var ok = await _confirm.AskAsync(
                "고치던 내용이 저장되지 않았습니다. 그래도 옮길까요?",
                "저장하지 않고 옮기기", "옮긴다", ButtonRenderStyle.Secondary);

            if (!ok) return;
        }

        _editing = false;
        _expanded = false;
        _selectedId = id;
        MarkSeen(id);
    }

    private void StartEdit()
    {
        if (Current is not { } doc) return;

        _draftTitle = doc.Title;
        _draftContent = doc.Content;
        _editing = true;
    }

    private void CancelEdit() => _editing = false;

    private async Task SaveAsync()
    {
        if (ProjectRid is not int rid || Current is not { } doc) return;

        if (string.IsNullOrWhiteSpace(_draftTitle))
        {
            Say("제목은 필수입니다.", NoticeTone.Warning);
            return;
        }

        var saved = await RunAsync(
            () => Api.UpdateAsync(rid, new WbsDocDto
            {
                Id = doc.Id,
                Title = _draftTitle,
                Content = _draftContent ?? "",
                SortOrder = doc.SortOrder,
            }),
            "저장했습니다.", "저장하지 못했습니다");

        if (!saved) return;

        _editing = false;
        await ReloadAsync(rid);

        // 내가 방금 쓴 것이 나에게 「변경됨」으로 보이면 안 된다.
        MarkSeen(doc.Id, alsoShown: true);
    }

    private void StartNew()
    {
        _newTitle = null;
        _newOpen = true;
    }

    /// <summary>제목만 받아 만들고 바로 편집으로 들어간다.</summary>
    private async Task CreateAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_newTitle))
        {
            Say("제목을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var id = 0;

        var made = await RunAsync(
            async () => id = await Api.CreateAsync(rid, new WbsDocDto
            {
                Title = _newTitle.Trim(),
                Content = "",

                // 맨 뒤에 붙인다. 0 으로 두면 서버가 100 을 주는데, 100 짜리가 이미 있으면 차례가 겹친다.
                SortOrder = _docs.Count == 0 ? 100 : _docs.Max(d => d.SortOrder) + 10,
            }),
            "만들었습니다.", "만들지 못했습니다");

        if (!made) return;

        _newOpen = false;
        _selectedId = id;
        await ReloadAsync(rid);

        // 내가 만든 쪽이 나에게 「새 쪽」으로 보이면 안 된다.
        MarkSeen(id, alsoShown: true);
        StartEdit();
    }

    /// <summary>휴지통으로 보낸다. 지운 뒤에는 이웃 쪽을 고르고, 잠깐 「되돌리기」 띠를 띄운다.</summary>
    private async Task DeleteAsync()
    {
        if (ProjectRid is not int rid || Current is not { } doc || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"「{doc.Title}」 쪽을 휴지통으로 보냅니다.\n휴지통에서 되돌릴 수 있습니다.", "쪽 삭제");

        if (!ok) return;

        var index = _docs.ToList().FindIndex(d => d.Id == doc.Id);
        var next = _docs.ElementAtOrDefault(index + 1) ?? _docs.ElementAtOrDefault(index - 1);

        var gone = await RunAsync(
            () => Api.DeleteAsync(rid, doc.Id), "휴지통으로 보냈습니다.", "지우지 못했습니다");

        if (!gone) return;

        _editing = false;
        _selectedId = next?.Id;
        ShowUndo(doc);

        await ReloadAsync(rid);
    }

    /// <summary>띠는 잠깐만 둔다 — 놓쳐도 휴지통에 그대로 있다.</summary>
    private void ShowUndo(WbsDocDto doc)
    {
        _undo = doc;
        _undoTimer?.Cancel();
        _undoTimer = new CancellationTokenSource();

        var token = _undoTimer.Token;

        _ = Task.Delay(TimeSpan.FromSeconds(12), token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;

            _ = InvokeAsync(() =>
            {
                if (_undo?.Id == doc.Id) _undo = null;
                StateHasChanged();
            });
        }, TaskScheduler.Default);
    }

    private async Task RestoreAsync(int id)
    {
        if (ProjectRid is not int rid) return;

        var ok = await RunAsync(() => Api.RestoreAsync(rid, id), "되돌렸습니다.", "되돌리지 못했습니다");
        if (!ok) return;

        _undo = null;
        _selectedId = id;
        await ReloadAsync(rid);

        if (_trash.Count == 0) _showTrash = false;
    }

    private async Task PurgeAsync(WbsDocDto doc)
    {
        if (ProjectRid is not int rid || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"「{doc.Title}」 을 완전히 지웁니다. 붙인 문서도 함께 사라지며 되돌릴 수 없습니다.", "완전 삭제");

        if (!ok) return;

        var gone = await RunAsync(() => Api.PurgeAsync(rid, doc.Id), "완전히 지웠습니다.", "지우지 못했습니다");
        if (!gone) return;

        if (_undo?.Id == doc.Id) _undo = null;
        await ReloadAsync(rid);

        if (_trash.Count == 0) _showTrash = false;
    }

    // ── 목차 끌어 옮기기 ───────────────────────────────────────

    /// <summary>놓은 자리로 옮긴다. 화면을 먼저 옮기고 서버가 거절하면 되돌린다 — 기다리면 놓은 것이 돌아갔다 다시 가는 것처럼 보인다.</summary>
    [JSInvokable]
    public async Task MoveDoc(int fromId, int toId, bool after)
    {
        if (ProjectRid is not int rid || _editing) return;

        var list = _docs.ToList();
        var moved = list.FirstOrDefault(d => d.Id == fromId);
        if (moved is null) return;

        list.Remove(moved);

        var at = list.FindIndex(d => d.Id == toId);
        if (at < 0) return;

        list.Insert(after ? at + 1 : at, moved);

        var before = _docs;
        _docs = list;
        StateHasChanged();

        try
        {
            await Api.ReorderAsync(rid, list.Select(d => d.Id));

            for (var i = 0; i < list.Count; i++) list[i].SortOrder = (i + 1) * 10;
        }
        catch (ApiException ex)
        {
            _docs = before;
            Say($"순서를 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }

        StateHasChanged();
    }

    // ── 안 본 변경 ──────────────────────────────────────────────
    // 「내가 마지막으로 본 뒤에 바뀌었나」를 사람마다 따로 기억한다. 굳히는 값(_seen)과 그리는 값(_base)을 가른다 —
    // 고른 즉시 표시를 지우면 지금 보는 쪽에서 무엇이 바뀌었는지 확인할 수가 없다. 다음에 열면 없어진다.

    private enum SeenMark { None, New, Changed }

    private Dictionary<string, string>? _seen;
    private Dictionary<string, string>? _base;
    private CancellationTokenSource? _seenSave;

    private int Unseen => _docs.Count(d => MarkOf(d) != SeenMark.None);

    private SeenMark MarkOf(WbsDocDto doc)
    {
        if (_base is null) return SeenMark.None;
        if (!_base.TryGetValue(doc.Id.ToString(), out var at)) return SeenMark.New;

        return at == (doc.UpdatedAt ?? "") ? SeenMark.None : SeenMark.Changed;
    }

    private async Task LoadSeenAsync(int rid)
    {
        Dictionary<string, string>? stored = null;

        try
        {
            var pref = await Prefs.GetPrefAsync(rid, SeenPref);

            if (!string.IsNullOrWhiteSpace(pref?.Value))
            {
                stored = JsonSerializer.Deserialize<Dictionary<string, string>>(pref.Value);
            }
        }
        catch (Exception ex) when (ex is ApiException or JsonException)
        {
            // 못 읽으면 처음 쓰는 사람처럼 기준선을 깐다.
        }

        if (stored is null)
        {
            // 처음 쓰는 사람 — 지금 있는 것을 전부 본 것으로 깔고 그 뒤의 변경만 표시한다. 안 깔면 전부가 「새 쪽」이다.
            stored = _docs.ToDictionary(d => d.Id.ToString(), d => d.UpdatedAt ?? "");
            _seen = stored;
            _base = new(stored);
            QueueSeenSave();
            return;
        }

        _seen = stored;
        _base = new(stored);
    }

    /// <summary>보고 있는 것을 「봤다」로 굳힌다. alsoShown 이면 화면 표시도 지운다 — 내가 쓴 것이 나에게 변경으로 보이면 안 된다.</summary>
    private void MarkSeen(int? id, bool alsoShown = false)
    {
        if (_seen is null || id is null || _docs.FirstOrDefault(d => d.Id == id) is not { } doc) return;

        var key = doc.Id.ToString();
        var at = doc.UpdatedAt ?? "";

        if (alsoShown && _base is not null) _base[key] = at;
        if (_seen.TryGetValue(key, out var was) && was == at) return;

        _seen[key] = at;
        QueueSeenSave();
    }

    private void MarkAllSeen()
    {
        if (_seen is null) return;

        foreach (var d in _docs) _seen[d.Id.ToString()] = d.UpdatedAt ?? "";

        _base = new(_seen);
        QueueSeenSave();
    }

    /// <summary>서버에는 몰아서 한 번만 — 목차를 훑을 때마다 보내면 쓸데없이 잦다.</summary>
    private void QueueSeenSave()
    {
        if (ProjectRid is not int rid || _seen is null) return;

        _seenSave?.Cancel();
        _seenSave = new CancellationTokenSource();

        var token = _seenSave.Token;
        var snapshot = new Dictionary<string, string>(_seen);

        _ = Task.Delay(1500, token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;

            try
            {
                await Prefs.SetPrefAsync(rid, SeenPref, snapshot);
            }
            catch (ApiException)
            {
                // 못 담으면 이번 방문 동안만 맞는다.
            }
        }, TaskScheduler.Default);
    }

    // ── HTML 로 내려받기 ───────────────────────────────────────
    // 혼자서도 열리는 파일이 되도록 문서 껍데기와 화면과 같은 모양의 style 을 둘러 준다 — 메일로 돌거나 혼자 열리므로 포털 변수를 못 쓴다.

    private const string DownloadCss = """
          body { margin: 0; padding: 28px 32px; background: #fff; color: #1c2024;
                 font: 13px/1.65 -apple-system, "Segoe UI", "맑은 고딕", Malgun Gothic, sans-serif; }
          .doc { max-width: 920px; margin: 0 auto; }
          h1 { font-size: 18px; margin: 0 0 4px; }
          .meta { font-size: 11.5px; color: #6b7480; margin: 0 0 18px; border-bottom: 1px solid #e3e6ea; padding-bottom: 10px; }
          .meta span + span::before { content: " | "; color: #c9cdd3; }
          h3 { font-size: 13.5px; margin: 14px 0 6px; padding-bottom: 3px; border-bottom: 1px solid #e3e6ea; }
          p { margin: 5px 0; }
          ul, ol { margin: 5px 0; padding-left: 22px; }
          a { color: #2f6fd0; }
          table { border-collapse: collapse; width: 100%; table-layout: fixed; margin: 8px 0; font-size: 12.5px; }
          th, td { border: 1px solid #e3e6ea; padding: 4px 7px; vertical-align: top; word-break: break-word; }
          th { background: #f6f7f9; font-weight: 600; text-align: left; }
          code { background: #eef1f4; border-radius: 3px; padding: 1px 5px; font-family: Consolas, "Courier New", monospace; font-size: 12px; }
          pre { background: #f5f6f8; border: 1px solid #e3e6ea; border-radius: 4px; padding: 8px 10px; overflow-x: auto;
                font-family: Consolas, "Courier New", monospace; font-size: 12px; }
          img { max-width: 100%; }
          @media print { body { padding: 0; } }
        """;

    private async Task DownloadAsync()
    {
        if (Current is not { } d || _module is null) return;

        static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        var meta = new List<string>
        {
            string.IsNullOrEmpty(d.CreatedAt)
                ? "작성 기록 없음"
                : $"작성 {d.CreatedAt}{(string.IsNullOrEmpty(d.CreatedBy) ? "" : $" · {d.CreatedBy}")}",
        };

        if (!string.IsNullOrEmpty(d.UpdatedAt))
        {
            meta.Add($"수정 {d.UpdatedAt}{(string.IsNullOrEmpty(d.UpdatedBy) ? "" : $" · {d.UpdatedBy}")}");
        }

        var html = new StringBuilder()
            .Append("<!doctype html>\n<html lang=\"ko\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append($"<title>{Esc(d.Title)}</title>\n<style>{DownloadCss}</style>\n</head>\n<body>\n")
            .Append($"<div class=\"doc\">\n<h1>{Esc(d.Title)}</h1>\n")
            .Append($"<p class=\"meta\">{string.Concat(meta.Select(m => $"<span>{Esc(m)}</span>"))}</p>\n")
            .Append(string.IsNullOrWhiteSpace(d.Content) ? "<p>내용 없음</p>" : d.Content)
            .Append("\n</div>\n</body>\n</html>\n")
            .ToString();

        var stamp = AppTime.UtcNow.Kst("yyyyMMdd_HHmm");

        try
        {
            await _module.InvokeVoidAsync("save", $"{SafeName(d.Title)}_{stamp}.html", html);
        }
        catch (JSException ex)
        {
            Say($"내려받지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    /// <summary>제목을 파일 이름으로 쓸 수 있게 다듬는다 — 윈도에서 막히는 글자만 바꾼다.</summary>
    private static string SafeName(string? title)
    {
        var bad = "\\/:*?\"<>|";
        var cleaned = new string((title ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray());
        cleaned = string.Join(' ', cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return cleaned.Length == 0 ? "문서" : cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }

    // ── 붙인 문서 ──────────────────────────────────────────────

    private void OpenUpload()
    {
        _picked = [];
        _uploadOpen = true;
    }

    /// <summary>한 장씩 올린다. 섞여 들어온 것(HTML 이 아니거나 너무 큰 것)은 서버가 거절하고, 나머지는 올라간 채로 무엇이 빠졌는지만 알린다.</summary>
    private async Task UploadAsync()
    {
        if (ProjectRid is not int rid || Current is not { } doc || _picked.Count == 0) return;

        _busy = true;
        var added = new List<WbsDocFileDto>();
        var skipped = new List<string>();

        try
        {
            foreach (var p in _picked)
            {
                try
                {
                    string html;

                    await using (var s = p.OpenRead())
                    using (var reader = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        html = await reader.ReadToEndAsync();
                    }

                    if (await Api.UploadAsync(rid, doc.Id, p.Name, html) is { } f) added.Add(f);
                }
                catch (ApiException ex)
                {
                    skipped.Add($"{p.Name} ({ex.Message})");
                }
            }
        }
        finally
        {
            _busy = false;
        }

        if (added.Count > 0)
        {
            await ReloadAsync(rid);

            // 방금 올린 것을 보여 준다. 올려 놓고 탭을 찾아 누르게 하지 않는다.
            _tabOf[doc.Id] = added[^1].Id;
        }

        if (skipped.Count > 0)
        {
            Say($"올리지 못한 파일: {string.Join(" · ", skipped)}", NoticeTone.Error);
        }
        else
        {
            Say($"{added.Count}장을 붙였습니다.");
        }

        _picked = [];
        if (_picker is not null) await _picker.ClearAsync();
        _uploadOpen = false;
    }

    private void OpenLink()
    {
        _linkUrl = null;
        _linkName = null;
        _linkOpen = true;
    }

    private async Task LinkAsync()
    {
        if (ProjectRid is not int rid || Current is not { } doc || string.IsNullOrWhiteSpace(_linkUrl)) return;

        _busy = true;
        WbsDocFileDto? made = null;

        var ok = await RunAsync(
            async () => made = await Api.LinkAsync(rid, doc.Id, _linkUrl.Trim(), _linkName?.Trim()),
            "주소를 걸었습니다.", "주소를 걸지 못했습니다");

        _busy = false;
        if (!ok) return;

        _linkOpen = false;
        await ReloadAsync(rid);

        if (made is not null) _tabOf[doc.Id] = made.Id;
    }

    /// <summary>한 장만 내린다. 묻지 않는다 — 글도 다른 장도 그대로 남고, 잘못 눌렀으면 다시 올리면 된다.</summary>
    private async Task RemoveFileAsync(WbsDocFileDto file)
    {
        if (ProjectRid is not int rid) return;

        var ok = await RunAsync(
            () => Api.RemoveFileAsync(rid, file.DocId, file.Id),
            file.IsLink ? "연결을 내렸습니다." : "문서를 내렸습니다.", "내리지 못했습니다");

        if (!ok) return;

        _tabOf.Remove(file.DocId);
        await ReloadAsync(rid);
    }

    /// <summary>보고 있는 탭을 왼쪽·오른쪽으로 한 칸 옮긴다.</summary>
    private async Task MoveFileAsync(int step)
    {
        if (ProjectRid is not int rid || Current is not { } doc || CurrentFile is not { } file) return;

        var list = CurrentFiles.ToList();
        var i = list.FindIndex(f => f.Id == file.Id);
        var j = i + step;

        if (i < 0 || j < 0 || j >= list.Count) return;

        (list[i], list[j]) = (list[j], list[i]);
        _files[doc.Id] = list;

        try
        {
            await Api.ReorderFilesAsync(rid, doc.Id, list.Select(f => f.Id));
        }
        catch (ApiException ex)
        {
            Say($"차례를 바꾸지 못했습니다 — {ex.Message}", NoticeTone.Error);
            await ReloadAsync(rid);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _undoTimer?.Cancel();

        try
        {
            if (_tocDrag is not null)
            {
                await _tocDrag.InvokeVoidAsync("dispose");
                await _tocDrag.DisposeAsync();
            }

            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }

        _self?.Dispose();
    }
}
