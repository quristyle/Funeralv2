using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class AiAskPanel
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private AiTargetClient TargetApi { get; set; } = default!;
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;
    [Inject] private AiAskPrefs Prefs { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// 이 알맹이의 <b>권한 열쇠</b>. 「빠른 지시」 화면의 경로이고
    /// 권한표(<c>scom.system_menus.path</c>)가 그것으로 쥔다.
    /// <para>
    /// 화면 쪽(<c>AiAsk</c>)의 <c>@@page</c> 와 같아야 한다 — 어긋나면 화면에서
    /// 보이던 단추가 서랍에서만 사라진다. 아키텍처 테스트가 <c>@@page</c> 와 DB
    /// 경로를 대조하므로 이 값도 그 하나를 따른다.
    /// </para>
    /// </summary>
    private const string AskPath = "/projmng/ai/ask";

    /// <summary>고르는 칸 한 줄.</summary>
    public sealed record PickOption(string Value, string Text);

    private IReadOnlyList<PickOption> AllKinds = [];

    /// <summary>
    /// 처음 보이는 건수이자 <b>「더보기」가 한 번에 늘리는 수</b>의 <b>밑값</b>이다.
    /// 목록 화면이 아니므로 짧게 둔다 — 기본인 두 단에서 <b>가로 두 장씩
    /// 다섯 줄</b>이다.
    /// <para>
    /// 실제로 쓰는 수는 <see cref="PageCount"/> 다. 단 수가 사람 손에 달려
    /// 있으므로 <b>여기 적힌 10이 늘 그대로 나가지 않는다</b> — 세 단이면
    /// 12로 올라간다. 까닭은 그 속성에.
    /// </para>
    /// </summary>
    private const int RecentMax = 10;

    /// <summary>고를 수 있는 단 수. 화면의 고르개가 이 차례로 선다.</summary>
    private static readonly int[] ColChoices = [1, 2, 3];

    /// <summary>
    /// 기억이 없을 때의 단 수. <b>한동안 이 값 하나뿐이었다</b> — 고르개가
    /// 생기기 전에 쓰던 사람은 아무것도 안 바뀐 화면을 본다.
    /// </summary>
    private const int ColsDefault = 2;

    /// <summary>
    /// 카드를 몇 단으로 까는가. 격자에 그대로 실린다(<c>--pm-ask-cols</c>).
    /// </summary>
    private int _cols = ColsDefault;

    /// <summary>
    /// 한 번에 까는 장수이자 「더보기」가 한 번에 늘리는 수.
    /// <b><see cref="RecentMax"/> 이상이면서 단 수로 나누어떨어지는 가장 작은 수</b>다
    /// (한 단 10 · 두 단 10 · 세 단 12).
    /// </summary>
    /// <remarks>
    /// 10을 그대로 쓰면 세 단에서 <b>마지막 줄에 빈 칸이 하나</b> 남는다.
    /// 「더보기」를 누를 때마다 그 구멍이 같은 자리에 다시 생기므로 한 번
    /// 어긋나고 마는 것이 아니다.
    /// <para>
    /// <b>깔린 장수를 이 값에 맞추지는 못한다.</b> 안 끝난 것은 몇이든 전부
    /// 깔리기 때문이다(<see cref="Show"/>) — 여기서 고르게 두는 것은
    /// <b>채움분</b>뿐이다.
    /// </para>
    /// </remarks>
    private int PageCount => ((RecentMax + _cols - 1) / _cols) * _cols;

    /// <summary>
    /// 적어 둔 단 수를 화면이 쓸 수 있는 값으로 맞춘다. <b>범위를 벗어나면
    /// 기본값으로 돌린다</b> — 0을 그대로 쓰면 <c>repeat(0, …)</c> 이 되어
    /// 카드가 통째로 안 그려지고, 그 화면에는 아무 오류도 안 뜬다.
    /// </summary>
    private static int PickCols(int n) => ColChoices.Contains(n) ? n : ColsDefault;

    /// <summary>
    /// 한 건에 붙일 수 있는 파일 수. <b>서버의 상한과 같은 값이다</b>
    /// (<c>AiTaskFileService.MaxCount</c>) — 어긋나면 화면이 받아 놓고 서버가
    /// 거절하는 자리가 생기고, 그때는 이미 바이트를 다 보낸 뒤다.
    /// </summary>
    private const int MaxFiles = 5;

    /// <summary>
    /// 파일 한 개의 상한. <b>서버와 같은 값이어야 한다</b>
    /// (<c>AiTaskFileService.MaxBytes</c>).
    /// </summary>
    /// <remarks>
    /// 여기가 작으면 붙일 수 있는 것을 화면이 먼저 거절하고, 여기가 크면
    /// 바이트를 다 올린 뒤에 서버가 거절한다 — 뒤쪽이 더 나쁘다.
    /// </remarks>
    private const long MaxFileBytes = 100L * 1024 * 1024;

    /// <summary>고르개 아래 한 줄. <b>남은 자리를 적는다</b> — 「몇 개까지」보다 「몇 개 더」가 쓸모 있다.</summary>
    private string FileHint =>
        $"화면 사진이나 파일을 함께 보냅니다 — {MaxFiles - _attached.Count}개 더 · 한 개 {MaxFileBytes / 1024 / 1024}MB 까지";

    private FilePicker? _picker;

    /// <summary>
    /// 올려 두었고 <b>아직 어느 지시에도 안 묶인</b> 첨부. 보낼 때 번호만 실어 보낸다.
    /// </summary>
    /// <remarks>
    /// 목록을 브라우저에 기억하지 않는다 — 서버가 하루 지난 것을 치우므로
    /// 적어 두면 <b>없는 첨부가 붙어 보인다.</b> 화면이 열릴 때 서버에 묻는다
    /// (<see cref="LoadStagedFilesAsync"/>).
    /// </remarks>
    private List<AiTaskFileDto> _attached = [];

    /// <summary>바이트를 받거나 임시 첨부 저장을 마치기 전, 선택 사실을 보여 주는 파일명.</summary>
    private IReadOnlyList<string> _selectedFileNames = [];

    /// <summary>
    /// 지금 올리는 중인가. <b>보내기를 함께 막는다</b> — 덜 올라간 채로 나가면
    /// 사람은 붙인 줄 아는데 지시에는 안 붙는다.
    /// </summary>
    private bool _uploading;

    private IReadOnlyList<AiTargetDto> _targets = [];

    /// <summary>
    /// 받아 둔 것 <b>전부</b>. 서버가 잘라 주지 않으므로 한 번에 다 온다 —
    /// 「더보기」는 여기서 몇 장을 꺼내 보이느냐의 문제다.
    /// </summary>
    private IReadOnlyList<AiTaskDto> _all = [];

    /// <summary>
    /// <b>완료가 아닌</b> 것. 몇 건이든 전부 깔린다(<see cref="Show"/>).
    /// </summary>
    private IReadOnlyList<AiTaskDto> _open = [];

    /// <summary>완료된 것. 자리가 남을 때 <b>채우는 데</b> 쓴다.</summary>
    private IReadOnlyList<AiTaskDto> _done = [];

    /// <summary>지금 화면에 깔린 것. <c>_open</c> 전부 + <c>_done</c> 앞쪽 몇 장.</summary>
    private IReadOnlyList<AiTaskDto> _recent = [];

    /// <summary>
    /// 「더보기」를 몇 번 눌렀나. 한 번에 완료된 것 <see cref="RecentMax"/> 장씩
    /// 더 꺼낸다. <b>다시 읽어도 유지된다</b> — 5초마다 도는 타이머가 이 값을
    /// 되돌리면 펼쳐 놓고 보던 것이 눈앞에서 접힌다.
    /// </summary>
    /// <remarks>
    /// <b>장수가 아니라 누른 횟수를 들고 있다.</b> 장수로 들고 있으면 돌던 건이
    /// 완료로 넘어갈 때마다 채움분이 따로 늘어 <b>펼쳐 둔 자리가 저 혼자
    /// 길어진다.</b> 횟수로 두면 채움분은 채움분대로 다시 계산되고 사람이 늘린
    /// 몫만 그 위에 얹힌다.
    /// </remarks>
    private int _more;

    private long? _targetKey;
    private string _kind = "claude";
    private string? _text;

    /// <summary>복원된 임시본이 화면에 채워졌는지 여부.</summary>
    private bool _draftRestored;

    /// <summary>임시본이 저장되었던 시각.</summary>
    private DateTime? _draftSavedAt;

    /// <summary>본문 입력 후 임시저장 지연 타이머.</summary>
    private CancellationTokenSource? _draftDelay;

    /// <summary>DOM 레벨 실시간 임시저장 JS 모듈.</summary>
    private IJSObjectReference? _draftModule;

    /// <summary>카드 밀기를 받는 JS 모듈.</summary>
    private IJSObjectReference? _swipeModule;

    /// <summary>
    /// 그 모듈이 되부를 때 쥐는 손잡이. <b>반드시 치운다</b> — 안 치우면
    /// 회로가 닫혀도 이 부품이 JS 쪽 참조에 매달려 남는다.
    /// </summary>
    private DotNetObjectReference<AiAskPanel>? _swipeRef;

    /// <summary>
    /// 이 부품이 그린 상자의 id. <b>같은 알맹이가 화면과 서랍에 동시에 뜰 수
    /// 있어서</b> 생긴다 — 위 상자 주석 참고.
    /// </summary>
    private readonly string _domId = $"pm-ask-{Guid.NewGuid():N}";

    /// <summary>글상자를 집는 선택자. <b>제 상자 안에서만 찾는다.</b></summary>
    private string DraftSelector => $"#{_domId} .pm-ask__text";

    private string? Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;

            if (_draftRestored && string.IsNullOrWhiteSpace(_text))
            {
                _draftRestored = false;
            }

            TrackDraft();
        }
    }

    // _push Removed as we always follow SelectedTargetAllowsPush

    /// <summary>
    /// 메일로 받나. <b>기본이 켜짐이다</b> — 이 화면의 존재 이유다.
    /// 끈 것도 기억한다(위 머리말).
    /// </summary>
    private bool _mail = true;

    /// <summary>
    /// PWA 알림을 받나.
    /// </summary>
    private bool _pwa = true;

    /// <summary>
    /// 창으로 열어 둔 건. <b>번호만 들고 있고 자료는 목록에서 집는다</b> —
    /// 자료를 그대로 붙들면 5초마다 목록이 새로 읽힐 때 창만 옛 값에 남는다.
    /// </summary>
    private long? _peek;

    private bool Busy;

    /// <summary>보이는 동안 도는 타이머. <b>가려지면 멈춘다.</b></summary>
    private CancellationTokenSource? _poll;

    /// <summary>돌고 있는 건이 있을 때의 간격.</summary>
    /// <remarks>
    /// 로그를 따라가는 화면이 아니다. 5초면 넉넉하다 — 2초로 두면 열어 둔
    /// 판마다 서버를 그만큼 더 부른다.
    /// </remarks>
    private static readonly TimeSpan FollowBusy = TimeSpan.FromSeconds(5);

    /// <summary>하나도 안 도는 동안의 간격.</summary>
    /// <remarks>
    /// <b>여기서 아예 끄지 않는다.</b> 끄면 다른 자리에서 손댄 건과 서버가
    /// 나중에 지어 주는 제목이 영영 안 따라온다(머리말). 대신 넷 중 하나로
    /// 늦춰 값을 갚고, 가려진 동안에는 통째로 멈춘다.
    /// </remarks>
    private static readonly TimeSpan FollowIdle = TimeSpan.FromSeconds(20);

    /// <summary>
    /// 서랍이 펴져 있나. <b>화면(<c>AiAsk</c>)으로 열렸으면 안 내려온다</b> —
    /// 그때는 <c>null</c> 이고 「늘 보인다」는 뜻이다
    /// (<see cref="QuickAskReveal.OpenCascade"/>).
    /// </summary>
    [CascadingParameter(Name = QuickAskReveal.OpenCascade)]
    private bool? DrawerOpen { get; set; }

    /// <summary>지금 사람 눈에 보이는 자리인가. 따라갈지 말지를 이 값이 가른다.</summary>
    private bool Shown => DrawerOpen is not false;

    /// <summary>
    /// 마지막으로 본 <see cref="Shown"/>. <b>첫 판은 <c>null</c> 이다</b> —
    /// 그때는 <see cref="OnInitializedAsync"/> 가 이미 읽었으므로 또 읽지 않는다.
    /// </summary>
    private bool? _shown;

    /// <summary>고른 대상이 허용한 AI 만 고르게 한다(「AI 작업」 화면과 같은 규칙).</summary>
    private IReadOnlyList<PickOption> AllowedKinds
    {
        get
        {
            var target = _targets.FirstOrDefault(t => t.TargetKey == _targetKey);

            if (target?.RunnerKinds is not { Length: > 0 } kinds)
            {
                return AllKinds;
            }

            var allowed = kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var picked = AllKinds.Where(k => allowed.Contains(k.Value, StringComparer.OrdinalIgnoreCase)).ToList();

            return picked.Count > 0 ? picked : AllKinds;
        }
    }

    private IReadOnlyList<PickOption> RunnerKinds => AllowedKinds;

    /// <summary>
    /// 카드의 AI 배지에 달 전체 이름. <b>고르는 칸과 같은 글자</b>를 쓴다 —
    /// 공통코드(<c>AI_MODEL</c>)에서 읽은 <see cref="AllKinds"/> 에서 집는다.
    /// </summary>
    /// <remarks>
    /// <b>고른 대상이 허용한 것(<see cref="AllowedKinds"/>)으로 찾지 않는다.</b>
    /// 아래 카드는 지난 건들이라 <b>지금 고른 대상과 무관하고</b>, 그 대상이
    /// 허용하지 않는 AI 로 돌린 건도 섞여 있다 — 거기서 찾으면 그런 건의
    /// 배지만 이름을 잃는다.
    /// <para>
    /// 못 찾으면 코드값을 그대로 적는다. 목록을 못 받았을 때도(그쪽은 실패해도
    /// 화면을 막지 않는다) 배지가 <b>이름 없는 색 조각</b>으로 남지 않는다.
    /// </para>
    /// </remarks>
    private string KindName(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "AI 를 알 수 없음";
        }

        return AllKinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Text ?? kind;
    }

    /// <summary>
    /// 아직 안 보인 건수. <b>단추에 그대로 적는다.</b> 안 끝난 것은 언제나 다
    /// 깔리므로 <b>여기 남는 것은 완료된 것뿐이다.</b>
    /// </summary>
    private int Rest => Math.Max(0, _all.Count - _recent.Count);

    private bool CanSend =>
        !Busy && !_uploading && _selectedFileNames.Count == 0
        && _targetKey is not null && !string.IsNullOrWhiteSpace(_text);

    private AiTargetDto? SelectedTarget => _targets.FirstOrDefault(t => t.TargetKey == _targetKey);

    /// <summary>고른 대상이 올리기를 허용하나 (<c>ai_target.allow_push</c>).</summary>
    private bool SelectedTargetAllowsPush => SelectedTarget?.AllowPush == true;

    /// <summary>
    /// 화면이 내보이는 올리기 상태. <b>허용하지 않는 대상으로 바꾸면 저절로
    /// 꺼진 것으로 보인다</b> — 켜 둔 채로 회색이 되면 「올라가는 건가 마는
    /// 건가」를 알 수 없고, 이 화면에서 그 물음은 배포가 걸린 물음이다.
    /// </summary>
    private bool PushOn => SelectedTargetAllowsPush;

    /// <summary>메일로 받나. 켠 것도 끈 것도 기억한다.</summary>
    private bool MailOn
    {
        get => _mail;
        set
        {
            _mail = value;
            Remember();
        }
    }

    /// <summary>PWA 알림을 받나. 켠 것도 끈 것도 기억한다.</summary>
    private bool PwaOn
    {
        get => _pwa;
        set
        {
            _pwa = value;
            Remember();
        }
    }

    /// <summary>
    /// 대상 칸이 매인 자리. 바꾸면 <b>AI 를 맞추고 그 자리에서 기억한다.</b>
    /// 칸이 <c>_targetKey</c> 에 곧장 매이면 이 둘을 할 자리가 없다.
    /// </summary>
    private long? PickTarget
    {
        get => _targetKey;
        set
        {
            if (_targetKey == value)
            {
                return;
            }

            _targetKey = value;

            // 새 대상이 못 쓰는 AI 가 골라져 있을 수 있다. 기억하기 **전에**
            // 맞춘다 — 안 맞춘 채로 적어 두면 다음에 열 때 그 어긋남이 되살아난다.
            FixKind();
            Remember();
        }
    }

    private string PickKind
    {
        get => _kind;
        set
        {
            // 비우는 길이 없는 칸이다(`NullText` 를 안 걸었다). 그래도 빈 값이
            // 들어오면 **고른 것을 지우지 않는다** — 실행기 이름이 빈 채로
            // 저장되면 그 건은 아무 데서도 안 돈다.
            if (string.IsNullOrWhiteSpace(value) || _kind == value)
            {
                return;
            }

            _kind = value;
            Remember();
        }
    }

    /// <summary>
    /// 단추 글자. <b>올리기를 켜면 바뀐다</b> — 누르기 직전에 눈이 닿는
    /// 마지막 글자가 여기라, 여기서까지 「메일로 받기」라고만 적혀 있으면
    /// 배포가 나가는 줄 모르고 누른다.
    /// </summary>
    /// <remarks>
    /// 설명 줄(옛 <c>SendNote</c>)을 걷어낸 뒤로 <b>올리기가 어느 쪽인지 말하는
    /// 자리가 여기와 묻는 창 둘뿐이다.</b> 글자를 흐리게 고치지 않는다.
    /// </remarks>
    private string SendText => _uploading || _selectedFileNames.Count > 0 ? "첨부 올리는 중"
        : Busy ? "보내는 중"
        : PushOn ? "보내고 올리기까지"
        : "보내고 메일로 받기";

    /// <summary>기억을 사람마다 갈라 두려고 본다. 로그인 아이디만 쓴다.</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>로그인 아이디. 못 읽었으면 <c>null</c> 이다.</summary>
    private string? _me;

    /// <summary>
    /// <b>내가 올린 건인가.</b> 「새 남긴말」 배지를 내 카드에만 세우는 데
    /// 쓴다 — 이 목록에는 남이 보낸 것도 함께 깔린다.
    /// </summary>
    private bool IsMine(AiTaskDto t) =>
        _me is { Length: > 0 } me
        && string.Equals(me, t.CreId?.Trim(), StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        // **기억은 사람마다 갈라 둔다.** 공용 PC 에서 남이 고른 대상이 내
        // 화면에 뜨면 안 된다(「AI 작업」 화면의 임시본과 같은 규칙이다).
        if (AuthState is not null)
        {
            var state = await AuthState;

            // **기억을 가르는 데도, 카드를 가르는 데도 같은 아이디를 쓴다**
            // (`IsMine` — 「새 남긴말」 배지가 내 건에만 서게 한다).
            _me = state.User.Identity?.Name?.Trim();

            Prefs.Use(_me);
        }

        await LoadModelsAsync();
        await LoadTargetsAsync();
        await LoadStagedFilesAsync();
        await LoadRecentAsync();
    }

    /// <summary>
    /// 붙여 두고 아직 안 보낸 첨부를 되읽는다.
    /// </summary>
    /// <remarks>
    /// <b>화면을 막지 않는다.</b> 첨부를 못 읽는 것과 지시를 못 보내는 것은
    /// 다른 일이라, 여기서 토스트를 띄우거나 조회 표시를 잡지 않는다 —
    /// 최악이라도 붙여 둔 것을 다시 고르면 된다.
    /// </remarks>
    private async Task LoadStagedFilesAsync()
    {
        try
        {
            _attached = [.. await Api.MyStagedFilesAsync()];
        }
        catch (ApiException)
        {
            // 게이트웨이가 잠깐 없다. 붙인 것이 없는 것으로 보고 넘어간다.
        }
    }

    /// <summary>
    /// 고른 파일을 <b>그 자리에서</b> 올린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>FilesChanged</c> 는 고를 때와 바이트가 넘어온 뒤 두 번 온다. 앞엣것은
    /// 아직 받아 둔 것이 없어 <b>빈 목록</b>이라 그대로 지나간다.
    /// </para>
    /// <para>
    /// 올린 뒤 <see cref="FilePicker.ClearAsync"/> 로 그쪽 목록을 비운다 —
    /// 안 비우면 같은 파일이 두 벌로 보이고 다음에 고를 때 앞엣것까지 다시
    /// 올라간다. 그 호출이 <c>FilesChanged</c> 를 한 번 더 부르므로
    /// <see cref="_uploading"/> 이 그 되돌이를 막는다.
    /// </para>
    /// </remarks>
    private async Task OnPickedAsync(IReadOnlyList<PickedFile> files)
    {
        if (_uploading || files.Count == 0)
        {
            return;
        }

        _uploading = true;
        StateHasChanged();

        try
        {
            var saved = await Api.StageFilesAsync(files);

            _attached = [.. _attached, .. saved];

            Say(saved.Count == 1 ? $"{saved[0].FileNm} 을(를) 붙였습니다." : $"{saved.Count}개를 붙였습니다.");
        }
        catch (ApiException ex)
        {
            // **여기서 말하지 않으면 아무도 모른다.** 고르개는 제 목록을
            // 비워 버리므로 실패한 파일이 화면에서 통째로 사라진다.
            Say($"첨부를 올리지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            if (_picker is not null)
            {
                await _picker.ClearAsync();
            }

            _uploading = false;
            StateHasChanged();
        }
    }

    private Task OnSelectionChanged(IReadOnlyList<string> fileNames)
    {
        _selectedFileNames = fileNames;
        return Task.CompletedTask;
    }

    /// <summary>붙인 것을 뗀다. <b>아직 안 보낸 것만</b> 떼어진다.</summary>
    private async Task DropFileAsync(AiTaskFileDto file)
    {
        try
        {
            await Api.DeleteFileAsync(file.FileKey);
            _attached = [.. _attached.Where(f => f.FileKey != file.FileKey)];
        }
        catch (ApiException ex)
        {
            Say($"첨부를 떼지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }

        StateHasChanged();
    }

    /// <summary>
    /// 서랍이 여닫혔다. <b>펴지면 그 자리에서 한 번 읽고 다시 따라간다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 서랍은 닫아도 이 알맹이를 지우지 않아서(<c>QuickAskDrawer</c>) 다시
    /// 열어도 아무도 목록을 읽지 않았다 — 처음 열었을 때의 카드가 그대로
    /// 앉아 있었다. 그것이 이 갈고리를 둔 이유다.
    /// </para>
    /// <para>
    /// <b>읽는 데 <see cref="LoadRecentAsync"/> 를 쓰지 않는다.</b> 그쪽은
    /// <see cref="DataPage.LoadAsync"/> 를 거치므로 화면 전환 표시를 잡고
    /// 실패하면 토스트를 띄운다 — 서랍을 여는 손짓마다 그것이 뜨면 방해다.
    /// </para>
    /// </remarks>
    protected override void OnParametersSet()
    {
        var shown = Shown;

        if (_shown == shown)
        {
            return;
        }

        var first = _shown is null;
        _shown = shown;

        // 첫 판은 `OnInitializedAsync` 가 이미 읽고 따라가기까지 걸어 두었다.
        if (first)
        {
            return;
        }

        if (!shown)
        {
            StopPoll();
            return;
        }

        _ = ReopenAsync();
    }

    /// <summary>다시 펴졌다. 한 번 읽고 따라가기를 건다.</summary>
    private async Task ReopenAsync()
    {
        await RefreshAsync(CancellationToken.None);
        Follow();
    }

    private async Task LoadModelsAsync()
    {
        var models = await ModelCodes.GetAsync();
        AllKinds = [.. models.Select(x => new PickOption(x.Value, x.Text))];
    }

    /// <summary>
    /// 기억한 것을 얹는다. <b>회로가 붙은 뒤에야 읽을 수 있다</b>(JS 왕복)라
    /// 첫 렌더 뒤다 — 대상 목록은 그때 이미 와 있다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var pref = await Prefs.ReadAsync();

        if (pref is not null)
        {
            // **없어졌거나 꺼진 대상은 얹지 않는다.** 목록에 없는 번호를 넣으면
            // 칸이 빈 채로 보이고, 사람은 「골랐는데 왜 안 보내지나」를 겪는다.
            if (pref.TargetKey is { } key && _targets.Any(t => t.TargetKey == key))
            {
                _targetKey = key;
            }

            if (!string.IsNullOrWhiteSpace(pref.RunnerKind))
            {
                _kind = pref.RunnerKind;
            }

            // 지금 고른 대상이 못 쓰는 AI 였으면 여기서 맞는 것으로 바뀐다.
            FixKind();

            // _push = pref.AutoPush; (removed)
            _mail = pref.NotifyEmail;
            _pwa = pref.NotifyPwa;

            // 단 수는 **얹고 나서 다시 잘라야 한다.** 첫 판은 기본인 두 단으로
            // 깔렸고 채움분이 그 수(10)로 계산되어 있어서, 세 단을 기억한
            // 사람은 여기서 다시 세지 않으면 **마지막 줄에 빈 칸 하나를 둔
            // 채로** 화면이 선다.
            _cols = PickCols(pref.ListCols);
            Show(_all);

            // 작성 중이던 임시본이 있으면 복원한다
            if (!string.IsNullOrWhiteSpace(pref.DraftText))
            {
                _text = pref.DraftText;
                _draftRestored = true;
                _draftSavedAt = pref.DraftSavedAt;
            }

            StateHasChanged();
        }

        // 브라우저 측 실시간 입력 감지기 부착 (네트워크 단절 중 타자도 localStorage 에 즉시 보존)
        await AttachDraftListenerAsync();

        // 카드 밀어서 확인하기.
        await AttachSwipeAsync();
    }

    private async Task AttachDraftListenerAsync()
    {
        try
        {
            _draftModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/ask-draft.js");
            await _draftModule.InvokeVoidAsync("attachDraft", DraftSelector, Prefs.CurrentKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// 카드를 오른쪽으로 밀면 확인 완료가 되게 한다.
    /// </summary>
    /// <remarks>
    /// <b>거는 자리가 목록(<c>ul</c>)이 아니라 이 판의 뿌리 상자다.</b> 목록은
    /// 카드가 하나도 없으면 아예 안 그려지고(그 자리에 「아직 보낸 것이
    /// 없습니다」가 선다), 다시 생길 때는 **다른 DOM** 이라 걸어 둔 것이
    /// 사라진다 — 첫 건을 보낸 사람은 그 뒤로 못 민다.
    /// <para>
    /// 뿌리는 이 부품이 사는 내내 그대로이므로 <b>한 번만 건다.</b> 카드마다
    /// 거는 길도 있지만 목록이 5초마다 다시 그려져 그때마다 왕복이 생긴다 —
    /// 손짓은 눌린 카드를 <c>closest</c> 로 거슬러 찾는다.
    /// </para>
    /// </remarks>
    private async Task AttachSwipeAsync()
    {
        try
        {
            _swipeModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/ask-swipe.js");
            _swipeRef ??= DotNetObjectReference.Create(this);

            await _swipeModule.InvokeVoidAsync("attachSwipe", $"#{_domId}", _swipeRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// <b>민 카드를 확인 완료로 넘긴다.</b> 브라우저가 띠를 다 열고 카드를
    /// 스러뜨린 뒤에 부른다.
    /// </summary>
    /// <remarks>
    /// <b>화면에서 먼저 빼고 서버에 간다.</b> 반대로 하면 게이트웨이 왕복
    /// 동안 스러진 카드가 제자리에 그대로 있다가 사라지는데, 그 몇 백 ms 가
    /// 「민 것이 안 먹었나」로 읽혀 한 번 더 밀게 된다.
    /// <para>
    /// 서버가 못 받으면 <b>목록을 다시 읽어 되돌린다.</b> 확인되지 않은 건이
    /// 화면에서만 사라지면 그 건은 아무 데서도 안 보인다 — 이 목록이
    /// 「아직 확인 안 한 것」을 보는 유일한 자리다.
    /// </para>
    /// </remarks>
    [JSInvokable]
    public async Task ConfirmSwipedAsync(long taskKey)
    {
        if (!_all.Any(t => t.TaskKey == taskKey))
        {
            return;
        }

        Show([.. _all.Where(t => t.TaskKey != taskKey)]);
        await InvokeAsync(StateHasChanged);

        try
        {
            await Api.ConfirmAsync(taskKey);
        }
        catch (ApiException ex)
        {
            Say($"확인 처리하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            await RefreshAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 고른 것을 적어 둔다. <b>기다리지 않는다</b> — 칸 하나 만질 때마다
    /// JS 왕복을 기다리면 그만큼 칸이 굼떠 보이고, 적히지 않아도 잃는 것은
    /// 「다음에 다시 고르는 일」뿐이다.
    /// </summary>
    private void Remember() => _ = Prefs.SaveAsync(new AiAskPref
    {
        TargetKey = _targetKey,
        RunnerKind = _kind,

        // 올리기는 이제 항상 대상의 허용 여부를 따르므로
        // 기억할 때도 켜진 상태로 저장한다.
        AutoPush = true,
        NotifyEmail = _mail,
        NotifyPwa = _pwa,
        ListCols = _cols,
        DraftText = _text,
        DraftSavedAt = _draftSavedAt ?? (_text is not null ? DateTime.Now : null),
    });

    /// <summary>
    /// 본문 입력을 잠시 뒤에 브라우저 저장소에 적어 둔다.
    /// </summary>
    private void TrackDraft()
    {
        CancelDraftDelay();

        _draftDelay = new CancellationTokenSource();
        var ct = _draftDelay.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, ct);

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                await Prefs.SaveDraftAsync(_text);
            }
            catch (OperationCanceledException)
            {
            }
        }, ct);
    }

    private void CancelDraftDelay()
    {
        _draftDelay?.Cancel();
        _draftDelay?.Dispose();
        _draftDelay = null;
    }

    /// <summary>
    /// 복원되었거나 작성 중이던 임시본을 비운다.
    /// </summary>
    private async Task ClearDraftAsync()
    {
        CancelDraftDelay();
        _text = null;
        _draftRestored = false;
        _draftSavedAt = null;

        await Prefs.ClearDraftAsync();

        if (_draftModule is not null)
        {
            try
            {
                await _draftModule.InvokeVoidAsync("clearDraft", DraftSelector, Prefs.CurrentKey);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// 대상 목록. <b>실패해도 화면을 막지 않는다</b> — 최근 보낸 것은 보여야
    /// 「왜 아무것도 없나」를 알 수 있다.
    /// </summary>
    private async Task LoadTargetsAsync()
    {
        await LoadAsync(async () =>
        {
            _targets = await TargetApi.ListAsync(onlyEnabled: true);
            return _targets.Count;
        }, emptyMessage: "쓸 수 있는 대상이 없습니다.", failMessage: "대상을 읽지 못했습니다");

        // **하나뿐이면 골라 둔다.** 고를 것이 없는 목록을 열게 하지 않는다.
        if (_targetKey is null && _targets.Count == 1)
        {
            _targetKey = _targets[0].TargetKey;
        }

        FixKind();
    }

    /// <summary>
    /// 최근 보낸 것. <b>내가 보낸 것만</b>은 서버가 가리지 않으므로 여기서는
    /// 전부 온다 — 이 기능을 쓰는 사람이 여럿이 되면 그때 서버에 조건을 더한다.
    /// </summary>
    private async Task LoadRecentAsync()
    {
        await LoadAsync(async () =>
        {
            Show(await Api.ListAsync(userConfirmed: false));

            return _recent.Count;
        }, emptyMessage: string.Empty, failMessage: "보낸 것을 읽지 못했습니다");

        // 지시자의 얼굴. **깔린 카드만이 아니라 받아 둔 것 전부**를 채운다 —
        // 「더보기」는 서버를 다시 부르지 않으므로(`ShowMore`) 여기서 안
        // 채우면 펼친 순간 얼굴 없는 카드가 깔린다.
        //
        // 화면을 막지 않는다. 얼굴은 목록이 다 그려진 뒤에 채워져도 되고,
        // 못 읽어도 첫 글자가 선다.
        await Faces.EnsureAsync(_all.Select(t => t.CreId));

        Follow();
    }

    /// <summary>
    /// 「완료」인가. <b>카드에 적히는 글자 그대로다</b> — 실패 · 시간초과 ·
    /// 취소 · 중단은 끝나기는 했어도 완료가 아니다(<c>AiTaskDto.StatusText</c>).
    /// </summary>
    private static bool IsDone(AiTaskDto t) => t.TaskStatus == "succeeded";

    /// <summary>
    /// 받은 것을 최근 순으로 세우고 <b>완료가 아닌 것 전부 + 모자란 만큼의
    /// 완료된 것</b>을 꺼내 놓는다.
    /// <b>읽어 오는 자리는 둘(첫 조회 · 타이머)인데 자르는 규칙은 하나다.</b>
    /// </summary>
    private void Show(IReadOnlyList<AiTaskDto> rows)
    {
        _all = rows
            .OrderByDescending(t => t.RequestedAt ?? t.CreDt ?? DateTime.MinValue)
            .ToList();

        _open = _all.Where(t => !IsDone(t)).ToList();
        _done = _all.Where(IsDone).ToList();

        // 안 끝난 것이 한 판을 넘으면 채움은 0이다 — **깎지는 않는다.**
        // 아홉이면 아홉 장이 다 깔린다.
        var page = PageCount;
        var fill = Math.Max(0, page - _open.Count) + (_more * page);

        _recent = _open.Concat(_done.Take(fill)).ToList();
    }

    /// <summary>
    /// 단 수를 바꾼다. <b>서버를 부르지 않는다</b> — 목록은 이미 다 받아 두었고
    /// 바뀌는 것은 같은 자료를 몇 줄로 늘어놓느냐뿐이다.
    /// </summary>
    /// <remarks>
    /// <b>「더보기」를 몇 번 눌렀는지(<see cref="_more"/>)는 그대로 둔다.</b>
    /// 되돌리면 펼쳐 놓고 보던 것이 단을 바꾸는 순간 접힌다 — 5초마다 도는
    /// 타이머가 그 값을 건드리지 않는 것과 같은 까닭이다.
    /// <para>
    /// 다만 <b>한 판의 장수가 함께 바뀌므로</b>(<see cref="PageCount"/>)
    /// 깔린 장수는 조금 달라진다. 세 단으로 옮기면 열 장이 열두 장이 된다.
    /// </para>
    /// </remarks>
    private void SetCols(int n)
    {
        n = PickCols(n);

        if (_cols == n)
        {
            return;
        }

        _cols = n;

        // 채움분이 단 수의 배수라 여기서 다시 센다.
        Show(_all);
        Remember();
    }

    /// <summary>
    /// 완료된 것 넉 장 더. 서버를 다시 부르지 않는다 — 이미 다 받아 두었다.
    /// </summary>
    private void ShowMore()
    {
        _more++;
        Show(_all);
    }

    /// <summary>채움분만 남기고 되돌린다.</summary>
    private void Fold()
    {
        _more = 0;
        Show(_all);
    }

    /// <summary>
    /// 고른 대상이 못 쓰는 AI 가 남아 있을 수 있다. 그대로 두면 **집어 간 뒤에야**
    /// 어긋남이 드러난다 — 화면에서는 아무 표시 없이 실패한다.
    /// </summary>
    private void FixKind()
    {
        var allowed = AllowedKinds;

        if (allowed.Count > 0 && !allowed.Any(k => k.Value == _kind))
        {
            _kind = allowed[0].Value;
        }
    }

    /// <summary>
    /// 적은 것을 작업으로 만들고 바로 요청까지 한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「AI 작업」 화면은 저장과 요청을 일부러 나눠 두었다 — 쓰다 만 글이
    /// 저장만으로 AI 에게 가면 안 되기 때문이다. <b>여기서는 합친다.</b>
    /// 이 화면에 글을 적는 일 자체가 「보내겠다」는 뜻이고, 단추를 두 번
    /// 누르게 하면 길에서 쓰는 화면이라는 전제가 무너진다.
    /// </para>
    /// <para>
    /// [단추는 <b>적어 둘 때까지만</b> 묶인다]
    /// </para>
    /// <para>
    /// 예전에는 적기 · 요청 · 목록 다시 읽기 셋을 전부 기다린 뒤에야 단추가
    /// 풀렸다. 그 셋 중 사람이 꼭 기다려야 하는 것은 <b>첫째뿐</b>이다 —
    /// 적어 두는 데 성공했으면 그 글은 없어지지 않고, 나머지는 늦어도
    /// 결과가 달라지지 않는다. 그런데 하필 둘째가 제일 오래 걸렸다(브로커에
    /// 종을 울리는 자리다. 지금은 서버도 그것을 뒤로 넘긴다 —
    /// <c>AiTaskBell</c>).
    /// </para>
    /// <para>
    /// 그래서 <b>적어 두면 그 자리에서 글상자를 비우고 단추를 돌려준다.</b>
    /// 이어지는 것은 <see cref="HandOffAsync"/> 가 뒤에서 한다.
    /// </para>
    /// </remarks>
    private async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        // 올리기를 켠 채로 보내도 **묻지 않는다.** 대상이 허용한 것만 켜지고
        // 단추 글자가 이미 「보내고 올리기까지」다 — 위 머리말 참고.
        Busy = true;

        // **`try` 밖에 둔다.** 적어 둔 것을 손에 쥔 채로 단추를 먼저 놓아 준다.
        AiTaskDto? created = null;

        try
        {
            FixKind();

            var madeOk = await RunAsync(async () =>
            {
                // 커밋·push 와 DB 처리를 말해 주는 문구를 **무조건** 뒤에 붙인다
                // (`AiTaskAlways`). 「이어서 지시」가 쓰는 것과 같은 글이다 —
                // 처음 보낸 건만 그 말을 못 들어 고친 것이 워크트리에 남는 일이
                // 있었다.
                var finalInstruction = AiTaskAlways.Append(_text);

                created = await Api.CreateAsync(new AiTaskDto
                {
                    Contents = finalInstruction,
                    ContentFormat = "markdown",
                    TargetKey = _targetKey,
                    RunnerKind = _kind,
                    // 화면에 제한 칸이 없다 — 「AI 작업」의 기본과 같은 값을 박는다.
                    TimeoutMinutes = 60,
                    AttemptMax = 3,

                    // 화면이 아니라 `PushOn` 을 싣는다 — 대상을 바꾸는 사이에
                    // `_push` 만 참으로 남아 있을 수 있다. 서버도
                    // `AiTaskService.Normalize` 에서 같은 것을 한 번 더 본다.
                    AutoPush = PushOn,

                    // 기본은 켜짐이다. 끈 사람은 끈 줄 알고 끈 것이고, 꺼진
                    // 네모가 그 사실을 계속 보여 준다.
                    NotifyEmail = MailOn,
                    NotifyPwa = PwaOn,
                    NotifyWhen = "always",

                    // 미리 올려 둔 첨부를 이 건에 묶어 달라는 뜻이다. 서버가
                    // **올린 사람이 나인 것만** 묶는다(`AiTaskFileService.BindAsync`).
                    FileKeys = _attached.Count == 0 ? null : [.. _attached.Select(f => f.FileKey)],
                });
            }, okMessage: "적어 두었습니다. 보내는 중입니다.", failMessage: "보내지 못했습니다");

            if (!madeOk || created is null)
            {
                return;
            }
        }
        finally
        {
            Busy = false;
        }

        // 여기서부터는 **적어 두기가 끝난 뒤**다. 화면을 먼저 비운다 —
        // 다음 한 줄을 적기 시작하는 데 서버를 기다릴 이유가 없다.
        //
        // **붙인 것도 함께 비운다.** 그 번호들은 방금 이 건에 묶였으므로
        // 더는 「떠 있는 첨부」가 아니다 — 남겨 두면 다음 건에도 같은 사진이
        // 붙어 보이고, 보내면 묶이지 않아 소리 없이 빠진다.
        _attached = [];
        _text = null;
        _draftRestored = false;
        _draftSavedAt = null;

        CancelDraftDelay();
        _ = Prefs.ClearDraftAsync();

        if (_draftModule is not null)
        {
            _ = _draftModule.InvokeVoidAsync("clearDraft", DraftSelector, Prefs.CurrentKey);
        }

        // **고른 것은 그대로 둔다.** 글상자만 비운다 — 여기서 잇따라 던지는
        // 건은 대개 같은 자리에 같은 모양이고, 한때는 올리기만 꺼 두었지만
        // 그것도 이제 기억한다(위 머리말). 켜져 있다는 사실은 단추 글자와
        // 묻는 창이 매번 말한다.

        // 기다리지 않는다. 회로의 실행 맥락에서 이어지므로 `Task.Run` 도
        // `InvokeAsync` 도 필요 없다 — 이 처리기가 끝난 자리에서 그대로 이어진다.
        _ = HandOffAsync(created.TaskKey);
    }

    /// <summary>
    /// 적어 둔 것을 <b>실제로 보낸다</b> — 단추를 놓아 준 뒤에 한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 만들기와 요청이 <b>따로다.</b> 만들기만 되고 요청이 실패하면 「대기」도
    /// 아닌 채로 남으므로, 그 사실을 화면이 말해야 한다 — 실패 토스트는
    /// 60초를 머문다(<c>Toasts.Failure</c>). <b>안 간 것을 갔다고 말하는 것이
    /// 이 화면에서 제일 나쁜 실패다.</b>
    /// </para>
    /// <para>
    /// 여기서 <see cref="DataPage.RunAsync"/> 를 쓰지 않는다. 그것은
    /// <c>Loading</c> 을 잡는데, 이미 단추를 돌려준 뒤라 <b>사람이 다음 글을
    /// 적는 동안 화면이 조회 중으로 보인다.</b>
    /// </para>
    /// </remarks>
    private async Task HandOffAsync(long taskKey)
    {
        try
        {
            await Api.RequestAsync(taskKey);
        }
        catch (ApiException ex)
        {
            Say($"적어 두기는 했는데 요청이 안 됐습니다 — {ex.Message}", NoticeTone.Error);
        }

        // 성공했든 아니든 목록은 다시 읽는다. 실패한 건도 「적어만 둔 것」으로
        // 거기 있어야 「AI 작업」 화면에서 이어 보낼 수 있다.
        await LoadRecentAsync();

        StateHasChanged();
    }

    /// <summary>
    /// 휴대폰인가. 카드를 눌렀을 때 <b>창을 띄울지 탭으로 옮길지</b>를
    /// 이 값이 가른다(위 머리말).
    /// </summary>
    private bool _isPhone;

    /// <summary>
    /// 카드를 눌렀다. <b>휴대폰이면 창, 넓은 화면이면 탭</b>이다.
    /// </summary>
    /// <remarks>
    /// 넓은 화면에서 옮겨 가는 주소는 앱알림·메일이 싣는 것과 같다 —
    /// 두 벌로 두면 한쪽만 고쳐져 어긋난다.
    /// </remarks>
    private void Peek(long taskKey)
    {
        if (!_isPhone)
        {
            Navigation.NavigateTo($"/projmng/ai/task/{taskKey}");
            return;
        }

        _peek = taskKey;
    }

    /// <summary>
    /// 창이 볼 건. <b>받아 둔 것 전부에서 찾는다</b> — 깔린 넉 장에서만 찾으면
    /// 창을 열어 둔 사이에 새 건이 들어와 그 건이 밀려날 때 창이 빈다.
    /// 목록에서 아주 사라졌으면 <c>null</c> 이고 창이 그렇게 말한다.
    /// </summary>
    private AiTaskDto? PeekItem =>
        _peek is { } key ? _all.FirstOrDefault(t => t.TaskKey == key) : null;

    private void OnPeekVisible(bool visible)
    {
        if (!visible)
        {
            _peek = null;
        }
    }

    /// <summary>
    /// 창에서 수동 재시도를 요청했다. 목록을 다시 읽고 화면을 갱신한다.
    /// </summary>
    private async Task OnPeekRetriedAsync(AiTaskDto _)
    {
        await LoadRecentAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 목록을 <b>조용히</b> 다시 읽는다 — 화면 전환 표시도 토스트도 없다.
    /// </summary>
    /// <remarks>
    /// 따라가기(<see cref="Follow"/>)와 서랍이 다시 펴질 때
    /// (<see cref="ReopenAsync"/>)가 같은 이것을 쓴다. 자르는 규칙은
    /// <see cref="Show"/> 하나뿐이라 두 자리가 갈릴 일이 없다.
    /// <para>
    /// <b>못 읽으면 조용히 지나간다.</b> 다음 바퀴에 다시 본다 — 몇 초마다
    /// 도는 일로 토스트를 쌓으면 화면이 오류로 뒤덮인다.
    /// </para>
    /// </remarks>
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var rows = await Api.ListAsync(userConfirmed: false, ct: ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            Show(rows);

            // 따라가는 동안 **남이 보낸 건이 끼어든다.** 첫 조회 때와 같이
            // 모르는 아이디만 묻는다 — 아는 것은 들고 있으므로 이 고리가
            // 왕복을 늘리지 않는다.
            await Faces.EnsureAsync(_all.Select(t => t.CreId), ct);

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // 판을 닫았거나 화면을 떠났다. 정상이다.
        }
        catch (ObjectDisposedException)
        {
            // 회로가 닫히는 중이다.
        }
        catch (ApiException)
        {
            // 게이트웨이가 잠깐 없다. 다음 바퀴에 다시 본다.
        }
    }

    /// <summary>
    /// <b>보이는 동안 목록을 계속 따라간다.</b> 가려지면 멈춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 간격이 둘이다 — 돌고 있는 건이 있으면 <see cref="FollowBusy"/>,
    /// 하나도 없으면 <see cref="FollowIdle"/> 다. <b>안 돈다고 꺼 버리지
    /// 않는다</b>: 이 판이 답해야 하는 것 중 절반은 다른 자리에서 일어난
    /// 일이라(머리말) 꺼 두면 그것이 영영 안 따라온다.
    /// </para>
    /// <para>
    /// 대신 <b>가려졌을 때 끈다.</b> 서랍은 닫아도 알맹이를 안 지우므로,
    /// 이것이 없으면 ⚡ 를 한 번 누른 사람의 모든 화면이 닫아 둔 판의
    /// 왕복을 계속 치른다.
    /// </para>
    /// </remarks>
    private void Follow()
    {
        if (!Shown)
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
                    // **잰 다음에 잔다.** 자고 일어나서 재면 방금 끝난 건을
                    // 한 바퀴 더 5초 간격으로 쫓는다.
                    var busy = _recent.Any(t => t.IsBusy || t.TitlePending);

                    await Task.Delay(busy ? FollowBusy : FollowIdle, cts.Token);

                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    await RefreshAsync(cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 판을 닫았거나 화면을 떠났다. 정상이다.
            }
            catch
            {
                // 한 번 실패했다고 화면을 깨지 않는다. 다음 바퀴에 다시 본다.
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
    public void Dispose()
    {
        StopPoll();
        CancelDraftDelay();

        _swipeRef?.Dispose();
        _swipeRef = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopPoll();
        CancelDraftDelay();

        _swipeRef?.Dispose();
        _swipeRef = null;

        if (_draftModule is not null)
        {
            try
            {
                await _draftModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }

        if (_swipeModule is not null)
        {
            try
            {
                await _swipeModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }
    }
}
