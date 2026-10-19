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

    /// <summary>AI 고르개에 한도를 적는다. 대시보드를 부르지 않고 한도 표만 읽는다.</summary>
    [Inject] private AiDashboardClient Dashboard { get; set; } = default!;
    [Inject] private AiAskPrefs Prefs { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>이 알맹이의 권한 열쇠. 화면(<c>AiAsk</c>)의 <c>@page</c> 와 같아야 한다 — 어긋나면 서랍에서만 단추가 사라진다.</summary>
    private const string AskPath = "/projmng/ai/ask";

    /// <summary>공통코드 <c>AI_MODEL</c> 의 AI 전부. 고르개와 같은 형(<see cref="AiModelOption"/>)을 쓴다.</summary>
    private IReadOnlyList<AiModelOption> AllKinds = [];

    /// <summary>처음 보이는 건수이자 「더보기」 증분의 밑값. 실제로 쓰는 수는 <see cref="PageCount"/> 다.</summary>
    private const int RecentMax = 10;

    /// <summary>고를 수 있는 단 수. 화면의 고르개가 이 차례로 선다.</summary>
    private static readonly int[] ColChoices = [1, 2, 3, 4];

    /// <summary>기억이 없을 때의 단 수.</summary>
    private const int ColsDefault = 2;

    /// <summary>카드를 몇 단으로 까는가. 격자에 실린다(<c>--pm-ask-cols</c>).</summary>
    private int _cols = ColsDefault;

    /// <summary>한 번에 깔고 더보기로 늘리는 장수. 단 수로 나누어떨어지는 가장 작은 수다(10 이면 세 단에서 마지막 줄 빈 칸이 남는다).
    /// 깔린 장수를 이 값에 맞추지는 못한다 — 안 끝난 것은 몇이든 전부 깔린다(<see cref="Show"/>).</summary>
    private int PageCount => ((RecentMax + _cols - 1) / _cols) * _cols;

    /// <summary>적어 둔 단 수를 허용 값으로 맞춘다. 범위를 벗어나면 기본값으로 돌린다 — 0 이면 <c>repeat(0, …)</c> 이 되어 카드가 통째로 안 그려진다.</summary>
    private static int PickCols(int n) => ColChoices.Contains(n) ? n : ColsDefault;

    /// <summary>한 건에 붙일 파일 수. 서버 상한(<c>AiTaskFileService.MaxCount</c>)과 같아야 한다 — 어긋나면 바이트를 다 보낸 뒤 서버가 거절한다.</summary>
    private const int MaxFiles = 5;

    /// <summary>파일 한 개의 상한. 서버 <c>AiTaskFileService.MaxBytes</c>와 같아야 한다 — 작으면 화면이 먼저 거절하고, 크면 서버가 다 올린 뒤 거절한다.</summary>
    private const long MaxFileBytes = 100L * 1024 * 1024;

    /// <summary>고르개 아이콘의 설명(<c>title</c>). 「몇 개까지」보다 「몇 개 더」처럼 남은 자리를 적는다.</summary>
    private string FileHint =>
        $"화면 사진이나 파일을 함께 보냅니다 — {MaxFiles - _attached.Count}개 더 · 한 개 {MaxFileBytes / 1024 / 1024}MB 까지";

    private FilePicker? _picker;

    /// <summary>올려 두었고 아직 어느 지시에도 묶이지 않은 첨부. 목록은 브라우저에 기억하지 않고 화면이 열릴 때 서버에 묻는다 — 지난 첨부가 붙어 보이는 것을 막는다.</summary>
    private List<AiTaskFileDto> _attached = [];

    /// <summary>바이트를 받거나 임시 첨부 저장을 마치기 전, 선택 사실을 보여 주는 파일명.</summary>
    private IReadOnlyList<string> _selectedFileNames = [];

    /// <summary>업로드 중. 보내기도 함께 막는다 — 덜 올라간 채로 나가면 지시에 첨부가 빠진다.</summary>
    private bool _uploading;

    private IReadOnlyList<AiTargetDto> _targets = [];

    /// <summary>받아 둔 건 전부. 서버가 잘라 주지 않으므로 「더보기」는 여기서 몇 장을 꺼내 보이느냐의 문제다.</summary>
    private IReadOnlyList<AiTaskDto> _all = [];

    /// <summary>완료가 아닌 건. 몇 건이든 전부 깐다.</summary>
    private IReadOnlyList<AiTaskDto> _open = [];

    /// <summary>완료된 것. 자리가 남을 때 <b>채우는 데</b> 쓴다.</summary>
    private IReadOnlyList<AiTaskDto> _done = [];

    /// <summary>지금 화면에 깔린 것. <c>_open</c> 전부 + <c>_done</c> 앞쪽 몇 장.</summary>
    private IReadOnlyList<AiTaskDto> _recent = [];

    /// <summary>목록을 한 번이라도 물어봤나(실패 포함). 묻기 전의 빈 목록과 물어본 뒤 빈 목록은 다르다.</summary>
    private bool _recentLoaded;

    /// <summary>「더보기」를 누른 횟수. 장수가 아니라 횟수로 들어야 완료 전환 때 펼쳐 둔 자리가 저 혼자 길어지지 않는다.
    /// 다시 읽어도 유지한다 — 5초 타이머가 되돌리면 펼쳐 보던 것이 접힌다.</summary>
    private int _more;

    private long? _targetKey;
    private string _kind = "claude";
    private string? _text;

    /// <summary>임시본을 저장한 시각.</summary>
    private DateTime? _draftSavedAt;

    /// <summary>본문 입력 후 임시저장 지연 타이머.</summary>
    private CancellationTokenSource? _draftDelay;

    /// <summary>DOM 레벨 실시간 임시저장 JS 모듈.</summary>
    private IJSObjectReference? _draftModule;

    /// <summary>글상자의 Ctrl+Enter 를 받는 손잡이. 회로가 닫히면 반드시 치워야 JS 쪽 참조에 남지 않는다.</summary>
    private DotNetObjectReference<AiAskPanel>? _draftRef;

    /// <summary>카드 밀기를 받는 JS 모듈.</summary>
    private IJSObjectReference? _swipeModule;

    /// <summary>휴대폰 목록 끝에서 더 펼치기를 감지하는 JS 모듈.</summary>
    private IJSObjectReference? _moreModule;

    /// <summary>최근 목록 쓸어내려 새로고침(Pull-to-Refresh)을 받는 JS 모듈.</summary>
    private IJSObjectReference? _pullModule;

    /// <summary>최근 목록 쓸어내려 새로고침 시 되부를 때 쥐는 손잡이.</summary>
    private DotNetObjectReference<AiAskPanel>? _pullRef;

    /// <summary>그 JS 모듈이 되부를 때 쥐는 손잡이. 회로가 닫히면 반드시 치워야 JS 쪽 참조에 남지 않는다.</summary>
    private DotNetObjectReference<AiAskPanel>? _swipeRef;

    /// <summary>이 부품 상자의 id. 같은 알맹이가 화면과 서랍에 동시에 뜰 수 있어 고유해야 한다.</summary>
    private readonly string _domId = $"pm-ask-{Guid.NewGuid():N}";

    private ElementReference _moreSentinel;
    private DotNetObjectReference<AiAskPanel>? _moreRef;

    /// <summary>글상자를 집는 선택자. <b>제 상자 안에서만 찾는다.</b></summary>
    private string DraftSelector => $"#{_domId} .pm-ask__text";

    private static string TargetKindText(AiTaskDto task) =>
        task.TargetKind == "folder" ? "폴더" : "저장소";

    private static string? TargetName(AiTaskDto task)
    {
        var name = task.TargetKind == "folder"
            ? task.TargetNm
            : string.IsNullOrWhiteSpace(task.TargetRepoUrl)
                ? task.TargetNm
                : task.TargetRepoUrl;

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>카드 첫 줄 상태 문자열. 4단에서는 폭이 좁아 2글자만 쓴다.</summary>
    internal static string StatusText(string? text, int cols)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (cols >= 4 && text.Length > 2)
        {
            return text[..2];
        }

        return text;
    }

    internal static string StatusText(AiTaskDto task, int cols) =>
        StatusText(task.StatusText, cols);

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

            TrackDraft();
        }
    }


    private bool Busy;

    /// <summary>보이는 동안 도는 타이머. <b>가려지면 멈춘다.</b></summary>
    private CancellationTokenSource? _poll;

    /// <summary>돌고 있는 건이 있을 때의 간격. 5초면 넉넉하다 — 2초로 두면 열어 둔 판마다 서버를 그만큼 더 부른다.</summary>
    private static readonly TimeSpan FollowBusy = TimeSpan.FromSeconds(5);

    /// <summary>하나도 안 도는 동안의 간격. 끄지 않는다 — 끄면 다른 자리에서 손댄 건과 서버가 지은 제목이 안 따라온다.</summary>
    private static readonly TimeSpan FollowIdle = TimeSpan.FromSeconds(20);

    /// <summary>서랍이 펴져 있나. 화면(<c>AiAsk</c>)으로 열렸으면 <c>null</c> 이고 늘 보인다는 뜻이다.</summary>
    [CascadingParameter(Name = QuickAskReveal.OpenCascade)]
    private bool? DrawerOpen { get; set; }

    /// <summary>지금 사람 눈에 보이는 자리인가. 따라갈지 말지를 이 값이 가른다.</summary>
    private bool Shown => DrawerOpen is not false;

    /// <summary>마지막으로 본 <see cref="Shown"/>. 첫 판은 <c>null</c> 이고, 그때는 <see cref="OnInitializedAsync"/> 가 이미 읽었으므로 또 읽지 않는다.</summary>
    private bool? _shown;

    /// <summary>고른 대상이 허용한 AI 만 고르게 한다(「AI 작업」 화면과 같은 규칙).</summary>
    private IReadOnlyList<AiModelOption> AllowedKinds
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

    private IReadOnlyList<AiModelOption> RunnerKinds => AllowedKinds;

    /// <summary>실행기가 올린 AI 별 한도. 고르는 자리에서 보여야 다 쓴 CLI 로 보냈다가 실패하는 일을 막는다.
    /// 못 읽으면 빈 목록이 되고 고르개는 이름만 보인다 — 지시 전송에는 지장이 없다.</summary>
    private IReadOnlyList<AiUsageSnapshot> _usage = [];

    /// <summary>한도를 한 번이라도 받았나. 받기 전과 받아서 빈 것은 다르다 — 없으면 묻는 중인 CLI 가 「보고 없음」으로 보인다. 한 번 참이 되면 되돌리지 않는다.</summary>
    private bool _usageSeen;

    /// <summary>그 CLI 의 한도 줄들. 한 CLI 가 여럿을 가질 수 있다.</summary>
    private List<AiUsageSnapshot> UsageOf(string? kind)
        => string.IsNullOrWhiteSpace(kind)
            ? []
            : [.. _usage.Where(u => string.Equals(u.RunnerKind, kind, StringComparison.OrdinalIgnoreCase))];


    /// <summary>한도를 읽어 둔다. 화면을 막지 않는다. 따라가기(폴링)에 태우지 않는다 — 실행기는 15분에 한 번 보고하므로 화면이 열릴 때와 서랍이 펴질 때만 읽는다.</summary>
    private async Task LoadUsageAsync()
    {
        _usage = await Dashboard.UsageAsync();

        // 못 읽었어도 참이다 — 아니면 「보고 없음」과 게이트웨이 없음이 둘 다 「곧 올 것」으로 보인다.
        _usageSeen = true;
    }

    /// <summary>카드 AI 배지의 전체 이름. 지난 건은 지금 고른 대상과 무관하므로 허용 목록(<see cref="AllowedKinds"/>)으로 찾지 않는다.
    /// 못 찾으면 코드값을 그대로 적는다.</summary>
    private string KindName(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "AI 를 알 수 없음";
        }

        return AllKinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Text ?? kind;
    }

    /// <summary>아직 안 보인 건수. 안 끝난 것은 늘 다 깔리므로 여기 남는 것은 완료된 것뿐이다.</summary>
    private int Rest => Math.Max(0, _all.Count - _recent.Count);

    private bool CanSend =>
        !Busy && !_uploading && _selectedFileNames.Count == 0
        && _targetKey is not null && !string.IsNullOrWhiteSpace(_text);

    private AiTargetDto? SelectedTarget => _targets.FirstOrDefault(t => t.TargetKey == _targetKey);

    /// <summary>고른 대상이 올리기를 허용하나 (<c>ai_target.allow_push</c>).</summary>
    private bool SelectedTargetAllowsPush => SelectedTarget?.AllowPush == true;

    /// <summary>화면에 보이는 올리기 상태. 허용하지 않는 대상이면 꺼진 것으로 보여야 한다 — 켜 둔 채 회색이면 올라가는지 알 수 없다.</summary>
    private bool PushOn => SelectedTargetAllowsPush;

    /// <summary>대상 칸이 매인 자리. 바꾸면 AI 를 맞추고 그 자리에서 기억한다. 칸을 <c>_targetKey</c> 에 곧장 매면 이 둘을 할 자리가 없다.</summary>
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

            // 못 쓰는 AI 가 골라져 있을 수 있어 기억하기 전에 맞춘다.
            FixKind();
            Remember();
        }
    }

    private string PickKind
    {
        get => _kind;
        set
        {
            // 비우는 칸이 아니다. 빈 값이 들어와도 고른 것을 지우지 않는다 — 빈 이름이 저장되면 그 건은 아무 데서도 안 돈다.
            if (string.IsNullOrWhiteSpace(value) || _kind == value)
            {
                return;
            }

            _kind = value;
            Remember();
        }
    }

    /// <summary>지시 단추 글자. 평소엔 「지시하기」이고 일하는 동안만 하는 일로 바뀐다. 올리기 여부는 대상이 정하므로 글자로 말하지 않는다.</summary>
    private string SendText => _uploading || _selectedFileNames.Count > 0 ? "첨부 올리는 중"
        : Busy ? "보내는 중"
        : "지시하기";

    /// <summary>기억을 사람마다 갈라 두려고 본다. 로그인 아이디만 쓴다.</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>로그인 아이디. 못 읽었으면 <c>null</c> 이다.</summary>
    private string? _me;

    /// <summary>내가 올린 건인가. 「새 남긴말」 배지를 내 카드에만 세운다 — 목록에는 남이 보낸 것도 깔린다.</summary>
    private bool IsMine(AiTaskDto t) =>
        _me is { Length: > 0 } me
        && string.Equals(me, t.CreId?.Trim(), StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        // 기억은 사람마다 갈라 둔다. 공용 PC 에서 남이 고른 대상이 뜨면 안 된다.
        if (AuthState is not null)
        {
            var state = await AuthState;

            // 기억을 가르는 것과 카드를 가르는 것이 같은 아이디를 쓴다(<c>IsMine</c>).
            _me = state.User.Identity?.Name?.Trim();

            Prefs.Use(_me);
        }

        // 고르개를 세우는 것만 기다린다 — AI 고르개는 목록이 와야 켜진다.
        // 대상 고르개의 기억 얹기는 목록이 와 있다고 가정한다.
        await LoadModelsAsync();

        // 나머지는 대상과 나란히 묻는다 — 줄 세우면 카드 목록이 왕복 하나만큼 늦게 선다.
        _ = LoadAsideAsync();

        await LoadTargetsAsync();
    }

    /// <summary>곁들이는 것(최근 보낸 것 · 한도 · 붙은 첨부)을 한꺼번에 묻는다. 카드 목록이 알맹이라 기다리지 않는다.
    /// 프리렌더에서는 묻지 않는다 — 그 판은 회로가 붙으면 버려진다.</summary>
    private async Task LoadAsideAsync()
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        try
        {
            await Task.WhenAll(
                // 안에서 `Follow()` 까지 건다 — 따라가기가 제 리듬으로 다시 그린다.
                Then(LoadRecentAsync()),
                Then(LoadUsageAsync()),
                Then(LoadStagedFilesAsync()));
        }
        catch (ObjectDisposedException)
        {
            // 다 받기 전에 화면을 떠났다. 정상이다.
        }

        async Task Then(Task load)
        {
            await load;
            await Redraw();
        }
    }

    /// <summary>배경에서 받은 것을 화면에 얹는다. 떠난 뒤면 조용히 지나간다.</summary>
    private async Task Redraw()
    {
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
            // 회로가 닫히는 중이다.
        }
    }

    /// <summary>붙여 두고 아직 안 보낸 첨부를 되읽는다. 화면을 막지 않는다 — 첨부를 못 읽는 것과 지시를 못 보내는 것은 다른 일이다.</summary>
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

    /// <summary>고른 파일을 그 자리에서 올린다. <c>FilesChanged</c> 는 고를 때와 바이트가 넘어온 뒤 두 번 오고, 앞엣것은 빈 목록이라 그냥 지나간다.
    /// 올린 뒤 <see cref="FilePicker.ClearAsync"/> 로 고르개 목록을 비운다 — 안 비우면 같은 파일이 두 벌로 올라간다. <see cref="_uploading"/> 이 그 되돌이를 막는다.</summary>
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
            // 여기서 말하지 않으면 아무도 모른다 — 고르개가 제 목록을 비워 실패한 파일이 사라진다.
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

    /// <summary>서랍이 여닫혔다. 펴지면 한 번 읽고 따라가기를 건다. 서랍은 닫아도 지우지 않으므로 이 갈고리가 없으면 처음 카드가 그대로 남는다.
    /// 읽기에 <see cref="LoadRecentAsync"/> 를 쓰지 않는다 — 화면 전환 표시와 실패 토스트가 서랍 여는 손짓마다 뜬다.</summary>
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

        // 한도도 여기서 다시 읽는다 — 서랍은 살아 있는 부품이라 안 다시 읽으면 어제 값이 오늘 값처럼 보인다.
        await LoadUsageAsync();

        Follow();
        await ResetMoreObserverAsync();
    }

    private async Task LoadModelsAsync()
    {
        var models = await ModelCodes.GetAsync();
        AllKinds = models;
    }

    /// <summary>기억한 것을 얹는다. 회로가 붙은 뒤에야 읽을 수 있어 첫 렌더 뒤에 한다.
    /// 구르는 자리 옮기기는 첫 렌더 밖에 둔다 — 탭을 갈아탄 뒤의 일이라 빠져나가는 줄 안쪽에 두면 영영 안 돈다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await MoveScrollAsync();

        if (!firstRender)
        {
            return;
        }

        var pref = await Prefs.ReadAsync();

        if (pref is not null)
        {
            // 없어졌거나 꺼진 대상은 얹지 않는다 — 목록에 없는 번호는 칸을 빈 채로 보이게 한다.
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


            // 단 수는 얹고 나서 다시 잘라야 한다 — 첫 판의 채움분은 기본 두 단 기준이라 세 단이면 마지막 줄에 빈 칸이 남는다.
            _cols = PickCols(pref.ListCols);
            Show(_all);

            // 임시본이 있으면 복원한다. 글상자에 그 글이 이미 보이므로 안내 줄은 세우지 않는다.
            if (!string.IsNullOrWhiteSpace(pref.DraftText))
            {
                _text = pref.DraftText;
                _draftSavedAt = pref.DraftSavedAt;
            }

            StateHasChanged();
        }

        // 브라우저 측 실시간 입력 감지기 부착 (네트워크 단절 중 타자도 localStorage 에 즉시 보존)
        await AttachDraftListenerAsync();

        // 카드 밀어서 확인하기.
        await AttachSwipeAsync();

        // 휴대폰에서 목록 끝에 닿으면 더 펼친다.
        await AttachMoreObserverAsync();

        // 최근 리스트에서 쓸어내려 새로고침(Pull-to-refresh)을 붙인다.
        await AttachPullRefreshAsync();
    }

    private async Task AttachDraftListenerAsync()
    {
        try
        {
            _draftModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/ask-draft.js");
            await _draftModule.InvokeVoidAsync("attachDraft", DraftSelector, Prefs.CurrentKey);

            // Ctrl+Enter 로 보내는 길. 같은 글상자를 잡으므로 같은 모듈이 한다.
            _draftRef ??= DotNetObjectReference.Create(this);
            await _draftModule.InvokeVoidAsync("attachSubmitKey", DraftSelector, _draftRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <param name="text">누른 순간 글상자의 글. 들고 있는 값을 쓰지 않는다 — 편집기가 300ms 를 모아 보내므로 마지막 글자가 빠진다.</param>
    [JSInvokable]
    public async Task SubmitFromKeyAsync(string? text)
    {
        await InvokeAsync(async () =>
        {
            // 속성으로 넣는다 — 임시저장 타이머까지 함께 다시 건다.
            Text = string.IsNullOrWhiteSpace(text) ? null : text;

            if (!CanSend)
            {
                // 보낼 수 없는 까닭은 단추가 이미 말하므로 여기서 또 띄우지 않는다.
                StateHasChanged();
                return;
            }

            await SendAsync();
            StateHasChanged();
        });
    }

    /// <summary>카드를 오른쪽으로 밀면 확인 완료 처리. 뿌리 상자에 한 번만 건다 — 목록은 카드가 없으면 안 그려져서 다시 생길 때 걸어 둔 것이 사라진다.</summary>
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

    private async Task AttachMoreObserverAsync()
    {
        try
        {
            _moreModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/ask-more.js");
            _moreRef ??= DotNetObjectReference.Create(this);

            await _moreModule.InvokeVoidAsync(
                "attachMoreObserver", _moreSentinel, _moreRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    private async Task ResetMoreObserverAsync()
    {
        if (_moreModule is not null)
        {
            try
            {
                await _moreModule.InvokeVoidAsync("resetMoreObserver", _moreSentinel);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }
    }

    /// <summary>휴대폰이나 마우스 휠로 목록 끝에 닿아 더 보기를 시도했다.</summary>
    [JSInvokable]
    public async Task<bool> ShowMoreFromScrollAsync()
    {
        var hasMore = false;

        await InvokeAsync(() =>
        {
            if (Rest <= 0)
            {
                return;
            }

            ShowMore();
            StateHasChanged();
            hasMore = Rest > 0;
        });

        return hasMore;
    }

    /// <summary>최근 목록을 아래로 쓸어내리면 새로고침한다.</summary>
    private async Task AttachPullRefreshAsync()
    {
        try
        {
            _pullModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/ask-pull.js");
            _pullRef ??= DotNetObjectReference.Create(this);

            await _pullModule.InvokeVoidAsync("attachPullRefresh", $"#{_domId}", _pullRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <summary>최근 목록에서 아래로 쓸어내려 새로고침을 요청했다.</summary>
    [JSInvokable]
    public async Task RefreshRecentFromSwipeDownAsync()
    {
        await RefreshAsync(CancellationToken.None);
        await LoadUsageAsync();
        await ResetMoreObserverAsync();
        Follow();
    }

    /// <summary>민 카드를 확인 완료로 넘긴다. 화면에서 먼저 빼고 서버에 간다 — 거꾸로 하면 왕복 동안 스러진 카드가 남아 「안 먹었나」로 읽힌다.
    /// 서버가 못 받으면 목록을 다시 읽어 되돌린다. 확인 안 된 건이 화면에서만 사라지면 아무 데서도 안 보인다.</summary>
    [JSInvokable]
    public async Task ConfirmSwipedAsync(long taskKey)
    {
        if (!_all.Any(t => t.TaskKey == taskKey))
        {
            return;
        }

        // 탭으로 열어 둔 건이면 함께 닫는다 — 아니면 목록에서 빠진 건을 가리킨 채 남는다.
        CloseView(taskKey);

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

    /// <summary>고른 것을 적어 둔다. 기다리지 않는다 — 칸마다 JS 왕복을 기다리면 칸이 굼떠 보인다.</summary>
    private void Remember() => _ = Prefs.SaveAsync(new AiAskPref
    {
        TargetKey = _targetKey,
        RunnerKind = _kind,

        // 올리기는 대상의 허용 여부를 따르므로 기억할 때도 켜진 상태로 적는다.
        AutoPush = true,
        NotifyEmail = true,
        NotifyPwa = true,
        ListCols = _cols,
        DraftText = _text,
        DraftSavedAt = _draftSavedAt ?? (_text is not null ? AppTime.UtcNow : null),
    });

    /// <summary>본문 입력을 잠시 뒤에 브라우저 저장소에 적어 둔다.</summary>
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

    /// <summary>추천 프롬프트를 입력창에 채운다.</summary>
    private void SetPrompt(string prompt)
    {
        Text = prompt;
        StateHasChanged();
    }

    /// <summary>대상 목록. 실패해도 화면을 막지 않는다 — 최근 보낸 것은 보여야 빈 이유를 알 수 있다.</summary>
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

    /// <summary>최근 보낸 것. 내가 보낸 것만 거르지 않으므로 전부 온다 — 쓰는 사람이 여럿이 되면 서버에 조건을 더한다.</summary>
    private async Task LoadRecentAsync()
    {
        await LoadAsync(async () =>
        {
            Show(await Api.ListAsync(userConfirmed: false));

            return _recent.Count;
        }, emptyMessage: string.Empty, failMessage: "보낸 것을 읽지 못했습니다");

        // 카드부터 세운다 — 얼굴은 아래에서 따로 채운다.
        _recentLoaded = true;
        StateHasChanged();

        // 지시자의 얼굴은 받아 둔 것 전부를 채운다 — 더보기가 서버를 다시 부르지 않아 여기서 안 채우면 얼굴 없는 카드가 깔린다.
        // 화면을 막지 않는다. 못 읽어도 첫 글자가 선다.
        await Faces.EnsureAsync(_all.Select(t => t.CreId));

        Follow();
    }

    /// <summary>완료인가. 카드에 적히는 글자 그대로다 — 실패·시간초과·취소·중단은 끝났어도 완료가 아니다.</summary>
    private static bool IsDone(AiTaskDto t) => t.TaskStatus == "succeeded";

    /// <summary>받은 것을 최근 순으로 세우고 완료가 아닌 것 전부와 모자란 만큼의 완료된 것을 꺼낸다. 읽는 자리는 둘이어도 자르는 규칙은 이 하나다.</summary>
    private void Show(IReadOnlyList<AiTaskDto> rows)
    {
        _all = rows
            .OrderByDescending(t => t.RequestedAt ?? t.CreDt ?? DateTime.MinValue)
            .ToList();

        _open = _all.Where(t => !IsDone(t)).ToList();
        _done = _all.Where(IsDone).ToList();

        // 안 끝난 것이 한 판을 넘으면 채움은 0 이다 — 깎지는 않는다.
        var page = PageCount;
        var fill = Math.Max(0, page - _open.Count) + (_more * page);

        _recent = _open.Concat(_done.Take(fill)).ToList();
    }

    /// <summary>단 수를 바꾼다. 서버를 부르지 않는다. 「더보기」 횟수는 그대로 둬야 펼쳐 둔 것이 접히지 않는다. 한 판의 장수가 함께 바뀌어 깔린 장수는 달라진다.</summary>
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

    /// <summary>다음에 바뀔 단 수 (1 -> 2 -> 3 -> 4 -> 1).</summary>
    private int NextCols => _cols >= 4 ? 1 : _cols + 1;

    /// <summary>단 아이콘을 누를 때마다 다음 단 수로 바꾼다.</summary>
    private void CycleCols() => SetCols(NextCols);

    /// <summary>완료건 일괄확인을 진행 중인가.</summary>
    private bool _batchConfirming;

    /// <summary>확인 대상인 완료건 수 (TaskStatus == "succeeded").</summary>
    private int CompletedCount => _done.Count;

    /// <summary>완료된 빠른 지시 건을 모두 사용자 확인 완료 처리한다.</summary>
    private async Task ConfirmCompletedBatchAsync()
    {
        if (_batchConfirming)
        {
            return;
        }

        var targets = _done.ToList();
        if (targets.Count == 0)
        {
            return;
        }

        _batchConfirming = true;

        // 열어 둔 탭 중 대상 건이 있으면 닫는다
        foreach (var t in targets)
        {
            CloseView(t.TaskKey);
        }

        // 화면에서 즉시 내린다 (낙관적 갱신)
        var targetKeys = targets.Select(t => t.TaskKey).ToHashSet();
        Show([.. _all.Where(t => !targetKeys.Contains(t.TaskKey))]);
        await InvokeAsync(StateHasChanged);

        try
        {
            await Task.WhenAll(targets.Select(t => Api.ConfirmAsync(t.TaskKey)));
            Say($"완료건 {targets.Count}건을 일괄 확인 처리했습니다.");
        }
        catch (Exception ex)
        {
            Say($"일부 작업을 확인 처리하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _batchConfirming = false;
            await RefreshAsync(CancellationToken.None);
        }
    }

    /// <summary>완료된 것 넉 장 더. 서버를 다시 부르지 않는다.</summary>
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
        _ = ResetMoreObserverAsync();
    }

    /// <summary>대상이 못 쓰는 AI 가 골라져 있으면 맞춘다. 두면 집어 간 뒤에야 어긋남이 드러나 아무 표시 없이 실패한다.</summary>
    private void FixKind()
    {
        var allowed = AllowedKinds;

        if (allowed.Count > 0)
        {
            var matched = allowed.FirstOrDefault(k => string.Equals(k.Value, _kind, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                _kind = matched.Value;
            }
            else
            {
                _kind = allowed[0].Value;
            }
        }
    }

    /// <summary>적은 것을 작업으로 만들고 바로 요청까지 한다. 「AI 작업」과 달리 저장과 요청을 합친다 — 이 화면에 글을 적는 것이 「보내겠다」는 뜻이다.
    /// 단추는 적어 둘 때까지만 묶인다. 요청은 기다리지 않고 <see cref="HandOffAsync"/> 가 뒤에서 한다.</summary>
    private async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        // 올리기를 켠 채로 보내도 묻지 않는다 — 대상이 허용한 것만 켜진다.
        Busy = true;

        // **`try` 밖에 둔다.** 적어 둔 것을 손에 쥔 채로 단추를 먼저 놓아 준다.
        AiTaskDto? created = null;

        try
        {
            FixKind();

            var madeOk = await RunAsync(async () =>
            {
                // 커밋·push·DB 문구를 무조건 붙인다 — 이어서 지시와 같은 글이다. 빠지면 고친 것이 워크트리에 남는다.
                var finalInstruction = AiTaskAlways.Append(_text);

                created = await Api.CreateAsync(new AiTaskDto
                {
                    Contents = finalInstruction,
                    ContentFormat = "markdown",
                    TargetKey = _targetKey,
                    RunnerKind = _kind,
                    // 화면에 제한 칸이 없다 — 「AI 작업」의 기본값인 2시간을 쓴다.
                    TimeoutMinutes = 120,
                    AttemptMax = 3,

                    // 화면이 아니라 PushOn 을 싣는다 — 대상을 바꾸는 사이 옛 값이 남을 수 있다. 서버도 Normalize 에서 한 번 더 본다.
                    AutoPush = PushOn,

                    // 알림 여부는 권한별·사용자 설정이 정하므로 여기서는 항상 요청한다.
                    NotifyEmail = true,
                    NotifyPwa = true,
                    NotifyWhen = "always",

                    // 미리 올린 첨부를 이 건에 묶는다. 서버는 올린 사람이 나인 것만 묶는다(AiTaskFileService.BindAsync).
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

        // 적어 두기가 끝난 뒤다 — 화면을 먼저 비운다.
        // 붙인 것도 비운다 — 방금 묶인 번호는 더는 떠 있는 첨부가 아니다. 남기면 다음 건에 같은 사진이 붙어 보이고 소리 없이 빠진다.
        _attached = [];
        _text = null;
        _draftSavedAt = null;

        CancelDraftDelay();
        _ = Prefs.ClearDraftAsync();

        if (_draftModule is not null)
        {
            _ = _draftModule.InvokeVoidAsync("clearDraft", DraftSelector, Prefs.CurrentKey);
        }

        // 고른 것은 그대로 둔다 — 글상자만 비운다. 같은 자리에 같은 모양을 잇따라 던지는 일이 많다.
        // 켜진 상태는 단추 글자와 묻는 창이 말한다.

        // 기다리지 않는다 — 이 처리기의 실행 맥락에서 이어지므로 Task.Run · InvokeAsync 가 필요 없다.
        _ = HandOffAsync(created.TaskKey);
    }

    /// <summary>적어 둔 것을 실제로 보낸다 — 단추를 놓아 준 뒤에 한다. 만들기와 요청이 따로라 요청이 실패하면 화면이 그 사실을 말해야 한다.
    /// 안 간 것을 갔다고 말하는 것이 이 화면에서 제일 나쁜 실패다. <c>DataPage.RunAsync</c> 는 쓰지 않는다 — 단추를 돌려준 뒤에 조회 중 표시를 잡는다.</summary>
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

        // 성공했든 아니든 목록은 다시 읽는다 — 실패한 건도 「적어만 둔 것」으로 거기 있어야 이어 보낼 수 있다.
        await LoadRecentAsync();

        StateHasChanged();
    }

    /// <summary>휴대폰인가. 카드를 눌렀을 때 판 안의 탭으로 펼칠지 포털 탭으로 옮길지를 이 값이 가른다.</summary>
    private bool _isPhone;

    /// <summary>폭이 경계를 넘었다. 넓어졌으면 판 안의 탭을 걷는다 — 포털 탭 줄과 겹치면 탭 줄이 두 겹이 된다.
    /// 보고 있던 건은 그 주소로 옮겨 간다. 처음 한 번은 거짓에서 거짓으로 오므로 아무 일도 하지 않는다.</summary>
    private void OnPhoneChanged(bool phone)
    {
        if (_isPhone == phone)
        {
            return;
        }

        _isPhone = phone;

        if (phone || _views.Count == 0)
        {
            return;
        }

        var going = _view;

        _views.Clear();
        _view = null;

        if (going is { } key)
        {
            Navigation.NavigateTo($"/projmng/ai/task/{key}");
        }
    }

    /// <summary>카드를 눌렀다. 포털 탭으로 연다 — 다른 메뉴 화면들처럼 레이아웃 상단의 탭 줄에 상세 탭이 선다.</summary>
    private Task PeekAsync(long taskKey)
    {
        Navigation.NavigateTo($"/projmng/ai/task/{taskKey}");
        return Task.CompletedTask;
    }

    /// <summary>한 번에 열어 둘 수 있는 상세 탭 수. 포털 탭 줄은 열둘이지만 여기는 휴대폰 한 줄이라 여섯을 넘기면 찾는 품이 다시 누르는 품보다 크다.</summary>
    private const int MaxViews = 6;

    /// <summary>판 안의 탭으로 열어 둔 건들. 번호만 들고 자료는 목록에서 집는다 — 자료를 붙들면 목록이 새로 읽힐 때 탭만 옛 값에 남는다.</summary>
    private readonly List<long> _views = [];

    /// <summary>지금 보고 있는 건. <c>null</c> 이면 적는 자리와 카드 목록이다.</summary>
    private long? _view;

    /// <summary>지금 보고 있는 건. 휴대폰에서만 값이 있다 — 넓은 화면은 포털 탭이 그 일을 한다.</summary>
    private long? Viewing => _isPhone ? _view : null;

    /// <summary>적는 자리와 카드 목록에 붙일 클래스. 상세를 보는 동안 둘 다 감춘다 — 한쪽만 감추면 상세 아래에 나머지 판이 이어 붙는다.</summary>
    private string? PaneCss => Viewing is null ? null : "pm-ask__pane--off";

    private string WritePaneCss => Viewing is null
        ? "pm-ask__write"
        : "pm-ask__write pm-ask__pane--off";

    private string ListPaneCss => Viewing is null
        ? "pm-ask__recent"
        : "pm-ask__recent pm-ask__pane--off";

    /// <summary>탭이 볼 건. 받아 둔 것 전부에서 찾는다 — 깔린 넉 장에서만 찾으면 새 건이 들어와 밀려날 때 판이 빈다. 목록에서 사라졌으면 <c>null</c>.</summary>
    private AiTaskDto? ViewItem =>
        Viewing is { } key ? _all.FirstOrDefault(t => t.TaskKey == key) : null;

    /// <summary>탭에 적을 이름. 제목이 아직 없으면 번호다 — 제목이 나중에 지어지는 건이 있어 빈 칸이 서면 어느 탭인지 모른다.</summary>
    private string ViewTitle(long key) =>
        _all.FirstOrDefault(t => t.TaskKey == key)?.Title is { Length: > 0 } title
            ? title
            : $"지시 #{key}";

    /// <summary>상세를 탭으로 연다. 이미 열려 있으면 그 탭으로 갈아탈 뿐이다. 상한을 넘으면 가장 먼저 연 것부터 닫되 보고 있는 것은 닫지 않는다.</summary>
    private void OpenView(long taskKey)
    {
        if (!_views.Contains(taskKey))
        {
            _views.Add(taskKey);

            while (_views.Count > MaxViews)
            {
                var oldest = _views.FirstOrDefault(k => k != taskKey);

                if (!_views.Remove(oldest))
                {
                    break;
                }
            }
        }

        _view = taskKey;
        _scroll = ScrollMove.Top;
    }

    /// <summary>다음 그리기 뒤에 구르는 자리를 어떻게 할 것인가.</summary>
    private enum ScrollMove
    {
        /// <summary>그대로 둔다.</summary>
        None,

        /// <summary>맨 위로 — 상세를 열거나 다른 탭으로 갈아탔다.</summary>
        Top,

        /// <summary>목록에서 보던 자리로 — 탭을 닫고 돌아왔다.</summary>
        Restore,
    }

    /// <inheritdoc cref="ScrollMove"/>
    private ScrollMove _scroll = ScrollMove.None;

    private IJSObjectReference? _viewModule;

    /// <summary>구르는 자리를 다루는 모듈. 못 실어도 조용히 넘어간다 — 잃는 것은 구른 자리가 그대로 남는 것뿐이다.</summary>
    private async Task<IJSObjectReference?> ViewModuleAsync()
    {
        try
        {
            return _viewModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/ask-view.js");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            return null;
        }
    }

    /// <summary>목록에서 보던 자리를 적어 둔다. <b>감추기 전에</b> 부른다.</summary>
    private async Task RememberScrollAsync()
    {
        try
        {
            if (await ViewModuleAsync() is { } module)
            {
                await module.InvokeVoidAsync("remember", $"#{_domId}");
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <summary>그려진 뒤에 구르는 자리를 옮긴다.</summary>
    private async Task MoveScrollAsync()
    {
        var move = _scroll;
        _scroll = ScrollMove.None;

        if (move == ScrollMove.None)
        {
            return;
        }

        try
        {
            if (await ViewModuleAsync() is { } module)
            {
                await module.InvokeVoidAsync(
                    move == ScrollMove.Top ? "toTop" : "restore", $"#{_domId}");
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <summary>적는 자리와 카드 목록으로 돌아간다. 열어 둔 탭은 그대로 둔다.</summary>
    private void ShowList()
    {
        _view = null;
        _scroll = ScrollMove.Restore;
    }

    /// <summary>열어 둔 탭 하나로 갈아탄다.</summary>
    private void ShowView(long taskKey)
    {
        _view = taskKey;
        _scroll = ScrollMove.Top;
    }

    /// <summary>탭 하나를 닫는다. 보고 있던 탭이면 오른쪽, 없으면 왼쪽, 그것도 없으면 목록으로 옮겨 간다.</summary>
    private void CloseView(long taskKey)
    {
        var at = _views.IndexOf(taskKey);

        if (at < 0)
        {
            if (_view == taskKey)
            {
                _view = null;
                _scroll = ScrollMove.Restore;
            }
            return;
        }

        _views.RemoveAt(at);

        if (_view != taskKey)
        {
            return;
        }

        _view = _views.Count == 0 ? null : _views[Math.Min(at, _views.Count - 1)];
        _scroll = _view is null ? ScrollMove.Restore : ScrollMove.Top;
    }

    /// <summary>상세에서 재시도·이어서 지시를 했다. 목록을 다시 읽는다.</summary>
    private async Task OnViewChangedAsync(AiTaskDto _)
    {
        await LoadRecentAsync();
        StateHasChanged();
    }

    /// <summary>사용자 확인을 마쳤거나 작성중인 건을 지웠다. 그 탭을 닫는다 — 열어 두면 「그 건을 놓쳤습니다」만 남는다.</summary>
    private async Task OnViewClosedAsync(AiTaskDto task)
    {
        CloseView(task.TaskKey);

        await LoadRecentAsync();
        StateHasChanged();
    }

    /// <summary>목록을 조용히 다시 읽는다. 따라가기와 서랍 다시 펴기가 같이 쓴다. 못 읽으면 다음 바퀴에 다시 본다 — 몇 초마다 토스트를 쌓으면 화면이 오류로 뒤덮인다.</summary>
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
            _recentLoaded = true;

            // 따라가는 동안 남이 보낸 건이 끼어든다 — 모르는 아이디만 묻는다.
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

    /// <summary>보이는 동안 목록을 계속 따라간다. 가려지면 멈춘다 — 안 도는 동안도 끄지 않는다: 다른 자리에서 일어난 일이 영영 안 따라온다.</summary>
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
                    // 잰 다음에 잔다 — 깨어나서 재면 방금 끝난 건을 한 바퀴 더 쫓는다.
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
        _moreRef?.Dispose();
        _moreRef = null;
        _draftRef?.Dispose();
        _draftRef = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopPoll();
        CancelDraftDelay();

        _swipeRef?.Dispose();
        _swipeRef = null;
        _draftRef?.Dispose();
        _draftRef = null;
        if (_moreModule is not null)
        {
            try
            {
                await _moreModule.InvokeVoidAsync("detachMoreObserver", _moreSentinel);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }

        _moreRef?.Dispose();
        _moreRef = null;

        _pullRef?.Dispose();
        _pullRef = null;

        if (_pullModule is not null)
        {
            try
            {
                await _pullModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }

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

        if (_moreModule is not null)
        {
            try
            {
                await _moreModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }

        if (_viewModule is not null)
        {
            try
            {
                await _viewModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException)
            {
            }
        }
    }
}
