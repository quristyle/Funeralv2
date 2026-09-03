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
/// 무엇을 어떻게 자동화할지는 결정 대기다 — docs/analysis/48-player-self-update.md.
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

  // ── 설치 (윈도우) — 결정 D-P1 ────────────────────────────────────────────
  //
  // 돌고 있는 exe 는 자기를 덮어쓸 수 없다. 그래서 **도우미 스크립트**를 임시 폴더에
  // 쓰고 분리(detached) 실행한 뒤 앱이 스스로 종료한다. 도우미가 하는 일:
  //
  //   앱 종료 대기 → 현재 설치본을 _prev 로 옮김(백업) → zip 풀어 덮음 → 새 exe 기동
  //   → 60초 뒤 살아 있는지 확인 → 죽어 있으면 _prev 로 되돌리고 옛 버전을 다시 띄움
  //
  // 되돌림이 있으므로 교체 중 실패해도 화면이 죽은 채 남지 않는다.
  // 기록은 설치 폴더의 update.log 에 남는다.

  /// 설치 폴더에 쓸 수 있는지 먼저 본다. Program Files 처럼 관리자 권한이
  /// 필요한 곳이면 여기서 걸러서 **이유를 사람에게 알려 준다** —
  /// 도우미가 조용히 실패하는 것이 최악이다.
  static Future<bool> canReplaceInPlace() async {
    if (!Platform.isWindows) return false;
    try {
      final dir = File(Platform.resolvedExecutable).parent;
      final probe = File('${dir.path}${Platform.pathSeparator}.write_probe');
      await probe.writeAsString('x');
      await probe.delete();
      return true;
    } catch (_) {
      return false;
    }
  }

  /// 받은 zip 으로 교체를 시작한다. **이 함수는 돌아오지 않는다** — 도우미를 띄우고
  /// 앱을 종료한다. 호출 전에 [canReplaceInPlace] 로 걸러야 한다.
  static Future<void> installWindowsUpdate(File zip) async {
    final exePath = Platform.resolvedExecutable;
    final installDir = File(exePath).parent.path;
    final exeName = exePath.split(Platform.pathSeparator).last;

    final helper = File(
        '${(await getTemporaryDirectory()).path}${Platform.pathSeparator}jsini_player_update.ps1');
    await helper.writeAsString(_windowsHelperScript, flush: true);

    // 분리 실행 — 앱이 죽어도 도우미는 살아서 교체를 진행한다.
    await Process.start(
      'powershell.exe',
      [
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', helper.path,
        '-ProcId', '$pid',
        '-InstallDir', installDir,
        '-ZipPath', zip.path,
        '-ExeName', exeName,
      ],
      mode: ProcessStartMode.detached,
    );

    // 도우미가 종료를 기다리고 있다. 정리할 시간을 짧게 주고 내려간다.
    await Future.delayed(const Duration(seconds: 2));
    exit(0);
  }

  /// 도우미 본문. 좌표 하드코딩 없이 인자만 받으므로 어느 설치 폴더에서든 동작한다.
  ///
  /// [실동 시험에서 배운 것들이 녹아 있다 — 고치기 전에 읽을 것]
  /// · 로그는 설치 폴더가 아니라 %TEMP% 에 둔다. 설치 폴더는 교체 중에 통째로
  ///   움직이므로 그 안의 로그는 함께 끌려가 "왜 실패했는지" 기록이 사라진다.
  /// · `Get-ChildItem -LiteralPath .. -Exclude ..` 를 쓰지 않는다 — PowerShell 5.1 은
  ///   이 조합에서 **-Exclude 를 무시**해서 백업 이동이 백업 폴더 자신까지 집어삼킨다.
  ///   Where-Object 로 이름을 직접 거른다.
  /// · 경로는 처음에 Resolve-Path 로 정규화한다(빗금 방향 섞임 방지).
  /// · 되돌림은 자기만의 try/catch 를 가진다 — 되돌림 중 예외가 바깥 catch 로 빠지면
  ///   화면이 죽은 채 남는다. 어떤 경로로든 마지막에 exe 기동을 시도한다.
  static const String _windowsHelperScript = r'''
param([int]$ProcId, [string]$InstallDir, [string]$ZipPath, [string]$ExeName)
$log = Join-Path $env:TEMP 'jsini_player_update.log'
function Log($m) { "$(Get-Date -Format o)  $m" | Add-Content -Path $log }

function Restore-Prev($InstallDir, $Prev, $ExeName) {
  # 되돌림. 여기서의 실패는 삼키고 기록만 한다 — 화면을 살리는 것이 우선이다.
  try {
    Get-ChildItem -LiteralPath $InstallDir -Force |
      Where-Object { $_.Name -ne '_prev' } |
      Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath $Prev -Force |
      Move-Item -Destination $InstallDir -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $Prev -Recurse -Force -ErrorAction SilentlyContinue
    Log "되돌림 수행"
  } catch { Log "되돌림 중 오류(계속 진행): $_" }
  $exe = Join-Path $InstallDir $ExeName
  if (Test-Path -LiteralPath $exe) {
    Start-Process -FilePath $exe -WorkingDirectory $InstallDir
    Log "옛 버전 재기동"
  } else {
    Log "치명: 되돌린 뒤에도 $exe 가 없다 — 수동 복구 필요"
  }
}

try {
  $InstallDir = (Resolve-Path -LiteralPath $InstallDir).Path
  $ZipPath    = (Resolve-Path -LiteralPath $ZipPath).Path
  Log "== 교체 시작 (설치: $InstallDir / zip: $ZipPath)"

  Wait-Process -Id $ProcId -Timeout 120 -ErrorAction SilentlyContinue
  if (Get-Process -Id $ProcId -ErrorAction SilentlyContinue) {
    Log "앱이 120초 안에 종료되지 않아 중단한다"; exit 1
  }
  Log "앱 종료 확인"

  $staging = Join-Path $env:TEMP ("jsini_player_new_" + [guid]::NewGuid().ToString('N'))
  Expand-Archive -Path $ZipPath -DestinationPath $staging -Force
  if (-not (Test-Path -LiteralPath (Join-Path $staging $ExeName))) {
    Log "zip 안에 $ExeName 이 없다 — 중단"; exit 3
  }
  Log "압축 해제 완료: $staging"

  $prev = Join-Path $InstallDir '_prev'
  if (Test-Path -LiteralPath $prev) { Remove-Item -LiteralPath $prev -Recurse -Force }
  New-Item -ItemType Directory -Path $prev | Out-Null
  Get-ChildItem -LiteralPath $InstallDir -Force |
    Where-Object { $_.Name -ne '_prev' } |
    Move-Item -Destination $prev -Force
  Log "현재 버전 백업 완료 (_prev)"

  Copy-Item -Path (Join-Path $staging '*') -Destination $InstallDir -Recurse -Force
  Remove-Item -LiteralPath $staging -Recurse -Force
  Log "새 버전 복사 완료"

  $exe = Join-Path $InstallDir $ExeName
  Start-Process -FilePath $exe -WorkingDirectory $InstallDir
  Log "새 버전 기동 — 60초 생존 확인"
  Start-Sleep -Seconds 60

  $name = [IO.Path]::GetFileNameWithoutExtension($ExeName)
  $alive = Get-Process -Name $name -ErrorAction SilentlyContinue
  if ($alive) { Log "생존 확인 — 교체 완료 (_prev 는 다음 교체 때 지워진다)"; exit 0 }

  Log "새 버전이 죽어 있다 — 되돌린다"
  Restore-Prev $InstallDir $prev $ExeName
  exit 2
} catch {
  Log ("오류: " + ($_ | Out-String).Trim())
  # 교체 도중 어디서 멈췄든, 백업이 있으면 화면부터 되살린다.
  $prev = Join-Path $InstallDir '_prev'
  if (Test-Path -LiteralPath $prev) { Restore-Prev $InstallDir $prev $ExeName }
  exit 9
}
''';

  // ── 설치 (리눅스) — 결정 D-P2 ────────────────────────────────────────────
  //
  // .deb 설치에는 root 가 필요하다. 새 .deb(v1.0.2+)가 설치한 sudoers 한 줄이
  // **인자 없는 도우미 스크립트 하나**만 허용한다. systemd-run 으로 부르는 이유는
  // apt 가 서비스를 재시작할 때 sudo 자식(우리)이 같이 죽기 때문 —
  // 별도 유닛으로 떼어 내면 설치가 끝까지 간다.

  /// 도우미가 집어 가는 고정 폴더. postinst 가 플레이어 계정 소유로 만들어 둔다.
  static const String linuxUpdatesDir = '/var/lib/funeralv2-player/updates';

  /// 받은 .deb 를 설치한다. 성공하면 곧 systemd 가 앱을 재시작하므로
  /// 이 함수가 돌아온 직후 앱이 죽는 것이 정상이다.
  /// 실패하면 (사람이 볼) 실패 사유를 돌려준다.
  static Future<String?> installLinuxUpdate(File deb) async {
    try {
      final dir = Directory(linuxUpdatesDir);
      if (!await dir.exists()) {
        return '업데이트 폴더가 없습니다 ($linuxUpdatesDir).\n'
            '이 기능은 v1.0.2 이상 .deb 로 설치된 장비에서만 동작합니다 — '
            '이번 한 번은 수동으로 설치해 주세요.';
      }
      final name = deb.path.split(Platform.pathSeparator).last;
      await deb.copy('$linuxUpdatesDir/$name');

      // sudoers 의 허용 줄과 **정확히 같은** 명령이어야 한다. 인자를 더하면 거부된다.
      final result = await Process.run('sudo', [
        '-n', // 비밀번호를 물을 상황이면 묻지 말고 실패해라 (키오스크에는 답할 사람이 없다)
        '/usr/bin/systemd-run',
        '--unit=funeralv2-player-update',
        '--collect',
        '/usr/lib/funeralv2-player/apply-update',
      ]);
      if (result.exitCode != 0) {
        return '설치 권한이 없습니다 (sudo 거부).\n'
            '${result.stderr.toString().trim()}\n'
            '수동 설치: sudo apt install $linuxUpdatesDir/$name';
      }
      return null; // 성공 — 잠시 후 systemd 가 새 버전으로 재시작한다
    } catch (e) {
      return '설치 시작에 실패했습니다. ($e)';
    }
  }

  // ── 원격 지시 (SignalR "UpdateNow") — 결정 D-P3 ─────────────────────────

  static bool _remoteUpdateRunning = false;

  /// 포털이 SignalR 로 밀어 준 업그레이드 지시를 수행한다.
  ///
  /// 사이니지 화면에는 **아무것도 띄우지 않는다** — 빈소 화면 위에 대화 상자를
  /// 올릴 수는 없다. 진행과 실패는 전부 로그로 남긴다.
  /// 안드로이드는 시스템 설치 확인을 원격에서 대신 눌러 줄 수 없으므로
  /// **내려받기까지만** 하고, 설치는 현장(리모컨)에서 잇는다.
  static Future<void> runRemoteUpdate() async {
    if (_remoteUpdateRunning) {
      print('[Update] 원격 지시 중복 수신 — 이미 진행 중이라 무시');
      return;
    }
    _remoteUpdateRunning = true;
    try {
      final r = await check();
      if (r.failed) {
        print('[Update] 원격 지시: 확인 실패 — ${r.error}');
        return;
      }
      if (!r.hasUpdate || r.asset == null) {
        print('[Update] 원격 지시: 새 버전 없음 (현재 ${r.currentVersion}, 최신 ${r.latestVersion})');
        return;
      }

      print('[Update] 원격 지시: ${r.latestVersion} 내려받기 시작 (${r.asset!.name})');
      final file = await download(r.asset!);
      print('[Update] 내려받기 완료: ${file.path}');

      if (Platform.isWindows) {
        if (!await canReplaceInPlace()) {
          print('[Update] 설치 폴더에 쓸 수 없어 교체 불가 — 수동 설치 필요');
          return;
        }
        await installWindowsUpdate(file); // 돌아오지 않는다 (앱 종료)
      } else if (Platform.isLinux) {
        final fail = await installLinuxUpdate(file);
        if (fail != null) print('[Update] 리눅스 설치 실패: $fail');
      } else if (Platform.isAndroid) {
        print('[Update] 안드로이드: 내려받기까지 완료 — 설치는 현장에서 (설정 → 버전 확인 → 설치)');
      }
    } catch (e) {
      print('[Update] 원격 지시 처리 중 오류: $e');
    } finally {
      _remoteUpdateRunning = false;
    }
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
