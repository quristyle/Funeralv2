using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class LlmHardware
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private LlmHardwareDto? _hw;

    /// <summary>
    /// 사내 문서 검색(RAG)·웹 검색(MCP)의 상태. <b>하드웨어와 따로 온다.</b>
    /// </summary>
    /// <remarks>
    /// 장비가 꺼져 있어도 이 자리는 그려야 한다 — 임베딩 서버와 도구 서버는
    /// 그 장비와 별개로 끊어지고, 반대로 장비가 멀쩡해도 둘 중 하나가 죽으면
    /// 답에서 근거가 통째로 빠진다.
    /// </remarks>
    private AiFeatureStatusDto? _ai;

    /// <summary>사람이 「검색 해 보기」를 눌러 받은 결과. 누르기 전에는 null.</summary>
    private WebSearchCheckDto? _webCheck;

    private bool _webChecking;

    private LlmMetricsDto? M => _hw?.Metrics;

    /// <summary>
    /// 10초. <see cref="ServerStatus"/> 와 같은 간격이다 — 둘 다 장애를 지켜보는 자리다.
    /// </summary>
    /// <remarks>
    /// 더 짧게 할 까닭이 없다. 수집기가 CPU 사용률을 재려고 0.25초를 쓰고,
    /// 온도·클럭은 그보다 느리게 움직인다.
    /// </remarks>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(10);

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    /// <summary>
    /// 자동 조회. <b>둘 다 읽되 눌러 보지는 않는다</b>(<c>recheck</c> 를 안 준다) —
    /// 임베딩 확인은 장비 CPU 를 쓰므로 서버가 2분 동안 재어 둔 값을 받아 온다.
    /// </summary>
    protected override async Task RefreshAsync()
    {
        _hw = await SafeAsync(Api.GetLlmHardwareAsync, _hw);
        _ai = await SafeAsync(ct => Api.GetAiFeatureStatusAsync(false, ct), _ai);
    }

    private async Task ReloadAsync()
    {
        await LoadOneAsync(
            () => Api.GetLlmHardwareAsync(),
            v => _hw = v,
            "LLM 장비 상태를 받지 못했습니다.",
            "LLM 장비 상태를 조회하지 못했습니다");

        // 기능 상태는 **조용히** 따라 읽는다. 장비를 못 읽은 것과 겹쳐
        // 안내 줄이 두 번 뜨면 어느 쪽이 안 되는 것인지 흐려진다.
        _ai = await SafeAsync(ct => Api.GetAiFeatureStatusAsync(false, ct), _ai);
    }

    /// <summary>
    /// 사람이 눌렀을 때. <b>재어 둔 값을 버리고 지금 다시 눌러 본다.</b>
    /// 자동 조회와 달리 실패를 안내 줄로 말한다 — 누른 사람은 답을 기다린다.
    /// </summary>
    private Task RecheckFeaturesAsync() => LoadOneAsync(
        () => Api.GetAiFeatureStatusAsync(recheck: true),
        v => _ai = v,
        "RAG·웹 검색 상태를 받지 못했습니다.",
        "RAG·웹 검색 상태를 조회하지 못했습니다");

    /// <summary>
    /// 웹 검색을 한 번 실제로 돌려 본다. <b>바깥으로 나가는 요청이라</b>
    /// 자동 조회에는 싣지 않는다 — AI 제공자의 「정밀 확인」과 같은 성격이다.
    /// </summary>
    private async Task CheckWebSearchAsync()
    {
        _webChecking = true;
        try
        {
            await LoadOneAsync(
                () => Api.CheckWebSearchAsync(),
                v => _webCheck = v,
                "웹 검색 결과를 받지 못했습니다.",
                "웹 검색을 해 보지 못했습니다");
        }
        finally
        {
            _webChecking = false;
        }
    }

    /// <summary>
    /// 자동 조회가 실패해도 마지막으로 성공한 화면을 그대로 둔다.
    /// 사람이 보고 있지 않을 때 빈 화면으로 바꿔 봐야 쓸모가 없다.
    /// </summary>
    private static async Task<T> SafeAsync<T>(Func<CancellationToken, Task<T>> load, T fallback)
    {
        try
        {
            return await load(CancellationToken.None);
        }
        catch
        {
            return fallback;
        }
    }

    // ── 색 고르기 ───────────────────────────────────────────────

    /// <summary>
    /// GPU 온도의 색. <b>경계를 실측으로 잡았다</b> — 이 장비는 연속 부하에서
    /// GPU0 이 81°C 까지 오르고 그때 생성 속도가 137 → 129 tok/s 로 떨어졌다.
    /// 그래서 75 를 노랑, 83 을 빨강으로 둔다(3090 은 83 부터 클럭을 누른다).
    /// </summary>
    private static string TempTone(int? c) => c switch
    {
        null => "off",
        >= 83 => "down",
        >= 75 => "warn",
        _ => "up",
    };

    /// <summary>차지한 비율의 색. 디스크·메모리·VRAM 이 함께 쓴다.</summary>
    private static string FillTone(double pct) => pct switch
    {
        >= 95 => "down",
        >= 85 => "warn",
        _ => "up",
    };

    /// <summary>
    /// 서비스 한 줄의 색.
    ///
    /// <para>
    /// <b>재시작 횟수를 함께 본다.</b> 반복해 죽는 서비스는 상태만 보면
    /// <c>active</c> 라 멀쩡해 보인다 — systemd 가 곧바로 다시 띄우기 때문이다.
    /// 실제로 이 장비에서 그런 일이 있었다.
    /// </para>
    /// </summary>
    private static string ServiceTone(LlmServiceDto s)
    {
        if (!string.Equals(s.ActiveState, "active", StringComparison.OrdinalIgnoreCase))
        {
            return "down";
        }

        return s.Restarts > 0 ? "warn" : "up";
    }

    private static string BadgeOf(string tone) => tone switch
    {
        "up" => "jsini-badge--on",
        "warn" => "jsini-badge--warn",
        "down" => "jsini-badge--err",
        _ => "jsini-badge--off",
    };

    // ── 숫자를 사람 말로 ────────────────────────────────────────

    private static string Bytes(long? b)
    {
        if (b is null or <= 0) return "-";

        double v = b.Value;
        string[] u = ["B", "KB", "MB", "GB", "TB", "PB"];
        var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v >= 100 ? $"{v:0} {u[i]}" : $"{v:0.0} {u[i]}";
    }

    /// <summary>nvidia-smi 는 MiB 로 준다.</summary>
    private static string Mib(long? mib) => mib is null ? "-" : Bytes(mib.Value * 1024L * 1024L);

    private static string Duration(long? seconds)
    {
        if (seconds is null or < 0) return "-";

        var s = seconds.Value;
        var d = s / 86400;
        var h = s % 86400 / 3600;
        var m = s % 3600 / 60;

        if (d > 0) return $"{d}일 {h}시간";
        if (h > 0) return $"{h}시간 {m}분";
        return $"{m}분";
    }

    private static string Num(double? v, string unit = "", int digits = 0)
        => v is null ? "-" : $"{v.Value.ToString($"N{digits}")}{unit}";

    /// <summary>파라미터 수를 B(십억) 단위로. 80B 를 80,000,000,000 으로 보여 줄 일은 없다.</summary>
    private static string Params(long? p)
        => p is null or <= 0 ? "-" : $"{p.Value / 1_000_000_000.0:0.#}B";

    /// <summary>
    /// 클럭을 누르는 까닭이 있는지. nvidia-smi 는 16진 비트묶음으로 준다.
    /// <b>0 이 아니면 무언가가 성능을 깎고 있다</b>는 뜻이라, 값보다 그 사실이 중요하다.
    /// </summary>
    private static bool IsThrottled(string? reasons)
    {
        if (string.IsNullOrWhiteSpace(reasons)) return false;

        var t = reasons.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber,
                       System.Globalization.CultureInfo.InvariantCulture, out var v) && v != 0;
        }

        return !string.Equals(t, "0", StringComparison.Ordinal);
    }

    /// <summary>
    /// PCIe 폭이 최대보다 좁은지. <b>세대(Gen)는 보지 않는다</b> — 유휴일 때
    /// Gen1 으로 떨어지는 것은 절전이고 부하가 걸리면 올라간다. 폭은 다르다.
    /// 메인보드 레인 배분이라 바뀌지 않는다.
    /// </summary>
    private static bool IsNarrowLink(LlmGpuDto g)
        => g.PcieLinkWidthCurrent is { } cur && g.PcieLinkWidthMax is { } max && cur < max;

    // ── RAG · 웹 검색 ───────────────────────────────────────────

    /// <summary>
    /// 카테고리 한 줄의 색과 한마디.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「토막 N개」만 보면 멀쩡해 보이는 고장이 둘 있다.</b> 올린 문서가
    /// 아직 색인에 안 들어간 것(올릴 때마다 색인을 돌리지 않으므로 생기는
    /// 정상적인 중간 상태지만, 그대로 두면 그 문서는 답의 근거가 되지 않는다)과,
    /// 임베딩이 빠진 채 색인이 만들어진 것이다. 뒤엣것은 토막 수가 그대로라
    /// 숫자로는 전혀 드러나지 않는다 — 그 카테고리는 낱말 검색만 한다.
    /// </para>
    /// <para>
    /// 그래서 <b>나쁜 것부터</b> 따진다. 한 줄에 한마디만 설 수 있다.
    /// </para>
    /// </remarks>
    private static (string Tone, string Label) CategoryState(RagCategoryStatusDto c)
    {
        if (c.Indexed && c.Chunks > 0 && c.VectorChunks < c.Chunks)
        {
            return ("down", "벡터 없음");
        }

        if (!c.Indexed)
        {
            return c.Documents > 0 ? ("warn", "색인 안 함") : ("off", "비어 있음");
        }

        if (c.DocumentsFailed > 0) return ("warn", $"실패 {c.DocumentsFailed}건");
        if (c.DocumentsPending > 0) return ("warn", $"색인 필요 {c.DocumentsPending}건");

        return ("up", "정상");
    }

    /// <summary>
    /// 임베딩 모델이 색인을 만들 때와 달라졌는지.
    /// </summary>
    /// <remarks>
    /// <b>차원이 다른 벡터끼리는 견줄 수 없다.</b> 그런데 코드는 그것을 막지
    /// 않고 그냥 0점으로 셈하므로(<c>RagStore.Dot</c> 가 길이가 다르면 0 을
    /// 준다) 오류 없이 <b>벡터 검색만 통째로 죽는다.</b> 낱말 검색이 남아
    /// 답은 계속 나오므로 사람이 알아챌 길이 이 한 줄뿐이다.
    /// </remarks>
    private static string? DimensionNote(RagStatusDto rag)
    {
        if (rag.Embedding is not { Reachable: true, Dimensions: > 0 } e) return null;

        var odd = rag.Categories
            .Where(c => c.Indexed && c.Dimensions > 0 && c.Dimensions != e.Dimensions)
            .Select(c => c.Name ?? c.Id ?? "?")
            .ToArray();

        return odd.Length == 0
            ? null
            : $"색인의 벡터 차원이 지금 모델과 다릅니다 — {string.Join(", ", odd)}. "
              + "그 카테고리는 벡터 검색이 되지 않으니 색인을 다시 만들어야 합니다.";
    }

    /// <summary>
    /// 도구 서버 칸의 색.
    /// </summary>
    /// <remarks>
    /// <b>「떠 있다」와 「쓸 수 있다」가 다르다.</b> 서버가 응답해도 MCP 규격으로
    /// 붙지 못하면 모델에게 도구가 하나도 없는 것과 같다 — 그때도 대화는
    /// 멀쩡히 돌아가므로(아는 것만으로 답한다) 응답 확인으로는 안 잡힌다.
    /// </remarks>
    private static string WebServerTone(WebSearchStatusDto web)
    {
        if (!web.Enabled) return "off";
        if (web.Connected) return "up";
        return web.Server?.Reachable == true ? "warn" : "down";
    }

    /// <summary>검색 뒷단의 이름. 설정값을 그대로 보여 주되 아는 것은 풀어 적는다.</summary>
    private static string SearchName(string? provider) => (provider ?? string.Empty) switch
    {
        "duckduckgo" => "DuckDuckGo",
        "searxng" => "SearXNG",
        "brave" => "Brave",
        "" => "—",
        var v => v,
    };

    private static string UnitName(string? unit) => (unit ?? string.Empty) switch
    {
        "llama-server.service" => "주 모델 (llama-server)",
        "llama-fim.service" => "자동완성 (llama-fim)",
        "nginx.service" => "nginx (인증·중계)",
        "llm-healthcheck.timer" => "상태 감시 타이머",
        var u => u,
    };
}
