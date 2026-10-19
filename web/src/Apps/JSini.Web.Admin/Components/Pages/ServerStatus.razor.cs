using JSini.Web.Components.Data;
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

    /// <summary>소셜 로그인 설정 상태. 열쇠가 없으면 가입 화면에 단추가 안 서는데 헬스체크는 초록이라 따로 본다.</summary>
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

    /// <summary>공급자 콘솔에 등록할 콜백 주소를 완성한다. origin 은 화면이 붙인다 — AuthServer 는 게이트웨이 뒤라 바깥 주소를 모른다.</summary>
    private string CallbackUrl(SocialProviderConfigDto p) =>
        $"{new Uri(Navigation.BaseUri).GetLeftPart(UriPartial.Authority)}{p.CallbackPath}";

    private IReadOnlyList<DockerContainerDto> Containers => _status?.Docker.Containers ?? [];
    private IReadOnlyList<DockerImageDto> Images => _status?.Docker.Images ?? [];

    /// <summary>쌓인 이미지 목록 팝업 열림 여부.</summary>
    private bool _imageListVisible;

    private long TotalImagesMb => _status?.Docker.ImagesTotalMb > 0
        ? _status.Docker.ImagesTotalMb
        : (Images.Count > 0 ? Images.Sum(i => i.SizeMb) : 0);

    private int InUseImagesCount => Images.Count(i => i.InUse);
    private long InUseImagesMb => Images.Where(i => i.InUse).Sum(i => i.SizeMb);

    private int UnusedImagesCount => Images.Count(i => !i.InUse);
    private long UnusedImagesMb => Images.Where(i => !i.InUse).Sum(i => i.SizeMb);

    /// <summary>옛 화면과 같은 10초. 이 화면은 장애를 지켜보는 자리다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(10);

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    /// <summary>자동 조회. 정밀 확인(ai/health/deep)은 부르지 않는다 — 유료 제공자에게 실제 요청을 보내므로 사람이 누를 때만 한다.</summary>
    protected override async Task RefreshAsync()
    {
        await LoadGatewayAsync();
        _ai = await SafeAsync(Api.GetAiProvidersAsync, null);
        _social = await SafeAsync(Api.GetSocialConfigStatusAsync, null);
        _status = await Api.GetDeployStatusAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 둘 중 하나가 실패해도 나머지는 보여 준다.
        await LoadGatewayAsync();
        _ai = await SafeAsync(Api.GetAiProvidersAsync, null);
        _models = await SafeAsync(Api.GetAiModelsAsync, []);
        _social = await SafeAsync(Api.GetSocialConfigStatusAsync, null);
        _status = await Api.GetDeployStatusAsync();

        return _services.Count + Containers.Count;
    }, "상태 정보를 받지 못했습니다.", "서버 상태를 읽지 못했습니다");

    /// <summary>「지울 목록만」 먼저 물어보고 확인 창을 연다. 목록이 비어도 창은 연다 — 「지울 것이 없습니다」도 답이다.</summary>
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
                // 창이 곧 답이라 안내 줄을 비운다.
                Say(null);
                _confirmVisible = true;
            }
        }
        finally
        {
            _cleaning = false;
        }
    }

    /// <summary>실제로 지운다. 여기부터는 되돌릴 수 없다. 끝나면 상태를 다시 읽어 표가 줄었는지 보여 준다.</summary>
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

            // 다시 읽는 것이 먼저다 — ReloadAsync 가 안내 줄을 비우므로 결과는 그 뒤에 적는다.
            await ReloadAsync();

            if (ok && result is not null)
            {
                // 일부만 지워지는 일이 있어 실패 건수를 함께 말한다.
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

    /// <summary>게이트웨이 상태를 한 번 읽어 자신과 서비스 목록을 함께 채운다. 둘이 같은 응답에서 와야 화면이 어긋나지 않는다.</summary>
    private async Task LoadGatewayAsync()
    {
        var status = await SafeAsync(Api.GetGatewayStatusAsync, null);
        if (status is null)
        {
            // 못 읽었으면 마지막으로 성공한 화면을 그대로 둔다 — 비우면 끊긴 게이트웨이 하나가 전부 중지로 보인다.
            _gateway = null;
            return;
        }

        _gateway = status.Gateway;
        _services = status.Services;
        _readAt = AppTime.UtcNow;
    }

    // ── 카드 꾸밈 ───────────────────────────────────────────
    //
    // 이름과 설명은 꾸밈말일 뿐이라 표에 없는 클러스터도 이름을 그대로 보여 준다.

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

            // 게이트웨이가 중계하지 않고 상태만 보는 서비스다(모델이 쓰는 바깥 도구). 빨개도 AI 는 돈다.
            // 이름이 -cluster 가 아닌 것은 셸이 하이픈 이름을 버려서다(ApiGateway/appsettings.json).
            ["mcp_cluster"] = ("McpServer", "MCP 도구 · 웹 검색 · 본문 가져오기"),

            // 우리 서비스가 아니라 헬스체크 규약을 따르지 않는다 — 이름을 그대로 두면 우리 MSA 하나가 죽은 것으로 읽힌다.
            ["oadr-cluster"] = ("OADR (외부)", "외부 고객사 시스템 · 헬스체크 대상 아님"),
        };

    private static (string Name, string Desc) MetaOf(string? cluster) =>
        cluster is { Length: > 0 } c && ClusterMeta.TryGetValue(c, out var meta)
            ? meta
            : (cluster ?? "gateway", string.Empty);

    /// <summary>게이트웨이의 UP · DEGRADED · DOWN 을 색 이름으로. 가운데는 따로 둔다 — DEGRADED 는 프로세스는 살아 있고 딸린 것(DB · AI 모델 · 큐)이 끊긴 상태라 빨강으로 칠하면 죽은 것으로 읽힌다.</summary>
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

    /// <summary>성한 것보다 탈난 것을 먼저 세운다. 상태가 바뀔 때만 자리가 움직인다.</summary>
    /// <summary>지금 상세를 펴 놓은 서비스의 열쇠. 인덱스가 아니라 열쇠로 붙든다 — 자리로 잡으면 정렬이 바뀌는 순간 보던 상세가 다른 서비스로 갈린다.</summary>
    private string? _selectedKey;

    /// <summary>클러스터와 목적지를 합친 열쇠. 클러스터 하나에 목적지가 여럿일 수 있다.</summary>
    private static string KeyOf(ServiceHealth svc) => $"{svc.Cluster}::{svc.Destination}";

    /// <summary>상세를 펼 서비스. 고른 것이 사라졌거나 아직 안 골랐으면 가장 나쁜 것을 고른다.</summary>
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

    // 서비스 쪽과 같은 규칙이다 — 고른 것이 사라졌거나 안 골랐으면 가장 나쁜 것을 편다.

    /// <summary>오른쪽 상세가 무엇을 펴고 있나. 상세는 한 자리라 갈래를 적어야 컨테이너를 누른 뒤 서비스 상세가 남지 않는다.</summary>
    private enum PickKind { Service, Container }

    private PickKind _pick = PickKind.Service;

    private string? _boxKey;

    /// <summary>컨테이너를 가리키는 열쇠. compose 서비스 이름이다 — 컨테이너 아이디는 다시 만들 때 바뀌어 자동 갱신에 풀린다.</summary>
    private static string BoxKey(DockerContainerDto box) =>
        box.Service ?? box.Image ?? string.Empty;

    private static bool IsUp(string? state) =>
        string.Equals(state, "running", StringComparison.OrdinalIgnoreCase);

    /// <summary>컨테이너 상태를 카드 색으로. restarting 은 죽은 것과 가른다 — 되살아나려 도는 중과 멈춘 것은 할 일이 다르다.</summary>
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

    /// <summary>이 컨테이너에 짝지어지는 서비스 응답. 못 찾으면 null 이고, 그때 상세에 응답 칸이 안 뜬다.</summary>
    private ServiceHealth? ServiceOf(DockerContainerDto box) =>
        box.Service is not { Length: > 0 } name
            ? null
            : _services.FirstOrDefault(s =>
                  string.Equals(s.Cluster, name, StringComparison.OrdinalIgnoreCase)
                  || string.Equals(s.Cluster, $"{name}-cluster", StringComparison.OrdinalIgnoreCase));

    /// <summary>카드에 적을 태그. 커밋 SHA 는 앞을 자른다 — 전체는 툴팁과 상세에 있다.</summary>
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

    /// <summary>제공자 하나를 실제로 눌러 본다. 실패도 결과라 서버가 준 이유를 칸에 보여 주고, 연결이 안 될 때만 안내로 올린다.</summary>
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
