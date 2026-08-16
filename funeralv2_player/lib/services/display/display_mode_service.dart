import 'dart:io';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// [디스플레이 해상도 프리셋]
/// 사이니지 패널은 대부분 16:9 이지만, 일부 현장은 16:10 패널을 사용한다.
/// 두 비율에 맞는 출력 해상도를 프리셋으로 제공한다.
enum DisplayAspect {
  /// 16:9 후보. 대부분의 사이니지 패널이 1920x1080 을 지원한다.
  ratio16x9('16:9', [(1920, 1080), (1600, 900), (1280, 720)]),

  /// 16:10 후보. 1920x1200 을 지원하지 않는 패널이 많아
  /// 같은 비율의 하위 해상도까지 순차적으로 시도한다.
  ratio16x10('16:10', [(1920, 1200), (1680, 1050), (1440, 900), (1280, 800)]);

  const DisplayAspect(this.label, this.candidates);

  final String label;

  /// 우선순위 순 해상도 후보 목록. 패널이 지원하는 첫 번째 것을 사용한다.
  final List<(int, int)> candidates;

  /// 대표 해상도 (버튼에 표시할 값)
  (int, int) get preferred => candidates.first;

  /// SharedPreferences 에 저장할 값
  String get storageValue => label;

  static DisplayAspect fromStorage(String? value) {
    return DisplayAspect.values.firstWhere(
      (e) => e.storageValue == value,
      orElse: () => DisplayAspect.ratio16x9,
    );
  }
}

/// [디스플레이 출력 모드 제어 서비스]
///
/// 라즈베리파이(Wayland/cage) 환경에서 출력 해상도를 바꾼다.
/// wlroots 계열 컴포지터는 커널의 video= 파라미터를 무시하고 EDID 의 선호 모드를 쓰기 때문에,
/// 컴포지터가 뜬 뒤 wlr-randr 로 모드를 지정해야 한다.
///
/// Windows 등 다른 플랫폼에서는 창 크기를 OS 가 관리하므로 이 서비스는 동작하지 않고
/// [isSupported] 가 false 를 반환한다.
class DisplayModeService {
  static const String prefsKey = 'displayAspectRatio';

  /// 기본 출력 커넥터. 환경변수 PLAYER_OUTPUT 으로 덮어쓸 수 있다.
  static String get _defaultOutput =>
      Platform.environment['PLAYER_OUTPUT'] ?? 'HDMI-A-1';

  /// 이 플랫폼에서 해상도 전환을 지원하는지 여부
  static bool get isSupported => !kIsWeb && Platform.isLinux;

  /// 저장된 비율 설정을 읽는다. 저장값이 없으면 16:9.
  static Future<DisplayAspect> loadSaved() async {
    final prefs = await SharedPreferences.getInstance();
    return DisplayAspect.fromStorage(prefs.getString(prefsKey));
  }

  /// 비율 설정을 저장한다.
  static Future<void> save(DisplayAspect aspect) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(prefsKey, aspect.storageValue);
  }

  /// 실제 출력 커넥터 이름을 조회한다. 실패 시 기본값을 돌려준다.
  /// wlr-randr 의 첫 줄이 커넥터 이름으로 시작한다. (예: `HDMI-A-1 "..."`)
  static Future<String> detectOutput() async {
    if (!isSupported) return _defaultOutput;
    try {
      final result = await Process.run('wlr-randr', const []);
      if (result.exitCode == 0) {
        for (final line in (result.stdout as String).split('\n')) {
          if (line.isEmpty || line.startsWith(' ')) continue;
          final name = line.split(' ').first.trim();
          if (name.isNotEmpty) return name;
        }
      }
    } catch (e) {
      print('[DisplayMode] 커넥터 조회 실패: $e');
    }
    return _defaultOutput;
  }

  /// 패널이 지원하는 해상도 목록을 조회한다. (예: {(1920,1080), (1440,900), ...})
  /// wlr-randr 출력의 "  1920x1080 px, 60.000000 Hz" 형태 줄을 파싱한다.
  static Future<Set<(int, int)>> supportedModes() async {
    final modes = <(int, int)>{};
    if (!isSupported) return modes;
    try {
      final result = await Process.run('wlr-randr', const []);
      if (result.exitCode != 0) return modes;

      final pattern = RegExp(r'(\d+)x(\d+)\s+px');
      for (final line in (result.stdout as String).split('\n')) {
        final m = pattern.firstMatch(line);
        if (m != null) {
          modes.add((int.parse(m.group(1)!), int.parse(m.group(2)!)));
        }
      }
    } catch (e) {
      print('[DisplayMode] 모드 목록 조회 실패: $e');
    }
    return modes;
  }

  /// 지정한 비율의 해상도를 즉시 적용한다.
  ///
  /// 패널마다 지원 해상도가 다르므로(예: 1920x1200 을 지원하지 않는 16:10 패널)
  /// 같은 비율의 후보를 우선순위대로 시도하고, 실제로 적용된 것을 돌려준다.
  /// 반환값은 (성공 여부, 사용자에게 보여줄 메시지).
  static Future<(bool, String)> apply(DisplayAspect aspect) async {
    if (!isSupported) {
      return (false, '이 플랫폼에서는 해상도 전환을 지원하지 않습니다.');
    }

    final output = await detectOutput();
    final available = await supportedModes();

    // 패널이 지원한다고 보고한 후보만 추린다.
    // 목록 조회에 실패했다면(빈 집합) 그냥 전부 시도해 본다.
    final targets = available.isEmpty
        ? aspect.candidates
        : aspect.candidates.where(available.contains).toList();

    if (targets.isEmpty) {
      final list = aspect.candidates.map((c) => '${c.$1}x${c.$2}').join(', ');
      return (false, '${aspect.label} 해상도를 패널이 지원하지 않습니다. (시도 대상: $list)');
    }

    String lastError = '';
    for (final (width, height) in targets) {
      try {
        // 주사율은 붙이지 않는다.
        // 패널마다 실제 값이 60.000 이 아니라 59.901 / 59.939 처럼 미묘하게 다르고,
        // "1440x900@60" 처럼 정확히 맞지 않으면 wlr-randr 가 unknown mode 로 거부한다.
        // 해상도만 지정하면 컴포지터가 해당 해상도의 가장 적절한 주사율을 고른다.
        final result = await Process.run(
          'wlr-randr',
          ['--output', output, '--mode', '${width}x$height'],
        );

        if (result.exitCode == 0) {
          print('[DisplayMode] 적용 완료: $output ${width}x$height');
          return (true, '${aspect.label} ${width}x$height 적용됨');
        }
        lastError = (result.stderr as String).trim();
        print('[DisplayMode] ${width}x$height 실패, 다음 후보 시도: $lastError');
      } on ProcessException catch (e) {
        print('[DisplayMode] wlr-randr 실행 불가: $e');
        return (false, 'wlr-randr 가 설치되어 있지 않습니다.');
      } catch (e) {
        lastError = '$e';
      }
    }

    return (false, '${aspect.label} 적용 실패: $lastError');
  }

  /// 앱 기동 시 저장된 비율을 다시 적용한다.
  /// (cage 런처가 기본 모드를 잡아두므로, 저장값이 있으면 앱이 덮어쓴다)
  static Future<void> applySavedOnStartup() async {
    if (!isSupported) return;
    try {
      final aspect = await loadSaved();
      await apply(aspect);
    } catch (e) {
      print('[DisplayMode] 기동 시 적용 실패: $e');
    }
  }
}
