using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class ArchiveListPage
{
    [Inject] private HelpApi Api { get; set; } = default!;
    [Inject] private FileUploadClient Files { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.Or(_category));

    private string? _keyword;
    private string? _category;

    private IReadOnlyList<Archive> _items = [];
    private IReadOnlyList<string> _categories = [];

    /// <summary>서버가 「이 사람은 고칠 수 있다」고 한 경우에만 참.</summary>
    private bool _canManage;

    private bool _editing;
    private bool _isNew;
    private Archive _edit = new();
    private bool _saving;

    // ── 첨부 (D5) ───────────────────────────────────────────

    /// <summary>
    /// 지금 이 자료에 매달려 있는 파일. <b>저장할 때 이대로 다시 보낸다</b> —
    /// 서버가 보낸 목록을 최종으로 삼아 빠진 것을 지우기 때문이다.
    /// </summary>
    private List<ArchiveFile> _attached = [];

    private FilePicker? _picker;
    private IReadOnlyList<PickedFile> _picked = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var list = await Api.GetArchiveListAsync(_category, _keyword);

        _items = list.Items;
        _categories = list.Categories;
        _canManage = list.CanManage;

        return _items.Count;
    }, "등록된 자료가 없습니다.", "자료실을 읽지 못했습니다");

    private void StartNew()
    {
        _isNew = true;
        _edit = new Archive
        {
            Category = _category,
            Status = 1,
            OrderNo = _items.Count == 0 ? 1 : _items.Max(a => a.OrderNo) + 1,
        };
        _attached = [];
        _picked = [];
        _editing = true;
    }

    /// <summary>복사본을 띄운다. 원본을 묶으면 취소해도 표에 바뀐 값이 남는다.</summary>
    private void StartEdit(Archive a)
    {
        _isNew = false;
        _edit = new Archive
        {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            Category = a.Category,
            OrderNo = a.OrderNo,
            Status = a.Status,
        };

        // 첨부도 복사본이다. 원본 목록을 그대로 쓰면 창에서 「빼기」를 누른 것이
        // 저장하지 않아도 표에 반영된다.
        _attached = [.. a.Files];
        _picked = [];
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_edit.Title))
        {
            Say("자료명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        _saving = true;
        try
        {
            // ① 새로 고른 파일부터 FileServer 로. 실패하면 자료를 건드리지 않는다 —
            //    반쯤 저장된 상태를 만들지 않는 편이 낫다.
            var files = new List<object>(_attached.Count + _picked.Count);
            var sort = 0;

            foreach (var file in _attached)
            {
                files.Add(new
                {
                    fileId = file.FileId,
                    fileName = file.FileName,
                    fileSize = file.FileSize,
                    contentType = file.ContentType,
                    sortNo = sort++,
                });
            }

            foreach (var pick in _picked)
            {
                UploadedFile? uploaded = null;

                var sent = await RunAsync(async () =>
                {
                    await using var stream = pick.OpenRead();
                    uploaded = await Files.UploadAsync(stream, pick.Name, pick.ContentType);
                }, string.Empty, $"{pick.Name} 을(를) 올리지 못했습니다");

                if (!sent || uploaded is null)
                {
                    return;
                }

                files.Add(new
                {
                    fileId = uploaded.FileId ?? uploaded.Id,
                    fileName = pick.Name,
                    fileSize = uploaded.FileSize ?? pick.Size,
                    contentType = pick.ContentType,
                    sortNo = sort++,
                });
            }

            // ② 자료를 저장한다. **`files` 는 보낸 그대로가 최종이다** —
            //    이미 매달린 것을 함께 실어야 지워지지 않는다(머리말 참고).
            var body = new
            {
                title = _edit.Title,
                description = _edit.Description,
                category = _edit.Category,
                orderNo = _edit.OrderNo,
                status = _edit.Status,
                files,
            };

            var saved = await RunAsync(
                () => _isNew ? Api.CreateArchiveAsync(body) : Api.UpdateArchiveAsync(_edit.Id, body),
                string.Empty,
                _isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

            if (!saved)
            {
                return;
            }

            _editing = false;
            if (_picker is not null)
            {
                await _picker.ClearAsync();
            }

            // 다시 읽는 것이 먼저다 — ReloadAsync 가 안내 줄을 비운다.
            await ReloadAsync();
            Say(_isNew ? "등록했습니다." : "저장했습니다.");
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(Archive a)
    {
        if (await RunAsync(() => Api.DeleteArchiveAsync(a.Id), "지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private static string FormatSize(long? bytes)
    {
        if (bytes is null or 0) return string.Empty;
        var mb = bytes.Value / 1024d / 1024d;
        return mb >= 1 ? $"{mb:0.0} MB" : $"{Math.Round(bytes.Value / 1024d)} KB";
    }
}
