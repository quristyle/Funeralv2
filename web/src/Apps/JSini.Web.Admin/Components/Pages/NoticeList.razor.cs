using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class NoticeList
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private NoticeUploadClient Uploads { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_keyword);

    private string? _keyword;
    private IReadOnlyList<NoticeDto> _notices = [];

    /// <summary>미리보기로 띄운 공지. 한 건이지만 화면이 목록을 받으므로 목록으로 넘긴다.</summary>
    private IReadOnlyList<NoticeDto> _preview = [];

    private bool _previewing;

    /// <summary>지금 편집 중인 공지에 이미 매달려 있는 첨부. 편집 모델의 Files 를 그대로 쓰지 않고 복사해 둔다 — 그쪽을 직접 고치면 저장을 취소해도 표의 자료가 이미 바뀌어 있다.</summary>
    private readonly List<NoticeFileDto> _attached = [];

    /// <summary>이번에 새로 고른 파일. 저장할 때 올린다.</summary>
    private IReadOnlyList<PickedFile> _picked = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>「조회」와 저장·삭제 뒤가 부른다. 거르기는 서버가 한다.</summary>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _notices = await Api.GetNoticesAsync(_keyword);
        return _notices.Count;
    }, "조건에 맞는 공지가 없습니다.", "공지 목록을 읽지 못했습니다");

    /// <summary>조건을 비우고 전체를 다시 읽는다.</summary>
    private Task ResetAsync()
    {
        _keyword = null;
        return ReloadAsync();
    }

    /// <summary>새 공지의 기본값. 팝업은 켜고 전체 공개는 끈다 — 전체 공개는 로그인하지 않은 사람에게도 보이므로 켜는 쪽을 기본으로 두면 안 된다.</summary>
    private void FillNew(NoticeDto n)
    {
        n.IsPopup = true;
        n.IsPublic = false;
        n.Status = 1;
        n.OrderNo = 0;
    }

    /// <summary>편집 창이 열릴 때 첨부 상태를 그 공지의 것으로 갈아 끼운다. 등록일 때도 부른다 — 안 그러면 앞선 편집의 첨부가 새 공지에 남의 첨부로 붙는다.</summary>
    private void OnEditOpen(NoticeDto n, bool isNew)
    {
        _attached.Clear();

        // 고른 파일은 FilePicker 가 임시 파일과 함께 들고 있다 — 창이 닫히면 부품이 지우므로 여기서는 참조만 둔다.
        _picked = [];

        if (!isNew)
        {
            _attached.AddRange(n.Files.OrderBy(f => f.SortNo));
        }
    }

    /// <summary>저장. 두 단계다 — 새 파일을 먼저 올리고 그 아이디를 공지에 매단다. 올리다 실패하면 공지를 건드리지 않고 멈추며, 어디까지 갔는지를 말한다.</summary>
    private async Task SaveAsync((NoticeDto Item, bool IsNew) e)
    {
        var files = new List<SaveNoticeFileDto>(_attached.Count + _picked.Count);
        var sort = 0;

        // ① 이미 매달린 것. **함께 실어야 지워지지 않는다** (머리말 참고).
        foreach (var file in _attached)
        {
            files.Add(new SaveNoticeFileDto
            {
                FileId = file.FileId,
                FileName = file.FileName,
                FileSize = file.FileSize,
                ContentType = file.ContentType,
                SortNo = sort++,
            });
        }

        // ② 새로 고른 것.
        var uploaded = 0;

        foreach (var pick in _picked)
        {
            UploadedNoticeFile? result;

            try
            {
                await using var stream = pick.OpenRead();
                result = await Uploads.UploadAsync(stream, pick.Name, pick.ContentType);
            }
            catch (Exception ex) when (ex is not ApiException)
            {
                // ApiException 으로 감싼다 — CommGrd 의 RunAsync 는 그것만 받아 안내 줄로 바꾸고, 다른 예외는 회로를 끊는다.
                throw Failed(pick.Name, uploaded, ex.Message, ex);
            }

            if (result is null || string.IsNullOrWhiteSpace(result.Key))
            {
                throw Failed(pick.Name, uploaded, "서버가 파일 아이디를 주지 않았습니다");
            }

            uploaded++;

            files.Add(new SaveNoticeFileDto
            {
                FileId = result.Key,
                FileName = pick.Name,
                FileSize = result.FileSize ?? pick.Size,
                ContentType = pick.ContentType,
                SortNo = sort++,
            });
        }

        var body = new SaveNoticeDto
        {
            Title = e.Item.Title,
            Content = e.Item.Content,
            IsPopup = e.Item.IsPopup,
            IsPublic = e.Item.IsPublic,
            OrderNo = e.Item.OrderNo,
            Status = e.Item.Status,
            StartAt = e.Item.StartAt,
            EndAt = e.Item.EndAt,
            Files = files,
        };

        // ③ 공지를 저장한다 — 서버가 이 시점에 첨부의 공개 여부를 함께 맞춘다(PublicFileSyncService).
        await (e.IsNew
            ? Api.CreateNoticeAsync(body)
            : Api.UpdateNoticeAsync(e.Item.Id, body));
    }

    /// <summary>첨부 올리기가 실패했을 때의 안내. 어디까지 갔는지 말한다 — 「저장하지 못했습니다」로만 끝내면 앞서 올라간 파일이 주인 없이 남는다.</summary>
    private static ApiException Failed(string fileName, int uploaded, string reason, Exception? inner = null) =>
        new($"{fileName} 을(를) 올리지 못했습니다"
            + (uploaded > 0 ? $" (앞의 {uploaded}개는 이미 올라갔습니다)" : string.Empty)
            + $" — {reason}",
            innerException: inner);

    private Task DeleteAsync(NoticeDto n) => Api.DeleteNoticeAsync(n.Id);

    /// <summary>공지 하나를 사용자가 볼 화면 그대로 띄운다. 목록을 새로 만들어 넘긴다 — 같은 배열을 고쳐 쓰면 NoticePopup 이 바뀐 것을 알아채지 못한다.</summary>
    private void Preview(NoticeDto n)
    {
        _preview = [n];
        _previewing = true;
    }

    /// <summary>사람이 읽는 크기.</summary>
    private static string Human(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };
}
