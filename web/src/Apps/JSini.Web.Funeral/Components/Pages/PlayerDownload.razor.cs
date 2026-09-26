using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class PlayerDownload
{
    [Inject] private HelpApi Api { get; set; } = default!;

    /// <summary>
    /// OS 별 카드 한 장.
    ///
    /// <para>
    /// <c>Matches</c> 는 자산 이름(소문자)을 받아 이 카드의 파일인지 답한다.
    /// <b>배포판과 아키텍처까지 본다</b> — 까닭은 화면 머리말에 있다.
    /// </para>
    /// </summary>
    private sealed record Platform(
        string Title,
        string Subtitle,
        Func<string, bool> Matches,
        string[] Requirements,
        string[] Steps);

    /// <summary>원본(Vue)의 일곱 카드를 그대로 옮겼다. 순서도 같다.</summary>
    private static readonly Platform[] Platforms =
    [
        new("Windows",
            "10 / 11 · 64비트 (x64)",
            n => n.Contains("windows") && n.EndsWith(".zip"),
            ["Windows 10 / 11 (64비트)", "Visual C++ 2015-2022 재배포 패키지"],
            ["zip 압축을 풀고 funeralv2_player.exe 실행"]),

        new("라즈베리파이",
            "Raspberry Pi OS 64비트 · .deb",
            n => n.Contains("debian13") && n.Contains("arm64") && n.EndsWith(".deb"),
            ["Raspberry Pi OS Lite 64비트 (Debian 13 trixie)", "Raspberry Pi 4 이상"],
            ["sudo apt install ./funeralv2-player_<버전>_debian13_arm64.deb",
             "sudo reboot  (재부팅하면 화면에 자동 실행)"]),

        new("Ubuntu (x64)",
            "24.04 LTS · 미니PC · .deb",
            n => n.Contains("ubuntu24") && n.Contains("amd64") && n.EndsWith(".deb"),
            ["Ubuntu 24.04 LTS 이상 (64비트)",
             "서버 · 최소 설치 권장 (데스크톱이면 디스플레이 매니저를 끈다)"],
            ["sudo apt install ./funeralv2-player_<버전>_ubuntu24_amd64.deb",
             "sudo systemctl disable --now gdm3   (데스크톱 설치인 경우)",
             "sudo reboot"]),

        new("Ubuntu (arm64)",
            "24.04 LTS · Jetson 등 · .deb",
            n => n.Contains("ubuntu24") && n.Contains("arm64") && n.EndsWith(".deb"),
            ["Ubuntu 24.04 LTS 이상 (arm64)",
             "Ubuntu 를 올린 arm 보드 (라즈베리파이는 위 카드를 쓴다)"],
            ["sudo apt install ./funeralv2-player_<버전>_ubuntu24_arm64.deb", "sudo reboot"]),

        new("Android TV",
            "TV 박스 · 태블릿 · .apk",
            n => n.EndsWith(".apk"),
            ["Android 5.0 이상 (TV 박스 · 태블릿)", "알 수 없는 출처 설치 허용 필요"],
            ["adb install -r funeralv2_player-<버전>-android-*.apk",
             "또는 USB · Downloader 앱으로 설치",
             "debugsigned 는 업데이트 시 기존 앱을 지우고 다시 설치"]),

        new("수동 설치 (arm64)",
            "deb 를 못 쓰는 환경 · tar.gz",
            n => n.Contains("arm64") && n.EndsWith(".tar.gz"),
            ["deb 를 쓸 수 없는 arm64 환경", "의존 패키지 수동 설치 필요"],
            ["압축을 풀고 동봉된 README.txt 절차대로 배치"]),

        new("수동 설치 (x64)",
            "deb 를 못 쓰는 환경 · tar.gz",
            n => n.Contains("amd64") && n.EndsWith(".tar.gz"),
            ["deb 를 쓸 수 없는 x64 환경", "의존 패키지 수동 설치 필요"],
            ["압축을 풀고 동봉된 README.txt 절차대로 배치"]),
    ];

    private PlayerLatest _latest = new();

    private string PublishedText =>
        _latest.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// 릴리스가 없어도 <b>실패가 아니다.</b> 서버가 까닭을 <c>Warning</c> 으로 주고
    /// 화면은 카드를 「파일 없음」으로 그린다 — 어느 OS 가 준비돼 있는지는
    /// 릴리스 전에도 보여 줘야 한다.
    /// </summary>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _latest = await Api.GetPlayerLatestAsync();

        // 서버가 릴리스를 읽으며 알아낸 것이다 — 조회의 **결과**라 토스트다.
        Say(_latest.Warning, NoticeTone.Warning);

        return 1;
    }, failMessage: "릴리스 정보를 읽지 못했습니다");

    private PlayerAsset? Match(Platform platform) =>
        _latest.Assets.FirstOrDefault(a => platform.Matches(a.Name.ToLowerInvariant()));

    private static string Size(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} MB"
            : $"{bytes / 1024d:0} KB";
}
