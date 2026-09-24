using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>
/// 구독한 기기 한 대를 <b>사람이 읽는 글자</b>로 바꾼다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 화면 밖으로 꺼냈나]
/// </para>
///
/// <para>
/// 원래 <c>NotificationPanel</c> 안의 private 메서드였다. 그 화면은 <b>자기
/// 기기</b>를 보여 주는 자리인데, 포털관리에 <b>남의 기기</b>를 보는 화면
/// (<c>/admin/system/account/{아이디}/app</c>)이 생기면서 같은 글자가 두 곳에
/// 필요해졌다.
/// </para>
///
/// <para>
/// 복제하면 갈라지는 자리가 뻔하다 — 브라우저 판별은 새 브라우저가 나올 때마다
/// 손보게 되는데, 그때 한쪽만 고치면 <b>같은 기기가 화면마다 다른 이름으로</b>
/// 보인다. 그 어긋남은 「내 기기가 목록에 없다」로 신고가 들어온다.
/// </para>
/// </remarks>
public static class PushDeviceLabel
{
    /// <summary>
    /// 사람이 읽는 기기 이름.
    ///
    /// <para>
    /// <b>Edge·삼성 인터넷을 먼저 가른다.</b> 그 둘은 자기 표시 뒤에 <c>Chrome</c>
    /// 도 함께 적어서, 순서를 바꾸면 전부 Chrome 으로 보인다.
    /// </para>
    /// </summary>
    public static string Name(PushDeviceDto device)
    {
        var metadata = device.Metadata;
        var type = metadata.DeviceType switch
        {
            "mobile" => "휴대폰",
            "tablet" => "태블릿",
            "desktop" => "PC",
            _ => null
        };
        var makerModel = string.Join(" ", new[] { metadata.DeviceVendor, metadata.DeviceModel }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var platform = string.Join(" ", new[] { metadata.Platform, metadata.PlatformVersion }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(type) &&
            (!string.IsNullOrWhiteSpace(makerModel) || !string.IsNullOrWhiteSpace(platform)))
        {
            return string.Join(" · ", new[] { type, makerModel, platform }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        return FromUserAgent(device.UserAgent);
    }

    /// <summary>이름 아래 한 줄로 덧붙이는 자잘한 것들(브라우저 · 화면 크기 · 시간대).</summary>
    public static string Details(PushDeviceDto device)
    {
        var metadata = device.Metadata;
        var parts = new List<string>();
        var browser = string.Join(" ", new[] { metadata.Browser, metadata.BrowserVersion }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(browser)) parts.Add(browser);
        if (!string.IsNullOrWhiteSpace(metadata.DisplayMode) && metadata.DisplayMode != "browser")
            parts.Add($"앱 {metadata.DisplayMode}");
        if (metadata.ScreenWidth is > 0 && metadata.ScreenHeight is > 0)
            parts.Add($"{metadata.ScreenWidth}×{metadata.ScreenHeight}px");
        if (metadata.MaxTouchPoints is > 0) parts.Add($"터치 {metadata.MaxTouchPoints}");
        if (!string.IsNullOrWhiteSpace(metadata.TimeZone)) parts.Add(metadata.TimeZone);
        if (!string.IsNullOrWhiteSpace(metadata.Language)) parts.Add(metadata.Language);
        return parts.Count > 0 ? string.Join(" · ", parts) : "추가 장비 정보 없음";
    }

    /// <summary>
    /// <b>앱으로 설치해 쓰는 기기인가.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 구독할 때 브라우저가 적어 보낸 값 둘을 본다 — <c>isStandalone</c> 과
    /// <c>displayMode</c>. 앞엣것이 정답이지만 <b>옛 구독에는 없다</b>(그 칸이
    /// 생기기 전에 등록한 기기). 그때는 표시 방식이 <c>browser</c> 가 아닌지로
    /// 가른다(<c>standalone</c> · <c>fullscreen</c> · <c>minimal-ui</c>).
    /// </para>
    /// <para>
    /// 둘 다 비어 있으면 <c>null</c> 이다 — <b>「설치 안 함」이 아니라 「모른다」</b>.
    /// 모르는 것을 「안 함」으로 그리면 옛 기기가 전부 설치 안 한 것으로 보인다.
    /// </para>
    /// </remarks>
    public static bool? Installed(PushDeviceDto device)
    {
        var metadata = device.Metadata;

        if (metadata.IsStandalone is { } standalone)
        {
            return standalone;
        }

        return string.IsNullOrWhiteSpace(metadata.DisplayMode)
            ? null
            : metadata.DisplayMode != "browser";
    }

    /// <summary>
    /// 장비 정보가 없을 때 원문 UA 에서 뽑는 이름.
    ///
    /// <para>
    /// <b>원문을 그대로 두지 않는다.</b> 「Mozilla/5.0 (Windows NT 10.0…」 는
    /// 기기를 고르는 데 아무 도움이 안 된다.
    /// </para>
    /// </summary>
    public static string FromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "알 수 없는 기기";
        }

        var ua = userAgent;

        var browser =
            ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge"
            : ua.Contains("SamsungBrowser", StringComparison.Ordinal) ? "삼성 인터넷"
            : ua.Contains("OPR/", StringComparison.Ordinal) || ua.Contains("Opera", StringComparison.Ordinal) ? "Opera"
            : ua.Contains("Whale", StringComparison.Ordinal) ? "웨일"
            : ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox"
            : ua.Contains("Chrome/", StringComparison.Ordinal) ? "Chrome"
            : ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari"
            : "브라우저";

        var os =
            ua.Contains("Windows", StringComparison.Ordinal) ? "Windows"
            : ua.Contains("Android", StringComparison.Ordinal) ? "Android"
            : ua.Contains("iPhone", StringComparison.Ordinal)
              || ua.Contains("iPad", StringComparison.Ordinal)
              || ua.Contains("iPod", StringComparison.Ordinal) ? "iOS"
            : ua.Contains("Mac OS X", StringComparison.Ordinal) ? "macOS"
            : ua.Contains("Linux", StringComparison.Ordinal) ? "Linux"
            : string.Empty;

        return os.Length > 0 ? $"{browser} · {os}" : browser;
    }
}
