using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class ServerStatus
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private DeployStatusDto? _status;
    private IReadOnlyList<ServiceHealth> _services = [];

    /// <summary>게이트웨이 자신의 상태. 이것이 죽으면 아래 카드가 전부 빨갛다.</summary>
    private ServiceHealth? _gateway;

    /// <summary>게이트웨이를 마지막으로 물어본 시각. 실패하면 갱신하지 않는다.</summary>
    private DateTime? _readAt;
    private AiProviderStatusDto? _ai;

    /// <summary>
    /// 소셜 로그인 설정 상태. AI 제공자와 같은 까닭으로 따로 본다 —
    /// <b>열쇠가 없으면 가입 화면에 단추가 안 서는데 헬스체크는 전부 초록이다.</b>
    /// </summary>
    private SocialConfigStatusDto? _social;
    private IReadOnlyList<AiProviderModelsDto> _models = [];

    /// <summary>제공자별 「정밀 확인」 결과. 누른 것만 들어 있다.</summary>
    private readonly Dictionary<string, AiDeepCheckDto> _deep = new(StringComparer.Ordinal);

    /// <summary>지금 확인 중인 제공자. 같은 단추를 두 번 누르는 것을 막는다.</summary>
    private string? _checking;

    // ── 이미지 정리 (D17) ───────────────────────────────────

    /// <summary>서버에게 물어 온 「지울 목록」. 확인 창이 이것을 그대로 보여 준다.</summary>
    private DockerCleanupDto? _preview;

    private bool _confirmVisible;

    /// <summary>미리보기·지우기가 도는 중. 같은 단추를 두 번 누르는 것을 막는다.</summary>
    private bool _cleaning;

    /// <summary>
    /// 공급자 콘솔에 등록할 콜백 주소를 완성한다.
    /// </summary>
    /// <remarks>
    /// origin 을 <b>화면이 붙인다</b> — AuthServer 는 게이트웨이 뒤라 포털의 바깥
    /// 주소를 모르고, 그래서 경로만 보내 준다(<c>SocialProviderConfigDto.CallbackPath</c>).
    /// 지금 보고 있는 주소를 그대로 쓰므로 <c>SocialLoginFlow.CallbackUri</c> 가
    /// 실제로 만드는 값과 같아진다.
    /// </remarks>
    private string CallbackUrl(SocialProviderConfigDto p) =>
        $"{new Uri(Navigation.BaseUri).GetLeftPart(UriPartial.Authority)}{p.CallbackPath}";

    private IReadOnlyList<DockerContainerDto> Containers => _status?.Docker.Containers ?? [];
    private IReadOnlyList<DockerImageDto> Images => _status?.Docker.Images ?? [];

    /// <summary>옛 화면과 같은 10초. 이 화면은 장애를 지켜보는 자리다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(10);

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    /// <summary>
    /// 자동 조회.
    ///
    /// <para>
    /// <b>정밀 확인(`ai/health/deep`)은 부르지 않는다</b> — 유료 제공자에게
    /// 실제 요청을 보내는 것이라 10초마다 돌면 돈이 나간다. 그것은 사람이
    /// 누를 때만 한다. 옛 화면도 자동 갱신에서는 목록만 다시 읽었다.
    /// </para>
    /// </summary>
    protected override async Task RefreshAsync()
    {
        await LoadGatewayAsync();
        _ai = await SafeAsync(Api.GetAiProvidersAsync, null);
        _social = await SafeAsync(Api.GetSocialConfigStatusAsync, null);
        _status = await Api.GetDeployStatusAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 둘 중 하나가 실패해도 나머지는 보여 준다. 서버 상태를 보러 온
        // 사람에게 「둘 다 못 읽었습니다」는 쓸모가 없다.
        await LoadGatewayAsync();
        _ai = await SafeAsync(Api.GetAiProvidersAsync, null);
        _models = await SafeAsync(Api.GetAiModelsAsync, []);
        _social = await SafeAsync(Api.GetSocialConfigStatusAsync, null);
        _status = await Api.GetDeployStatusAsync();

        return _services.Count + Containers.Count;
    }, "상태 정보를 받지 못했습니다.", "서버 상태를 읽지 못했습니다");

    /// <summary>
    /// 「지울 목록만」 먼저 물어보고 확인 창을 연다 (D17).
    ///
    /// 목록이 비어도 창은 연다 — 눌렀는데 아무 일도 일어나지 않으면 고장으로
    /// 읽힌다. 「지울 것이 없습니다」도 답이다.
    /// </summary>
    private async Task PreviewCleanupAsync()
    {
        if (_cleaning) return;

        _cleaning = true;
        _preview = null;
        try
        {
            var ok = await RunAsync(
                async () => _preview = await Api.CleanupDockerImagesAsync(dryRun: true),
                okMessage: string.Empty,
                failMessage: "지울 목록을 받지 못했습니다");

            if (ok)
            {
                // RunAsync 가 성공 문구를 남기는데, 여기서는 창이 곧 답이라
                // 안내 줄을 비운다.
                Say(null);
                _confirmVisible = true;
            }
        }
        finally
        {
            _cleaning = false;
        }
    }

    /// <summary>
    /// 실제로 지운다. <b>여기부터는 되돌릴 수 없다.</b>
    ///
    /// 끝나면 상태를 다시 읽는다 — 「쌓인 이미지」 표가 줄어든 것이 보여야
    /// 정말 지워졌는지 알 수 있다.
    /// </summary>
    private async Task RunCleanupAsync()
    {
        if (_cleaning) return;

        _cleaning = true;
        try
        {
            DockerCleanupDto? result = null;
            var ok = await RunAsync(
                async () => result = await Api.CleanupDockerImagesAsync(dryRun: false),
                okMessage: "이미지를 정리했습니다.",
                failMessage: "이미지를 정리하지 못했습니다");

            _confirmVisible = false;

            // **다시 읽는 것이 먼저다.** ReloadAsync 는 시작할 때 안내 줄을
            // 비우므로, 결과를 먼저 적으면 그 자리에서 지워진다. 표가 줄어든
            // 것을 보여 준 다음 무엇을 했는지 말한다.
            await ReloadAsync();

            if (ok && result is not null)
            {
                // 일부만 지워지는 일이 있다(다른 태그가 같은 레이어를 잡고 있는
                // 경우 등). 「정리했습니다」로 뭉뚱그리면 표가 안 줄어든 이유를
                // 알 수 없으므로 실패 건수를 함께 말한다.
                var message = $"이미지 {result.Removed.Count}개를 지웠습니다 ({result.SpaceReclaimedMb} MB 회수).";
                if (result.Errors.Count > 0)
                {
                    message += $" 지우지 못한 것 {result.Errors.Count}개 — {string.Join(", ", result.Errors.Take(3))}";
                }

                Say(message, result.Errors.Count > 0 ? NoticeTone.Warning : NoticeTone.Info);
            }
        }
        finally
        {
            _cleaning = false;
        }
    }

    /// <summary>
    /// 게이트웨이 상태를 한 번 읽어 자신과 서비스 목록을 함께 채운다.
    ///
    /// 둘이 <b>같은 응답</b>에서 온다. 따로 부르면 왕복이 두 번이고, 그 사이에
    /// 값이 갈려 「게이트웨이는 정상인데 서비스가 전부 중지」처럼 어긋난 화면이 난다.
    /// </summary>
    private async Task LoadGatewayAsync()
    {
        var status = await SafeAsync(Api.GetGatewayStatusAsync, null);
        if (status is null)
        {
            // 못 읽었으면 **마지막으로 성공한 화면을 그대로 둔다.** 여기서 목록을
            // 비우면 「전부 중지」로 보이는데, 실제로 끊긴 것은 게이트웨이 하나이거나
            // 우리 쪽 회선일 수 있다. 멎어 있는 시각이 그 사실을 말해 준다.
            _gateway = null;
            return;
        }

        _gateway = status.Gateway;
        _services = status.Services;
        _readAt = DateTime.Now;
    }

    // ── 카드 꾸밈 ───────────────────────────────────────────
    //
    // 이름과 한 줄 설명은 **꾸밈말일 뿐**이다. 여기 없는 클러스터가 와도
    // 클러스터 이름이 그대로 나온다 — 서비스를 늘릴 때 이 표를 고치지
    // 않아도 화면이 깨지지 않아야 한다.

    private static readonly Dictionary<string, (string Name, string Desc)> ClusterMeta =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["auth-cluster"] = ("AuthServer", "인증 · JWT 발급 · 공통코드"),
            ["funeral-cluster"] = ("funeralv2Api", "장례식장 업무 API · 장비 실시간 허브"),
            ["file-cluster"] = ("FileServer", "파일 올리기 · 변환 · 내려받기"),
            ["ai-cluster"] = ("AIAgentServer", "AI 에이전트 · 번역 · 추천"),
            ["helpdesk-cluster"] = ("HelpDeskServer", "헬프데스크 · 요청/WBS · 일정"),
            ["projmng-cluster"] = ("ProjMngServer", "프로젝트관리 · WBS · 개발도구"),
            ["site-cluster"] = ("SiteServer", "회사 소개 사이트 · 문의 접수"),
            ["life-cluster"] = ("LifeEnvServer", "생활과환경 · 기상 · 생일"),
            ["cargotrust-cluster"] = ("CargoTrustServer", "운송관리 · 거래처 신뢰정보"),
            ["notification-cluster"] = ("NotificationServer", "푸시 · 이메일 발송"),

            // 우리 서비스가 아니다. 외부 고객사 시스템이라 `/health` 규약을
            // 따르지 않고 게이트웨이도 헬스체크를 걸지 않는다. 이름을 그대로
            // 두면 **우리 MSA 하나가 죽은 것처럼 읽힌다.**
            ["oadr-cluster"] = ("OADR (외부)", "외부 고객사 시스템 · 헬스체크 대상 아님"),
        };

    private static (string Name, string Desc) MetaOf(string? cluster) =>
        cluster is { Length: > 0 } c && ClusterMeta.TryGetValue(c, out var meta)
            ? meta
            : (cluster ?? "gateway", string.Empty);

    /// <summary>
    /// 게이트웨이가 주는 <c>UP</c> · <c>DEGRADED</c> · <c>DOWN</c> 를 색 이름으로.
    ///
    /// <para>
    /// <b>가운데를 따로 두는 것이 요점이다.</b> <c>DEGRADED</c> 는 프로세스가
    /// 살아 있고 <b>딸린 것</b>(DB · AI 모델 · 큐)이 끊긴 상태다. 빨강으로
    /// 칠하면 「서비스가 죽었다」로 읽혀 엉뚱한 곳을 보러 가게 된다.
    /// </para>
    /// </summary>
    private static string ToneOf(string? status) => status?.ToUpperInvariant() switch
    {
        "UP" => "up",
        "DEGRADED" => "warn",
        _ => "down",
    };

    private static string LabelOf(string tone) => tone switch
    {
        "up" => "정상",
        "warn" => "응답 이상",
        _ => "중지",
    };

    private static string BadgeOf(string tone) => tone switch
    {
        "up" => "jsini-badge--on",
        "warn" => "jsini-badge--warn",
        _ => "ad-badge--err",
    };

    /// <summary>게이트웨이 자신. 응답이 없으면 중지로 본다.</summary>
    private string GatewayTone => ToneOf(_gateway?.Status ?? (_services.Count > 0 ? "UP" : null));

    private int CountOf(string tone) => _services.Count(s => ToneOf(s.Status) == tone);

    /// <summary>
    /// 성한 것보다 <b>탈난 것을 먼저</b> 세운다.
    ///
    /// <para>
    /// 게이트웨이가 주는 차례에는 뜻이 없어서, 열몇 장 중에 빨간 카드 한 장을
    /// 눈으로 찾아야 했다. 카드는 표와 달리 정렬 단추가 없으므로 화면이 정한다.
    /// 상태가 바뀔 때만 자리가 움직인다 — 그때는 <b>움직이는 것이 맞다.</b>
    /// </para>
    /// </summary>
    /// <summary>
    /// 지금 상세를 펴 놓은 서비스의 열쇠.
    ///
    /// <para>
    /// <b>인덱스가 아니라 열쇠로 붙든다.</b> 이 화면은 10초마다 스스로 되묻고,
    /// 목록은 상태 나쁜 것이 앞으로 오게 정렬된다(<see cref="SortedServices"/>).
    /// 자리로 잡아 두면 어느 서비스가 죽는 순간 순서가 바뀌면서 보고 있던
    /// 상세가 말없이 다른 서비스로 갈린다.
    /// </para>
    /// </summary>
    private string? _selectedKey;

    /// <summary>클러스터와 목적지를 합친 열쇠. 클러스터 하나에 목적지가 여럿일 수 있다.</summary>
    private static string KeyOf(ServiceHealth svc) => $"{svc.Cluster}::{svc.Destination}";

    /// <summary>
    /// 상세를 펼 서비스. 고른 것이 사라졌거나 아직 안 골랐으면
    /// <b>가장 나쁜 것</b>을 고른다 — 이 화면을 여는 이유가 그것이다.
    /// </summary>
    private ServiceHealth? Selected
    {
        get
        {
            if (_selectedKey is { Length: > 0 } key
                && _services.FirstOrDefault(s => KeyOf(s) == key) is { } found)
            {
                return found;
            }

            return SortedServices.FirstOrDefault();
        }
    }

    // ── 컨테이너 고르기 ────────────────────────────────────────
    //
    // 서비스 쪽과 **같은 규칙**이다. 값과 이름만 다르고, 고른 것이 사라졌거나
    // 아직 안 골랐으면 가장 나쁜 것을 편다.

    /// <summary>
    /// 오른쪽 상세가 무엇을 펴고 있나. <b>상세가 한 자리</b>라 이 값이 있어야
    /// 한다 — 열쇠 둘(<c>_selectedKey</c> · <c>_boxKey</c>)은 서로 지우지
    /// 않으므로, 갈래를 따로 적지 않으면 컨테이너를 누른 뒤에도 서비스 상세가
    /// 남는다.
    /// </summary>
    private enum PickKind { Service, Container }

    private PickKind _pick = PickKind.Service;

    private string? _boxKey;

    /// <summary>
    /// 컨테이너를 가리키는 열쇠. <b>compose 서비스 이름</b>이다 —
    /// 컨테이너 아이디는 다시 만들 때마다 바뀌어서, 그것을 열쇠로 두면
    /// 자동 갱신 한 번에 골라 둔 것이 풀린다.
    /// </summary>
    private static string BoxKey(DockerContainerDto box) =>
        box.Service ?? box.Image ?? string.Empty;

    private static bool IsUp(string? state) =>
        string.Equals(state, "running", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 컨테이너 상태를 카드 색으로. <c>restarting</c> 을 <b>죽은 것과 가르는</b>
    /// 것이 요점이다 — 되살아나려고 도는 중인 것과 멈춘 것은 할 일이 다르다.
    /// </summary>
    private static string BoxTone(string? state) => state?.ToLowerInvariant() switch
    {
        "running" => "up",
        "restarting" or "created" or "paused" => "warn",
        null => "warn",
        _ => "down",
    };

    private IEnumerable<DockerContainerDto> SortedContainers => Containers
        .OrderBy(b => BoxTone(b.State) switch { "down" => 0, "warn" => 1, _ => 2 })
        .ThenBy(b => b.Service, StringComparer.OrdinalIgnoreCase);

    private DockerContainerDto? PickedContainer
    {
        get
        {
            if (_boxKey is { Length: > 0 } key
                && Containers.FirstOrDefault(b => BoxKey(b) == key) is { } found)
            {
                return found;
            }

            return SortedContainers.FirstOrDefault();
        }
    }

    /// <summary>
    /// 이 컨테이너에 짝지어지는 서비스 응답. <b>못 찾으면 <c>null</c></b> 이고
    /// 그때는 상세에 응답 칸이 아예 안 뜬다 — 짝이 없는 컨테이너(nginx·db 따위)에
    /// 빈 칸을 그리면 「응답을 못 받았다」로 읽힌다.
    /// </summary>
    private ServiceHealth? ServiceOf(DockerContainerDto box) =>
        box.Service is not { Length: > 0 } name
            ? null
            : _services.FirstOrDefault(s =>
                  string.Equals(s.Cluster, name, StringComparison.OrdinalIgnoreCase)
                  || string.Equals(s.Cluster, $"{name}-cluster", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 카드에 적을 태그. 커밋 SHA 는 마흔 자라 카드를 넘치므로 앞을 자른다 —
    /// 전체는 툴팁과 상세에 있다.
    /// </summary>
    private static string Short(string? tag) =>
        tag is not { Length: > 0 } ? "-" : tag.Length <= 12 ? tag : tag[..12] + "…";

    /// <summary>딸린 것의 상태를 타일의 점 색으로 줄인다.</summary>
    private static string DepTone(string? status) => status switch
    {
        "Healthy" => "up",
        "Degraded" => "warn",
        _ => "down",
    };

    private IEnumerable<ServiceHealth> SortedServices => _services
        .OrderBy(s => ToneOf(s.Status) switch { "down" => 0, "warn" => 1, _ => 2 })
        .ThenBy(s => MetaOf(s.Cluster).Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>느린 것을 눈에 걸리게 한다. 살아 있는 것과 쓸 만한 것은 다르다.</summary>
    private static string LatencyClass(int? ms) => ms switch
    {
        null => string.Empty,
        >= 1000 => "ad-lat--bad",
        >= 300 => "ad-lat--slow",
        _ => string.Empty,
    };

    /// <summary>점검 이름을 사람이 읽는 말로. 모르는 이름은 그대로 보여 준다.</summary>
    private static string DepLabel(string? name) => name switch
    {
        "database" => "DB",
        "llm" => "AI 모델",
        "release-queue" => "배포 큐",
        "storage" => "파일 저장소",
        _ => name ?? "?",
    };

    private static string DepText(string? status) => status switch
    {
        "Healthy" => "연결됨",
        "Degraded" => "연결 안 됨",
        _ => "오류",
    };

    private static string DepBadge(string? status) => status switch
    {
        "Healthy" => "jsini-badge--on",
        "Degraded" => "jsini-badge--warn",
        _ => "ad-badge--err",
    };

    private IReadOnlyList<string> ModelsOf(string key) =>
        _models.FirstOrDefault(m => string.Equals(m.Provider, key, StringComparison.OrdinalIgnoreCase))?.Models ?? [];

    private static string DeepTone(AiDeepCheckDto result) =>
        result.Ok ? "jsini-badge--on" : result.RateLimited ? "jsini-badge--warn" : "jsini-badge--off";

    /// <summary>
    /// 제공자 하나를 실제로 눌러 본다.
    ///
    /// 실패도 결과다 — 서버가 200 에 이유를 담아 준다. 그래서 예외로 다루지
    /// 않고 그대로 칸에 보여 준다. 연결 자체가 안 될 때만 안내로 올린다.
    /// </summary>
    private async Task DeepCheckAsync(string provider)
    {
        _checking = provider;
        _deep.Remove(provider);

        try
        {
            var result = await Api.DeepCheckAiAsync(provider);

            if (result is not null)
            {
                _deep[provider] = result;
            }
        }
        catch (ApiException ex)
        {
            Say($"{provider} 를 확인하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _checking = null;
        }
    }

    /// <summary>실패해도 화면을 세우지 않는 조회. 빈 값으로 떨어진다.</summary>
    private static async Task<T> SafeAsync<T>(Func<CancellationToken, Task<T>> load, T fallback)
    {
        try
        {
            return await load(default);
        }
        catch (ApiException)
        {
            return fallback;
        }
    }
}
