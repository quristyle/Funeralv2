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

    /// <summary>
    /// 지금 편집 중인 공지에 <b>이미 매달려 있는</b> 첨부.
    ///
    /// <para>
    /// 편집 모델의 <c>Files</c> 를 그대로 쓰지 않고 복사해 둔다. 그쪽을 직접
    /// 고치면 저장을 취소해도 표에 남은 자료가 이미 바뀌어 있다 — DevExpress 가
    /// 편집 모델을 얕게 복사하므로 목록 알맹이는 원본과 같은 것이다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 새 공지의 기본값.
    ///
    /// 팝업은 켜고 전체 공개는 끈다 — 옛 화면과 같다. 전체 공개는 로그인하지
    /// 않은 사람에게도 보이는 것이라 **켜는 쪽을 기본으로 두면 안 된다.**
    /// </summary>
    private void FillNew(NoticeDto n)
    {
        n.IsPopup = true;
        n.IsPublic = false;
        n.Status = 1;
        n.OrderNo = 0;
    }

    /// <summary>
    /// 편집 창이 열릴 때 첨부 상태를 그 공지의 것으로 갈아 끼운다.
    ///
    /// <para>
    /// <b>등록일 때도 부른다.</b> 안 그러면 앞선 편집에서 보던 첨부 목록이
    /// 그대로 남아 새 공지에 남의 첨부가 붙는다 — 저장을 눌러야 드러난다.
    /// </para>
    /// </summary>
    private void OnEditOpen(NoticeDto n, bool isNew)
    {
        _attached.Clear();

        // 고른 파일은 FilePicker 가 자기 임시 파일과 함께 들고 있다. 창이
        // 닫히면 그 부품도 사라지며 지우므로 여기서는 참조만 놓는다.
        _picked = [];

        if (!isNew)
        {
            _attached.AddRange(n.Files.OrderBy(f => f.SortNo));
        }
    }

    /// <summary>
    /// 저장. <b>두 단계다</b> — 새 파일을 먼저 올리고, 그 아이디를 공지에 매단다.
    ///
    /// <para>
    /// 올리다 실패하면 공지를 건드리지 않고 멈춘다. 반쯤 저장된 상태를 만들지
    /// 않는 편이 낫다. 대신 <b>어디까지 갔는지</b>를 말한다 — 「저장하지
    /// 못했습니다」로 뭉뚱그리면 사용자가 같은 파일을 몇 번이고 다시 올려
    /// 주인 없는 파일만 쌓인다.
    /// </para>
    /// </summary>
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
                // **ApiException 으로 감싼다.** CommGrd 가 저장을 감싸는
                // `DataPage.RunAsync` 는 그것만 받아 안내 줄로 바꾼다 —
                // 다른 예외는 밖으로 나가 회로를 통째로 끊는다(화면이 굳는다).
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

        // ③ 공지를 저장한다. 서버가 이 시점에 첨부의 공개 여부를 함께 맞춘다
        //    (PublicFileSyncService · D-S10).
        await (e.IsNew
            ? Api.CreateNoticeAsync(body)
            : Api.UpdateNoticeAsync(e.Item.Id, body));
    }

    /// <summary>
    /// 첨부 올리기가 실패했을 때의 안내.
    ///
    /// <para>
    /// <b>어디까지 갔는지 말한다.</b> 「저장하지 못했습니다」로만 끝내면
    /// 사용자가 처음부터 다시 올리고, 앞서 올라간 파일은 주인 없이 남는다.
    /// </para>
    /// </summary>
    private static ApiException Failed(string fileName, int uploaded, string reason, Exception? inner = null) =>
        new($"{fileName} 을(를) 올리지 못했습니다"
            + (uploaded > 0 ? $" (앞의 {uploaded}개는 이미 올라갔습니다)" : string.Empty)
            + $" — {reason}",
            innerException: inner);

    private Task DeleteAsync(NoticeDto n) => Api.DeleteNoticeAsync(n.Id);

    /// <summary>
    /// 공지 하나를 사용자가 볼 화면 그대로 띄운다.
    ///
    /// <para>
    /// 목록을 <b>새로 만들어</b> 넘긴다. 같은 배열을 고쳐 쓰면
    /// <c>NoticePopup</c> 이 목록이 바뀐 것을 알아채지 못해, 다른 공지를
    /// 눌러도 앞서 본 것이 그대로 뜬다 — 그쪽은 참조로 본다.
    /// </para>
    /// </summary>
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
