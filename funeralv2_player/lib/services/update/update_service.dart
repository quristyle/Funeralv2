import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:package_info_plus/package_info_plus.dart';
import 'package:path_provider/path_provider.dart';

/// [플레이어 새 버전 확인 · 내려받기 서비스]
///
/// 설치 파일은 GitHub Releases 에서 온다. 저장소에 버전 태그(`v1.0.0`)를 밀면
/// `.github/workflows/release.yml` 이 OS 별 산출물을 만들어 릴리스에 첨부한다.
/// 포털의 `/system/player-download` 화면이 보는 것과 **같은 곳**을 본다.
///
/// [왜 통합 서버를 거치지 않고 GitHub 을 직접 보는가]
/// 저장소가 공개라 인증 없이 읽힌다. 포털 화면도 브라우저에서 같은 API 를 직접 부른다.
/// 서버를 거치게 하면 엔드포인트가 하나 더 늘 뿐 얻는 것이 없다.
/// 대신 **인터넷이 닿아야 한다** — 설정 화면이 공인 IP 를 보려고 api.ipify.org 를
/// 부르는 것과 같은 전제다. 닿지 않으면 확인이 실패하고, 그때도 재생은 그대로 돈다.
///
/// [이 서비스가 하지 않는 것]
/// 데스크톱(윈도우 · 리눅스)에서 **스스로 교체하지 않는다.** 돌고 있는 실행 파일을
/// 자기가 덮어쓸 수 없고, 리눅스는 `.deb` 설치에 root 가 필요하다.
/// 무엇을 어떻게 자동화할지는 결정 대기다 — docs/analysis/44-player-self-update.md.
class UpdateService {
  /// 릴리스가 올라가는 저장소. 포털 다운로드 화면과 같은 값이다.
  static const String repo = 'quristyle/Funeralv2';

  /// 릴리스 목록 페이지 (사람이 브라우저로 열 주소)
  static const String releasesPage = 'https://github.com/$repo/releases';

  /// 안드로이드 설치 화면을 띄우기 위한 채널. MainActivity 가 받는다.
  static const MethodChannel _channel =
      MethodChannel('com.quristyle.funeralv2_player/update');

  /// 이 플랫폼에서 앱이 스스로 설치까지 할 수 있는지.
  /// 안드로이드만 가능하다 — 시스템 설치 화면을 띄우는 길이 열려 있다.
  static bool get canInstallInPlace => !kIsWeb && Platform.isAndroid;

  // ── 버전 비교 ────────────────────────────────────────────────────────────

  /// `v1.0.0` · `1.0.0` · `1.0.0+3` 을 모두 받는다.
  ///
  /// 빌드 번호(`+3`)는 **비교에서 뺀다.** 릴리스 태그에는 빌드 번호가 없고
  /// 앱 자신의 버전에는 붙어 있어서, 그대로 비교하면 같은 버전이 달라 보인다.
  static List<int> parseVersion(String raw) {
    final core = raw.trim().replaceFirst(RegExp(r'^[vV]'), '').split('+').first;
    final parts = core.split('.');
    final out = <int>[];
    for (final part in parts) {
      // `1.0.0-rc1` 처럼 숫자 뒤에 꼬리가 붙어도 숫자 부분만 읽는다.
      final digits = RegExp(r'^\d+').firstMatch(part.trim())?.group(0);
      out.add(digits == null ? 0 : int.parse(digits));
    }
    while (out.length < 3) {
      out.add(0);
    }
    return out;
  }

  /// a 가 b 보다 크면 양수, 같으면 0, 작으면 음수.
  static int compareVersions(String a, String b) {
    final va = parseVersion(a);
    final vb = parseVersion(b);
    final len = va.length > vb.length ? va.length : vb.length;
    for (var i = 0; i < len; i++) {
      final x = i < va.length ? va[i] : 0;
      final y = i < vb.length ? vb[i] : 0;
      if (x != y) return x - y;
    }
    return 0;
  }

  // ── 이 기기에 맞는 설치 파일 ─────────────────────────────────────────────

  static PlayerTarget? _cachedTarget;

  /// 이 기기가 받아야 하는 자산이 무엇인지 판정한다. 한 번 판정하면 기억한다.
  static Future<PlayerTarget> target() async {
    return _cachedTarget ??= await _detectTarget();
  }

  static Future<PlayerTarget> _detectTarget() async {
    if (Platform.isWindows) {
      return const PlayerTarget(key: 'windows-x64', label: 'Windows x64');
    }
    if (Platform.isAndroid) {
      return const PlayerTarget(key: 'android', label: 'Android');
    }
    if (Platform.isLinux) {
      final distro = await _linuxDistroTag();
      final arch = await _linuxArch();
      return PlayerTarget(
        key: '$distro-$arch',
        label: 'Linux $distro $arch',
        distro: distro,
        arch: arch,
      );
    }
    return PlayerTarget(key: 'unsupported', label: Platform.operatingSystem);
  }

  /// `/etc/os-release` 를 읽어 릴리스 자산 이름에 쓰이는 배포판 태그를 고른다.
  ///
  /// 이름 규칙은 `packaging/build_deb.sh` 가 정하고 그쪽도 같은 파일을 읽는다.
  /// **리눅스 빌드는 빌드한 곳의 glibc 를 그대로 요구하므로** 배포판을 틀리면
  /// 받아도 실행되지 않는다(23번 문서 1절).
  static Future<String> _linuxDistroTag() async {
    try {
      final file = File(
          Platform.environment['PLAYER_OS_RELEASE'] ?? '/etc/os-release');
      if (!await file.exists()) return 'debian13';
      final map = <String, String>{};
      for (final line in await file.readAsLines()) {
        final i = line.indexOf('=');
        if (i <= 0) continue;
        map[line.substring(0, i)] =
            line.substring(i + 1).replaceAll('"', '').trim();
      }
      final id = (map['ID'] ?? '').toLowerCase();
      final major = (map['VERSION_ID'] ?? '').split('.').first;
      if (id == 'ubuntu') return major == '22' ? 'ubuntu22' : 'ubuntu24';
      if (id == 'debian') return major == '12' ? 'debian12' : 'debian13';
      if (id == 'raspbian') return 'debian13';
      // 모르는 배포판은 라즈베리파이(debian13) 로 본다. 지금 만들고 있는 자산 중
      // 가장 낮은 glibc 로 빌드된 것이라, 틀렸을 때 그래도 돌아갈 확률이 높다.
      return 'debian13';
    } catch (_) {
      return 'debian13';
    }
  }

  static Future<String> _linuxArch() async {
    try {
      final result = await Process.run('uname', ['-m']);
      final machine = result.stdout.toString().trim().toLowerCase();
      if (machine.contains('aarch64') || machine.contains('arm64')) {
        return 'arm64';
      }
      return 'amd64';
    } catch (_) {
      return 'amd64';
    }
  }

  // ── 릴리스 조회 ──────────────────────────────────────────────────────────

  /// 최신 릴리스를 조회하고 이 기기에 맞는 자산까지 골라 돌려준다.
  ///
  /// 실패해도 예외를 던지지 않는다 — 설정 화면이 상태 문구만 바꿔 보여 주면 되고,
  /// 확인이 안 되는 것 때문에 화면이 깨지면 안 된다.
  static Future<UpdateCheck> check() async {
    final current = await currentVersion();
    final myTarget = await target();

    try {
      final res = await http
          .get(
            Uri.parse('https://api.github.com/repos/$repo/releases/latest'),
            headers: const {'Accept': 'application/vnd.github+json'},
          )
          .timeout(const Duration(seconds: 8));

      if (res.statusCode == 404) {
        return UpdateCheck.failed(
          current,
          myTarget,
          '아직 발행된 릴리스가 없습니다.',
        );
      }
      if (res.statusCode != 200) {
        return UpdateCheck.failed(
          current,
          myTarget,
          '릴리스 정보를 가져오지 못했습니다. (HTTP ${res.statusCode})',
        );
      }

      final body = jsonDecode(utf8.decode(res.bodyBytes)) as Map<String, dynamic>;
      final tag = (body['tag_name'] ?? '').toString();
      final assets = <ReleaseAsset>[
        for (final a in (body['assets'] as List<dynamic>? ?? const []))
          ReleaseAsset(
            name: (a['name'] ?? '').toString(),
            url: (a['browser_download_url'] ?? '').toString(),
            size: (a['size'] as num?)?.toInt() ?? 0,
          ),
      ];

      return UpdateCheck(
        currentVersion: current,
        latestVersion: tag,
        target: myTarget,
        asset: myTarget.pick(assets),
        releaseUrl: (body['html_url'] ?? releasesPage).toString(),
        publishedAt: (body['published_at'] ?? '').toString(),
        // 현재 버전을 모르면 새 버전이 있다고 하지 않는다. 모르는 것과
        // "낮다" 는 것은 다르고, 붉은 점만 계속 찍혀 있으면 아무도 안 보게 된다.
        hasUpdate: tag.isNotEmpty &&
            current != unknownVersion &&
            compareVersions(tag, current) > 0,
      );
    } on TimeoutException {
      return UpdateCheck.failed(
        current,
        myTarget,
        'GitHub 응답이 없습니다. 이 장비가 인터넷에 닿지 않는 환경일 수 있습니다.',
      );
    } catch (e) {
      return UpdateCheck.failed(
        current,
        myTarget,
        '릴리스 정보를 가져오지 못했습니다. ($e)',
      );
    }
  }

  /// 현재 버전을 읽지 못했을 때 쓰는 값
  static const String unknownVersion = '알 수 없음';

  /// 지금 돌고 있는 앱의 버전. `pubspec.yaml` 의 `version` 이 그대로 온다.
  ///
  /// 읽는 곳이 플랫폼마다 다르다 — 윈도우는 exe 의 버전 정보(`ProductVersion`),
  /// 리눅스는 실행 파일 옆의 `data/flutter_assets/version.json`, 안드로이드는
  /// 패키지 정보다.
  ///
  /// **빈 값을 그대로 돌려주지 않는다.** 빈 값은 버전 0 으로 읽혀서
  /// "항상 새 버전이 있음" 이 되기 때문이다(리눅스에서 version.json 을 못 읽는 경우).
  static Future<String> currentVersion() async {
    try {
      final info = await PackageInfo.fromPlatform();
      final version = info.version.trim();
      return version.isEmpty ? unknownVersion : version;
    } catch (_) {
      return unknownVersion;
    }
  }

  // ── 내려받기 ─────────────────────────────────────────────────────────────

  /// 자산을 내려받아 파일로 저장한다. 진행률은 [onProgress] 로 알려 준다.
  ///
  /// 받는 중에 앱이 죽거나 네트워크가 끊겨도 반쪽 파일이 남지 않도록
  /// `.part` 로 받아서 다 받은 뒤에 이름을 바꾼다.
  static Future<File> download(
    ReleaseAsset asset, {
    void Function(int received, int total)? onProgress,
  }) async {
    final dir = await downloadDirectory();
    final saved = File('${dir.path}${Platform.pathSeparator}${asset.name}');

    // 같은 이름·같은 크기로 이미 받아 둔 것이 있으면 다시 받지 않는다.
    // 100MB 짜리(APK)를 다시 받게 하면 현장 회선을 두 번 먹는다.
    if (await saved.exists() && await saved.length() == asset.size) {
      onProgress?.call(asset.size, asset.size);
      return saved;
    }

    final part = File('${saved.path}.part');
    if (await part.exists()) await part.delete();

    final client = http.Client();
    try {
      final res = await client.send(http.Request('GET', Uri.parse(asset.url)));
      if (res.statusCode != 200) {
        throw HttpException('HTTP ${res.statusCode}', uri: Uri.parse(asset.url));
      }
      final total = res.contentLength ?? asset.size;
      var received = 0;
      final sink = part.openWrite();
      try {
        await sink.addStream(res.stream.map((chunk) {
          received += chunk.length;
          onProgress?.call(received, total);
          return chunk;
        }));
      } finally {
        await sink.close();
      }
      if (await saved.exists()) await saved.delete();
      await part.rename(saved.path);
      return saved;
    } finally {
      client.close();
    }
  }

  /// 받은 파일을 둘 곳.
  ///
  /// 안드로이드는 **앱 캐시**에 둔다 — FileProvider 로 설치 화면에 넘길 수 있고,
  /// 저장 권한이 필요 없다. 데스크톱은 내려받기 폴더에 둔다(사람이 찾아가야 한다).
  static Future<Directory> downloadDirectory() async {
    if (Platform.isAndroid) {
      return getTemporaryDirectory();
    }
    try {
      final downloads = await getDownloadsDirectory();
      if (downloads != null) return downloads;
    } catch (_) {
      // 리눅스에서 XDG 설정이 없으면 null 이거나 예외다. 임시 폴더로 떨어진다.
    }
    return getTemporaryDirectory();
  }

  // ── 설치 (안드로이드) ────────────────────────────────────────────────────

  /// 받은 APK 의 시스템 설치 화면을 띄운다.
  ///
  /// **조용히 설치되지 않는다.** 안드로이드는 시스템 앱이 아닌 앱이 사용자 확인 없이
  /// 패키지를 깔 수 없다. 화면에 뜨는 확인을 사람이 눌러야 한다
  /// (TV 박스에서는 리모컨으로).
  ///
  /// 처음 한 번은 "알 수 없는 앱 설치" 를 허용해야 한다. 허용되지 않았으면
  /// [installAllowed] 가 false 를 주고, [openInstallSettings] 로 그 설정 화면을 연다.
  static Future<void> installApk(File apk) async {
    await _channel.invokeMethod<void>('installApk', {'path': apk.path});
  }

  /// "알 수 없는 앱 설치" 가 이 앱에 허용되어 있는지.
  static Future<bool> installAllowed() async {
    if (!Platform.isAndroid) return false;
    try {
      return await _channel.invokeMethod<bool>('installAllowed') ?? false;
    } on PlatformException {
      return false;
    }
  }

  /// "알 수 없는 앱 설치" 허용 설정 화면을 연다.
  static Future<void> openInstallSettings() async {
    await _channel.invokeMethod<void>('openInstallSettings');
  }
}

/// [이 기기가 받아야 하는 자산 판정]
///
/// 릴리스 자산 이름에는 플랫폼 · 배포판 · 아키텍처가 들어 있다.
/// `.deb` 로만 고르면 라즈베리파이용을 Ubuntu 에 주게 된다(포털 화면도 같은 이유로
/// matcher 가 배포판까지 본다).
class PlayerTarget {
  const PlayerTarget({
    required this.key,
    required this.label,
    this.distro,
    this.arch,
  });

  /// `windows-x64` · `android` · `ubuntu24-amd64` 같은 식별자
  final String key;

  /// 화면에 보여 줄 이름
  final String label;

  /// 리눅스에서만 채워진다 (`debian13` · `ubuntu24` …)
  final String? distro;

  /// 리눅스에서만 채워진다 (`amd64` · `arm64`)
  final String? arch;

  /// 이 기기에서 설치 파일을 앱이 직접 다룰 수 있는지
  bool get isAndroid => key == 'android';

  /// 자산 목록에서 이 기기 것을 고른다. 없으면 null.
  ReleaseAsset? pick(List<ReleaseAsset> assets) {
    final candidates = assets.where((a) => matches(a.name.toLowerCase())).toList();
    if (candidates.isEmpty) return null;
    // APK 는 서명 방식이 둘일 수 있다. 릴리스 키로 서명된 것을 먼저 쓴다 —
    // 디버그 키로 서명된 것은 서명이 매번 달라 덮어쓰기 업데이트가 거부된다.
    candidates.sort((a, b) {
      final ax = a.name.toLowerCase().contains('releasesigned') ? 0 : 1;
      final bx = b.name.toLowerCase().contains('releasesigned') ? 0 : 1;
      return ax - bx;
    });
    return candidates.first;
  }

  /// 자산 이름(소문자)이 이 기기 것인지.
  bool matches(String name) {
    switch (key) {
      case 'windows-x64':
        return name.contains('windows') && name.endsWith('.zip');
      case 'android':
        return name.endsWith('.apk');
      case 'unsupported':
        return false;
      default:
        // 리눅스. 배포판과 아키텍처가 모두 맞는 `.deb` 만 고른다.
        // `.tar.gz`(수동 설치용) 는 고르지 않는다 — 같은 조건에 둘이 걸린다.
        return name.contains(distro ?? '') &&
            name.contains(arch ?? '') &&
            name.endsWith('.deb');
    }
  }
}

/// 릴리스에 붙어 있는 파일 하나
class ReleaseAsset {
  const ReleaseAsset({
    required this.name,
    required this.url,
    required this.size,
  });

  final String name;
  final String url;
  final int size;

  /// `40.1 MB` 처럼 사람이 읽는 크기
  String get sizeText {
    if (size >= 1024 * 1024) {
      return '${(size / 1024 / 1024).toStringAsFixed(1)} MB';
    }
    return '${(size / 1024).toStringAsFixed(0)} KB';
  }
}

/// 확인 결과 한 벌
class UpdateCheck {
  const UpdateCheck({
    required this.currentVersion,
    required this.latestVersion,
    required this.target,
    required this.asset,
    required this.releaseUrl,
    required this.publishedAt,
    required this.hasUpdate,
    this.error,
  });

  /// 확인이 실패했을 때
  factory UpdateCheck.failed(
    String current,
    PlayerTarget target,
    String message,
  ) {
    return UpdateCheck(
      currentVersion: current,
      latestVersion: '',
      target: target,
      asset: null,
      releaseUrl: UpdateService.releasesPage,
      publishedAt: '',
      hasUpdate: false,
      error: message,
    );
  }

  final String currentVersion;
  final String latestVersion;
  final PlayerTarget target;
  final ReleaseAsset? asset;
  final String releaseUrl;
  final String publishedAt;
  final bool hasUpdate;
  final String? error;

  bool get failed => error != null;

  /// 현재 버전을 읽지 못한 경우. 이때는 새 버전 여부를 판정하지 않고,
  /// 최신 버전과 받을 파일만 보여 준다(사람이 보고 판단한다).
  bool get currentUnknown => currentVersion == UpdateService.unknownVersion;

  /// 새 버전은 있는데 이 기기용 파일이 릴리스에 없는 경우.
  /// (예: 안드로이드 job 만 실패한 릴리스)
  bool get missingAsset => hasUpdate && asset == null;
}
