using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class NotificationHistory
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>알림구분 목록. 정본은 공통코드(<c>NOTI_CATEGORY</c>)다.</summary>
    [Inject] private PushCategoryClient Categories { get; set; } = default!;

    /// <summary>여는 김에 하는 읽음 처리가 실패했을 때만 쓴다 — 화면에는 말하지 않는다.</summary>
    [Inject] private ILogger<NotificationHistory> Log { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(_categoryOptions, o => o.Value, o => o.Text, _category),
        SchSummary.Or(_keyword.Trim()),
        SchSummary.On(_unreadOnly, "안 읽은 것만"));

    /// <summary>
    /// 조회 기간. <b>기본이 최근 한 달이다.</b>
    /// </summary>
    /// <remarks>
    /// 알림은 지우지 않으므로 자라기만 한다. 전에는 조건 없이 불러
    /// <b>그 사람의 알림을 전부</b> 받아 왔다(서버에 페이징이 없다 —
    /// <c>GetMyNotificationsAsync</c> 머리말). 발송 이력(이레)보다 길게 잡은
    /// 것은 자기 알림함은 드물게 열어 보기 때문이다.
    /// </remarks>
    private DateTime? _from = AppTime.TodayDate.AddMonths(-1);
    private DateTime? _to = AppTime.TodayDate;

    /// <summary>
    /// 고른 알림구분. 비면 전체다.
    /// </summary>
    /// <remarks>
    /// <see cref="PushCategoryClient.Unset"/> 이면 <b>구분이 안 붙은 줄</b>만
    /// 본다 — 구분을 붙이기 전에 쌓인 알림이 거기 있다.
    /// </remarks>
    private string? _category;

    /// <summary>제목 또는 내용에 들어 있는 글자를 찾는다.</summary>
    private string _keyword = string.Empty;

    /// <summary>고르개에 담는 항목들. 「전체」와 「구분 없음」이 함께 들어 있다.</summary>
    private IReadOnlyList<SchOption> _categoryOptions = [new SchOption(null, "전체")];

    /// <summary>코드값 → 이름. 표가 줄마다 부르므로 목록을 받을 때 한 번 만든다.</summary>
    private Func<string?, string> _categoryName = v => v ?? string.Empty;

    /// <summary>
    /// 안 읽은 것만 보기. <b>기본이 켜짐이다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 알림함을 여는 까닭은 대개 「안 본 것이 있나」다. 그런데 읽은 것까지
    /// 한 달치가 섞여 나오면 <b>볼 것을 눈으로 골라내야 했다</b> — 읽음 표시를
    /// 해 둔 값어치가 목록에서 살지 않았다. 다 보려면 스위치를 끄면 된다.
    /// </para>
    /// <para>
    /// 조건은 <b>양쪽에 건다.</b> 서버에 실어야 상한(2,000줄)을 읽은 줄이
    /// 먼저 먹지 않고, 화면에서도 걸러야 읽음 처리를 누른 줄이 <b>다시 조회하지
    /// 않아도</b> 목록에서 빠진다.
    /// </para>
    /// </remarks>
    private bool _unreadOnly = true;

    private IReadOnlyList<NotificationDto> _all = [];

    /// <summary>
    /// 체크한 줄들. 표 위의 일괄 단추 둘이 이 값에 건다.
    /// </summary>
    /// <remarks>
    /// <b>줄을 누르는 것으로는 안 채워진다</b> — 줄 누르기는 「연다」이고
    /// 체크는 맨 앞 칸에서만 한다(<c>AllowSelectRowByClick="false"</c>).
    /// 둘을 겹쳐 두면 알림 하나를 열어 보려던 손짓이 일괄 삭제의 대상을
    /// 한 건 늘린다.
    /// </remarks>
    private IReadOnlyList<NotificationDto> _selectedItems = [];

    /// <summary>일괄삭제를 묻는 창. 한 건짜리 길에는 안 쓴다.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>
    /// 지금까지 낸 조회의 번호. <b>마지막으로 낸 것만 표에 앉힌다.</b>
    /// </summary>
    /// <remarks>
    /// 조건을 고치면 그 자리에서 조회가 나가므로, 잇달아 고치면 조회가 겹친다.
    /// 늦게 낸 것이 먼저 오면 <b>옛 조건의 목록이 새 조건 아래 앉는다</b> —
    /// 화면에는 「구분: 배포」인데 목록은 전체인 꼴이고, 조건 칸을 보고
    /// 목록을 읽는 사람에게는 알아챌 방법이 없다.
    /// </remarks>
    private int _latest;

    private IReadOnlyList<NotificationDto> Shown =>
        _unreadOnly ? [.. _all.Where(n => !n.IsRead)] : _all;

    // ── 조건이 바뀌면 그 자리에서 다시 읽는다 ──────────────────
    //
    // 기간과 구분은 **서버가 거르는 조건**이라 다시 묻는 것 말고는 길이 없다.
    // 「안 읽은 것만」은 받아 둔 것에서도 걸러 그림이 먼저 바뀌지만
    // (`Shown`), 끌 때 읽은 알림을 **그때 받아 와야** 해서 역시 다시 읽는다.

    private Task OnFromChangedAsync(DateTime? value)
    {
        _from = value;
        return ReloadAsync();
    }

    private Task OnToChangedAsync(DateTime? value)
    {
        _to = value;
        return ReloadAsync();
    }

    private Task OnCategoryChangedAsync(string? value)
    {
        _category = value;
        return ReloadAsync();
    }

    private Task OnUnreadChangedAsync(bool value)
    {
        _unreadOnly = value;
        return ReloadAsync();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadCategoriesAsync();
        await ReloadAsync();
    }

    /// <summary>
    /// 갈래 목록을 받아 고르개와 옮기개를 함께 세운다.
    /// </summary>
    /// <remarks>
    /// <b>조회와 묶지 않는다.</b> 공통코드를 못 읽어도 알림함은 열려야 한다 —
    /// 그때는 고르개에 「전체」만 남고 표의 구분 칸에 코드값이 그대로 뜬다
    /// (<see cref="PushCategoryClient.GetAsync"/> 가 빈 목록을 돌려준다).
    /// </remarks>
    private async Task LoadCategoriesAsync()
    {
        _categoryOptions = await Categories.OptionsAsync();
        _categoryName = PushCategoryClient.Labeler(await Categories.GetAsync());
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var mine = ++_latest;
        var rows = await Api.GetMyNotificationsAsync(_from, _to, _category, _unreadOnly, _keyword);

        // 내가 낸 것보다 새 조회가 이미 나갔으면 받은 것을 버린다(위 머리말).
        // 버린 답은 「없습니다」도 말하지 않는다 — 곧 오는 최신 답이 말한다.
        if (mine != _latest) return 1;

        _all = rows;
        return _all.Count;
    }, "받은 알림이 없습니다.", "알림을 읽지 못했습니다");

    /// <summary>
    /// 그 알림이 가리키는 화면을 연다. <b>탭이 하나 선다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 알림마다 「눌렀을 때 열 주소」가 함께 저장돼 있다(<c>push_send_logs.url</c>).
    /// 기기에서 알림을 누르면 서비스워커가 그 주소를 여는데, <b>알림함에서
    /// 보던 사람에게는 그 길이 없었다</b> — 제목만 읽고 그 화면을 사이드바에서
    /// 다시 찾아 들어가야 했다.
    /// </para>
    /// <para>
    /// 여기서는 <b>옮기기만 한다.</b> 탭을 세우는 것은 가는 쪽 화면의 몫이고
    /// (AI 작업 지시는 <c>AiTaskViewPage</c> 가 스스로 연다), 메뉴에 있는
    /// 화면이면 레이아웃이 세운다.
    /// </para>
    /// <para>
    /// <b>열면서 읽음으로 찍는다.</b> 앱 알림을 눌러 들어올 때와 같은 판단이다
    /// (<c>PushClickRead</c>) — 그 건의 화면을 연 사람에게 다시 「봤다」를
    /// 누르게 하는 것은 앞뒤가 맞지 않는다.
    /// </para>
    /// </remarks>
    private async Task OpenAsync(NotificationDto n)
    {
        var url = OpenUrl(n.Url);

        if (url is null)
        {
            // 주소 없이 보낸 알림이 있다(구독 알림의 옛 줄 따위). 아무 일도
            // 안 일어나면 고장으로 읽히므로 그 사실을 말한다.
            //
            // **그때는 읽음으로도 안 찍는다.** 열리지 않았으니 본 것이 아니다.
            Say("이 알림에는 열어 볼 화면이 없습니다.", NoticeTone.Warning);
            return;
        }

        // **옮기기 전에 찍는다.** 옮기고 나면 이 화면이 사라지는데, 그 뒤에
        // 도는 일감은 취소되든 살아남든 확인할 길이 없다. 왕복 하나를 더
        // 기다리는 편이 「열었는데 안 읽은 채로 남았다」보다 낫다.
        await MarkReadQuietlyAsync(n);

        Navigation.NavigateTo(url);
    }

    /// <summary>
    /// 여는 김에 읽음으로 찍는다. <b>못 찍어도 조용하다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 사람이 시킨 것은 「열기」지 「읽음 처리」가 아니다. 곁다리로 도는 일이
    /// 실패했다고 토스트를 띄우면, 화면은 멀쩡히 열렸는데 빨간 줄이 따라와
    /// 열기가 실패한 것처럼 보인다. 표의 「읽음 처리」 단추가 그대로 남아
    /// 있으므로 사람이 다시 할 수 있다 — <c>PushClickRead</c> 와 같은 규칙이다.
    /// </para>
    /// <para>
    /// 이미 읽은 줄은 부르지 않는다. 서버는 안 읽은 줄만 찍으므로 결과는
    /// 같지만, 목록을 훑어보는 동안 왕복이 공짜로 늘 이유가 없다.
    /// </para>
    /// </remarks>
    private async Task MarkReadQuietlyAsync(NotificationDto n)
    {
        if (n.IsRead)
        {
            return;
        }

        try
        {
            await Api.MarkNotificationReadAsync(n.Id);

            // 돌아왔을 때 목록이 이미 그 사실을 알고 있게 한다 —
            // 「안 읽은 것만」이 켜져 있으면 그 줄은 다음 그림에서 빠진다.
            n.IsRead = true;
        }
        catch (ApiException ex)
        {
            Log.LogDebug(ex, "알림 {Id} 를 읽음으로 찍지 못했다.", n.Id);
        }
    }

    /// <summary>
    /// 저장된 주소를 <b>지금 열 수 있는 주소</b>로 옮긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 주소는 <b>보낸 그때의 값</b>이라 화면이 옮겨 간 뒤에도 옛 주소가 그대로
    /// 남아 있다. AI 작업이 그렇다 — 한동안 목록 주소(<c>/projmng/ai/tasks?task=89</c>)
    /// 를 싣다가 건마다 주소가 생겼다(<c>/projmng/ai/task/89</c>). 옛 줄을
    /// 그대로 열면 목록이 뜨고, <b>누른 사람은 그 건을 눈으로 다시 찾아야 한다</b> —
    /// 건별 주소를 만든 이유가 바로 그것이었다(<c>AiTaskViewPage</c> 머리말).
    /// </para>
    /// <para>
    /// DB 의 값은 건드리지 않는다. <b>보여 줄 때만 옮긴다</b> — 옛 공지 본문의
    /// 파일 주소를 <c>NoticeHtml</c> 이 옮기는 것과 같은 자리다.
    /// </para>
    /// <para>
    /// 여기 없는 옛 주소는 그대로 간다. 메뉴가 옮겨 간 경로는 셸의
    /// <c>RouteAliases</c> 가 받아 주고, 그것도 없으면 「준비 중」이 뜬다.
    /// </para>
    /// </remarks>
    private static string? OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var trimmed = url.Trim();

        // 옛 AI 작업 알림: 목록 주소 + 건 번호(`?task=` · `?key=`).
        var match = AiTaskListLink().Match(trimmed);

        return match.Success ? $"/projmng/ai/task/{match.Groups["key"].Value}" : trimmed;
    }

    [GeneratedRegex(@"^/projmng/ai/tasks\?(?:task|key)=(?<key>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AiTaskListLink();

    private async Task MarkReadAsync(NotificationDto n)
    {
        if (await RunAsync(() => Api.MarkNotificationReadAsync(n.Id),
                "읽음으로 표시했습니다.", "표시하지 못했습니다"))
        {
            n.IsRead = true;
        }
    }

    /// <summary>
    /// 체크한 것을 한꺼번에 읽음으로 찍는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이미 읽은 줄은 빼고 보낸다.</b> 서버도 안 읽은 줄만 찍으므로 결과는
    /// 같지만, 「3건을 읽음으로 표시했습니다」가 실제로 바뀐 수와 맞아야 한다.
    /// 뺀 뒤에 남는 것이 없으면 아무 일도 하지 않는다.
    /// </para>
    /// <para>
    /// 왕복은 <b>하나</b>다(<see cref="AdminClient.MarkNotificationsReadAsync"/>) —
    /// 한 건씩 부르면 체크 수만큼 왕복이 늘고, 중간에 끊겼을 때 어디까지
    /// 찍혔는지 알 수 없다.
    /// </para>
    /// </remarks>
    private async Task MarkSelectedReadAsync()
    {
        var targets = _selectedItems.Where(n => !n.IsRead).ToList();
        if (targets.Count == 0)
        {
            Say("고른 알림이 이미 모두 읽음입니다.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.MarkNotificationsReadAsync(targets.Select(n => n.Id)),
                $"{targets.Count}건을 읽음으로 표시했습니다.", "표시하지 못했습니다"))
        {
            // 받아 둔 것에도 반영한다 — 「안 읽은 것만」이 켜져 있으면 그
            // 줄들이 다음 그림에서 빠진다. 다시 조회하지 않는 것은 한 건짜리
            // 읽음 처리와 같은 까닭이다(백 건짜리 목록이 깜박인다).
            foreach (var n in targets)
            {
                n.IsRead = true;
            }

            _selectedItems = [];
        }
    }

    /// <summary>
    /// 체크한 것을 한꺼번에 알림함에서 치운다. <b>묻고 나서 한다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한 건씩 치우는 길(오른쪽 클릭 창 · 서랍에서 카드 밀기)은 눈앞의 그 줄
    /// 하나라 묻지 않는다. 체크해 둔 수십 건은 다르다 — 무엇이 사라졌는지
    /// 되짚을 길이 없고, 머리줄의 체크 한 번이면 보이는 것이 전부 잡힌다.
    /// </para>
    /// <para>
    /// 줄이 진짜로 지워지지는 않는다(<c>deleted_at</c>). 그래도 사람이 다시
    /// 꺼내 볼 자리는 없으므로 되돌릴 수 없는 것으로 치고 묻는다.
    /// </para>
    /// </remarks>
    private async Task DeleteSelectedAsync()
    {
        var targets = _selectedItems.ToList();
        if (targets.Count == 0) return;

        var ok = await _confirm!.AskAsync(
            $"고른 알림 {targets.Count}건을 알림함에서 치웁니다."
            + "\n되돌릴 수 없습니다 — 보낸 쪽 발송 기록에는 남지만 이 목록에서는 다시 볼 수 없습니다.");

        if (!ok) return;

        if (await RunAsync(() => Api.DeleteNotificationsAsync(targets.Select(n => n.Id)),
                $"{targets.Count}건을 삭제했습니다.", "삭제하지 못했습니다"))
        {
            _selectedItems = [];
            await ReloadAsync();
        }
    }
}
