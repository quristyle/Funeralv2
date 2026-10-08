using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class LlmHardware
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private LlmHardwareDto? _hw;

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

    protected override async Task RefreshAsync()
        => _hw = await SafeAsync(Api.GetLlmHardwareAsync, _hw);

    private Task ReloadAsync() => LoadOneAsync(
        () => Api.GetLlmHardwareAsync(),
        v => _hw = v,
        "LLM 장비 상태를 받지 못했습니다.",
        "LLM 장비 상태를 조회하지 못했습니다");

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

    private static string UnitName(string? unit) => (unit ?? string.Empty) switch
    {
        "llama-server.service" => "주 모델 (llama-server)",
        "llama-fim.service" => "자동완성 (llama-fim)",
        "nginx.service" => "nginx (인증·중계)",
        "llm-healthcheck.timer" => "상태 감시 타이머",
        var u => u,
    };
}
